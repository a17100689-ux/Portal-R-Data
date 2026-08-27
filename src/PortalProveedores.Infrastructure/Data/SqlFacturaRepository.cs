using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using PortalProveedores.Application.Interfaces;
using PortalProveedores.Core.Entities;

namespace PortalProveedores.Infrastructure.Data;

public class SqlFacturaRepository : IFacturaRepository
{
    private readonly string _connectionString;

    public SqlFacturaRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? throw new InvalidOperationException("Cadena de conexión 'DefaultConnection' no encontrada.");
    }

    private IDbConnection CreateConnection() => new SqlConnection(_connectionString);

    public async Task<int> InsertarFacturaSpAsync(Factura factura, CancellationToken ct = default)
    {
        using var connection = CreateConnection();
        var parameters = new DynamicParameters();
        parameters.Add("@ProveedorId", factura.ProveedorId, DbType.Int32);
        parameters.Add("@UUID", factura.UUID, DbType.String, size: 50);
        parameters.Add("@Serie", factura.Serie, DbType.String, size: 20);
        parameters.Add("@Folio", factura.Folio, DbType.String, size: 50);
        parameters.Add("@RFCEmisor", factura.RFCEmisor, DbType.String, size: 15);
        parameters.Add("@RFCReceptor", factura.RFCReceptor, DbType.String, size: 15);
        parameters.Add("@FechaEmision", factura.FechaEmision, DbType.DateTime);
        parameters.Add("@Subtotal", factura.Subtotal, DbType.Decimal);
        parameters.Add("@ImpuestosTrasladados", factura.ImpuestosTrasladados, DbType.Decimal);
        parameters.Add("@ImpuestosRetenidos", factura.ImpuestosRetenidos, DbType.Decimal);
        parameters.Add("@Total", factura.Total, DbType.Decimal);
        parameters.Add("@Moneda", factura.Moneda, DbType.String, size: 10);
        parameters.Add("@EstatusId", (int)factura.Estatus, DbType.Int32);
        parameters.Add("@ArchivoXmlNombreInterno", factura.ArchivoXmlNombreInterno, DbType.String, size: 250);
        parameters.Add("@ArchivoXmlNombreOriginal", factura.ArchivoXmlNombreOriginal, DbType.String, size: 250);
        parameters.Add("@ArchivoPdfNombreInterno", factura.ArchivoPdfNombreInterno, DbType.String, size: 250);
        parameters.Add("@ArchivoPdfNombreOriginal", factura.ArchivoPdfNombreOriginal, DbType.String, size: 250);
        parameters.Add("@Observaciones", factura.Observaciones, DbType.String, size: 500);
        parameters.Add("@NuevoId", dbType: DbType.Int32, direction: ParameterDirection.Output);

        var cmd = new CommandDefinition(
            commandText: "sp_Factura_Insertar",
            parameters: parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct
        );

        await connection.ExecuteAsync(cmd);
        return parameters.Get<int>("@NuevoId");
    }

    public async Task<Factura?> ObtenerFacturaPorIdSpAsync(int id, CancellationToken ct = default)
    {
        using var connection = CreateConnection();
        var parameters = new DynamicParameters();
        parameters.Add("@Id", id, DbType.Int32);

        var cmd = new CommandDefinition(
            commandText: "sp_Factura_ObtenerPorId",
            parameters: parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct
        );

        return await connection.QueryFirstOrDefaultAsync<Factura>(cmd);
    }

    public async Task<Factura?> ObtenerFacturaPorUuidSpAsync(string uuid, CancellationToken ct = default)
    {
        using var connection = CreateConnection();
        var parameters = new DynamicParameters();
        parameters.Add("@UUID", uuid, DbType.String, size: 50);

        var cmd = new CommandDefinition(
            commandText: "sp_Factura_ObtenerPorUuid",
            parameters: parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct
        );

        return await connection.QueryFirstOrDefaultAsync<Factura>(cmd);
    }

    public async Task<IEnumerable<Factura>> ListarFacturasPorProveedorSpAsync(int proveedorId, CancellationToken ct = default)
    {
        using var connection = CreateConnection();
        var parameters = new DynamicParameters();
        parameters.Add("@ProveedorId", proveedorId, DbType.Int32);

        var cmd = new CommandDefinition(
            commandText: "sp_Factura_ListarPorProveedor",
            parameters: parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct
        );

        return await connection.QueryAsync<Factura>(cmd);
    }

    public async Task<IEnumerable<Factura>> ListarTodasFacturasSpAsync(EstadoFactura? estatus, CancellationToken ct = default)
    {
        using var connection = CreateConnection();
        var parameters = new DynamicParameters();
        parameters.Add("@EstatusId", estatus.HasValue ? (int)estatus.Value : null, DbType.Int32);

        var cmd = new CommandDefinition(
            commandText: "sp_Factura_ListarTodas",
            parameters: parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct
        );

        return await connection.QueryAsync<Factura>(cmd);
    }

    public async Task<bool> ActualizarEstatusFacturaSpAsync(int facturaId, EstadoFactura nuevoEstatus, string? motivoRechazo, CancellationToken ct = default)
    {
        using var connection = CreateConnection();
        var parameters = new DynamicParameters();
        parameters.Add("@FacturaId", facturaId, DbType.Int32);
        parameters.Add("@NuevoEstatusId", (int)nuevoEstatus, DbType.Int32);
        parameters.Add("@MotivoRechazo", motivoRechazo, DbType.String, size: 500);

        var cmd = new CommandDefinition(
            commandText: "sp_Factura_ActualizarEstatus",
            parameters: parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct
        );

        int rows = await connection.ExecuteAsync(cmd);
        return rows > 0;
    }

    public async Task<bool> ExisteUuidSpAsync(string uuid, CancellationToken ct = default)
    {
        using var connection = CreateConnection();
        var parameters = new DynamicParameters();
        parameters.Add("@UUID", uuid, DbType.String, size: 50);

        var cmd = new CommandDefinition(
            commandText: "sp_Factura_ExisteUuid",
            parameters: parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct
        );

        int count = await connection.ExecuteScalarAsync<int>(cmd);
        return count > 0;
    }
}
