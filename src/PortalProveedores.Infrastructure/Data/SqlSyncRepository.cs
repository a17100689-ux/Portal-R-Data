using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using PortalProveedores.Application.DTOs;
using PortalProveedores.Application.Interfaces;

namespace PortalProveedores.Infrastructure.Data;

public class SqlSyncRepository : ISyncRepository
{
    private readonly string _connectionString;

    public SqlSyncRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("PortalDb")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("La cadena de conexión 'DefaultConnection' o 'PortalDb' no está configurada.");
    }

    private IDbConnection CreateConnection() => new SqlConnection(_connectionString);

    public async Task<IEnumerable<SyncColaItemDto>> ObtenerLotePendienteSpAsync(int tamanoLote = 10, CancellationToken ct = default)
    {
        using var connection = CreateConnection();
        var parameters = new DynamicParameters();
        parameters.Add("@TamanoLote", tamanoLote, DbType.Int32);

        var cmd = new CommandDefinition(
            commandText: "dbo.sp_Sync_ObtenerLotePendiente",
            parameters: parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct
        );

        return await connection.QueryAsync<SyncColaItemDto>(cmd);
    }

    public async Task<decimal> EjecutarInsertCentralSpAsync(long facturaId, CancellationToken ct = default)
    {
        using var connection = CreateConnection();
        var parameters = new DynamicParameters();
        parameters.Add("@FacturaId", facturaId, DbType.Int64);
        parameters.Add("@NumeroDocumentoAsignado", dbType: DbType.Decimal, precision: 9, scale: 0, direction: ParameterDirection.Output);

        var cmd = new CommandDefinition(
            commandText: "dbo.sp_Sync_EjecutarInsertCentral_EnMismaInstancia",
            parameters: parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct
        );

        await connection.ExecuteAsync(cmd);
        return parameters.Get<decimal>("@NumeroDocumentoAsignado");
    }

    public async Task ConfirmarSincronizacionSpAsync(long syncId, long facturaId, decimal numeroDocumentoCentral, CancellationToken ct = default)
    {
        using var connection = CreateConnection();
        var parameters = new DynamicParameters();
        parameters.Add("@SyncId", syncId, DbType.Int64);
        parameters.Add("@FacturaId", facturaId, DbType.Int64);
        parameters.Add("@NumeroDocumentoCentral", numeroDocumentoCentral, DbType.Decimal, precision: 9, scale: 0);

        var cmd = new CommandDefinition(
            commandText: "dbo.sp_Sync_ConfirmarSincronizacion",
            parameters: parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct
        );

        await connection.ExecuteAsync(cmd);
    }

    public async Task RegistrarFalloSpAsync(long syncId, long facturaId, string mensajeError, CancellationToken ct = default)
    {
        using var connection = CreateConnection();
        var parameters = new DynamicParameters();
        parameters.Add("@SyncId", syncId, DbType.Int64);
        parameters.Add("@FacturaId", facturaId, DbType.Int64);
        parameters.Add("@MensajeError", mensajeError, DbType.String, size: 1000);

        var cmd = new CommandDefinition(
            commandText: "dbo.sp_Sync_RegistrarFallo",
            parameters: parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct
        );

        await connection.ExecuteAsync(cmd);
    }

    public async Task<string> RefrescarCatalogosDesdeCentralSpAsync(CancellationToken ct = default)
    {
        using var connection = CreateConnection();
        var cmd = new CommandDefinition(
            commandText: "dbo.sp_Sync_RefrescarCatalogosDesdeCentral",
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct
        );

        var resultado = await connection.QueryFirstOrDefaultAsync<string>(cmd);
        return resultado ?? "Catálogos actualizados exitosamente.";
    }

    public async Task<int> ActualizarEstatusEntregaMercanciaSpAsync(CancellationToken ct = default)
    {
        using var connection = CreateConnection();
        var cmd = new CommandDefinition(
            commandText: "dbo.sp_Sync_ActualizarEstatusEntregaMercancia",
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct
        );

        return await connection.QueryFirstOrDefaultAsync<int>(cmd);
    }

    public async Task<EstadoSincronizacionResumenDto> ObtenerResumenEstadoSyncAsync(CancellationToken ct = default)
    {
        using var connection = CreateConnection();
        var cmd = new CommandDefinition(
            commandText: "dbo.sp_Sync_ObtenerResumenEstado",
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct
        );

        var resumen = await connection.QueryFirstOrDefaultAsync<EstadoSincronizacionResumenDto>(cmd)
                      ?? new EstadoSincronizacionResumenDto();

        resumen.WorkerActivo = true;
        return resumen;
    }
}
