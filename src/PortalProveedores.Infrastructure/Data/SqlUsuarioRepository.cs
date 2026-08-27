using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using PortalProveedores.Application.Interfaces;
using PortalProveedores.Core.Entities;

namespace PortalProveedores.Infrastructure.Data;

public class SqlUsuarioRepository : IUsuarioRepository
{
    private readonly string _connectionString;

    public SqlUsuarioRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Cadena de conexión 'DefaultConnection' no encontrada.");
    }

    private IDbConnection CreateConnection() => new SqlConnection(_connectionString);

    public async Task<Usuario?> ObtenerPorUsernameSpAsync(string username, CancellationToken ct = default)
    {
        using var connection = CreateConnection();
        var parameters = new DynamicParameters();
        parameters.Add("@Username", username, DbType.String, size: 100);

        var cmd = new CommandDefinition(
            commandText: "sp_Usuario_ObtenerPorUsername",
            parameters: parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct
        );

        return await connection.QueryFirstOrDefaultAsync<Usuario>(cmd);
    }

    public async Task<Usuario?> ObtenerPorIdSpAsync(int id, CancellationToken ct = default)
    {
        using var connection = CreateConnection();
        var parameters = new DynamicParameters();
        parameters.Add("@Id", id, DbType.Int32);

        var cmd = new CommandDefinition(
            commandText: "sp_Usuario_ObtenerPorId",
            parameters: parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct
        );

        return await connection.QueryFirstOrDefaultAsync<Usuario>(cmd);
    }

    public async Task RegistrarIntentoFallidoSpAsync(int usuarioId, CancellationToken ct = default)
    {
        using var connection = CreateConnection();
        var parameters = new DynamicParameters();
        parameters.Add("@UsuarioId", usuarioId, DbType.Int32);
        parameters.Add("@MaxIntentos", 5, DbType.Int32);
        parameters.Add("@MinutosBloqueo", 15, DbType.Int32);

        var cmd = new CommandDefinition(
            commandText: "sp_Usuario_RegistrarIntentoFallido",
            parameters: parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct
        );

        await connection.ExecuteAsync(cmd);
    }

    public async Task ResetearIntentosFallidosSpAsync(int usuarioId, CancellationToken ct = default)
    {
        using var connection = CreateConnection();
        var parameters = new DynamicParameters();
        parameters.Add("@UsuarioId", usuarioId, DbType.Int32);

        var cmd = new CommandDefinition(
            commandText: "sp_Usuario_ResetearIntentosFallidos",
            parameters: parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct
        );

        await connection.ExecuteAsync(cmd);
    }

    public async Task RegistrarAuditoriaSpAsync(RegistroAuditoria auditoria, CancellationToken ct = default)
    {
        using var connection = CreateConnection();
        var parameters = new DynamicParameters();
        parameters.Add("@UsuarioId", auditoria.UsuarioId, DbType.Int32);
        parameters.Add("@Accion", auditoria.Accion, DbType.String, size: 50);
        parameters.Add("@Detalle", auditoria.Detalle, DbType.String, size: 500);
        parameters.Add("@DireccionIP", auditoria.DireccionIP, DbType.String, size: 50);

        var cmd = new CommandDefinition(
            commandText: "sp_Auditoria_Registrar",
            parameters: parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct
        );

        await connection.ExecuteAsync(cmd);
    }
}
