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

    public async Task<UsuarioAdministrador?> ObtenerAdminPorLoginSpAsync(string identificador, CancellationToken ct = default)
    {
        using var connection = CreateConnection();
        var parameters = new DynamicParameters();
        parameters.Add("@Identificador", identificador, DbType.String, size: 120);

        var cmd = new CommandDefinition(
            commandText: "dbo.sp_Portal_Admin_Login",
            parameters: parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct
        );

        return await connection.QueryFirstOrDefaultAsync<UsuarioAdministrador>(cmd);
    }

    public async Task<UsuarioProveedor?> ObtenerProveedorPorLoginSpAsync(string identificador, CancellationToken ct = default)
    {
        using var connection = CreateConnection();
        var parameters = new DynamicParameters();
        parameters.Add("@Identificador", identificador, DbType.String, size: 120);

        var cmd = new CommandDefinition(
            commandText: "dbo.sp_Portal_Usuario_ObtenerPorLogin",
            parameters: parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct
        );

        return await connection.QueryFirstOrDefaultAsync<UsuarioProveedor>(cmd);
    }

    public async Task RegistrarIntentoFallidoSpAsync(int usuarioId, CancellationToken ct = default)
    {
        using var connection = CreateConnection();
        var parameters = new DynamicParameters();
        parameters.Add("@UsuarioId", usuarioId, DbType.Int32);
        parameters.Add("@MaxIntentos", (byte)5, DbType.Byte);
        parameters.Add("@MinutosBloqueo", 15, DbType.Int32);

        var cmd = new CommandDefinition(
            commandText: "dbo.sp_Portal_Usuario_RegistrarIntentoFallido",
            parameters: parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct
        );

        await connection.ExecuteAsync(cmd);
    }

    public async Task RegistrarLoginExitosoSpAsync(int usuarioId, string direccionIp, CancellationToken ct = default)
    {
        using var connection = CreateConnection();
        var parameters = new DynamicParameters();
        parameters.Add("@UsuarioId", usuarioId, DbType.Int32);
        parameters.Add("@DireccionIP", direccionIp, DbType.String, size: 45);

        var cmd = new CommandDefinition(
            commandText: "dbo.sp_Portal_Usuario_RegistrarLoginExitoso",
            parameters: parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct
        );

        await connection.ExecuteAsync(cmd);
    }

    public async Task RegistrarAuditoriaSpAsync(int? usuarioId, string modulo, string accion, string detalle, string direccionIp, CancellationToken ct = default)
    {
        using var connection = CreateConnection();
        var parameters = new DynamicParameters();
        parameters.Add("@UsuarioId", usuarioId, DbType.Int32);
        parameters.Add("@Modulo", modulo, DbType.String, size: 50);
        parameters.Add("@Accion", accion, DbType.String, size: 50);
        parameters.Add("@Detalle", detalle, DbType.String, size: 500);
        parameters.Add("@DireccionIP", direccionIp, DbType.String, size: 45);

        var cmd = new CommandDefinition(
            commandText: "dbo.sp_Portal_Auditoria_Registrar",
            parameters: parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct
        );

        await connection.ExecuteAsync(cmd);
    }

    // Métodos de compatibilidad con interfaz previa
    public async Task<Usuario?> ObtenerPorUsernameSpAsync(string username, CancellationToken ct = default)
    {
        var admin = await ObtenerAdminPorLoginSpAsync(username, ct);
        if (admin != null)
        {
            return new Usuario
            {
                Id = admin.AdminId,
                Username = admin.Email,
                Email = admin.Email,
                PasswordHash = admin.PasswordHash,
                Rol = RolUsuario.Administrador,
                Activo = admin.Activo
            };
        }

        var prov = await ObtenerProveedorPorLoginSpAsync(username, ct);
        if (prov != null)
        {
            return new Usuario
            {
                Id = prov.UsuarioId,
                ProveedorId = prov.ProveedorId,
                CodigoProveedor = prov.CodigoProveedor,
                Username = prov.Email,
                Email = prov.Email,
                PasswordHash = prov.PasswordHash,
                Rol = RolUsuario.Proveedor,
                Activo = prov.UsuarioActivo,
                IntentosFallidosLogin = prov.IntentosFallidos,
                BloqueadoHasta = prov.BloqueadoHasta
            };
        }

        return null;
    }

    public async Task<Usuario?> ObtenerPorIdSpAsync(int id, CancellationToken ct = default)
    {
        return await ObtenerPorUsernameSpAsync(id.ToString(), ct);
    }

    public async Task ResetearIntentosFallidosSpAsync(int usuarioId, CancellationToken ct = default)
    {
        await RegistrarLoginExitosoSpAsync(usuarioId, "127.0.0.1", ct);
    }

    public async Task RegistrarAuditoriaSpAsync(RegistroAuditoria auditoria, CancellationToken ct = default)
    {
        await RegistrarAuditoriaSpAsync(auditoria.UsuarioId, "SEGURIDAD", auditoria.Accion, auditoria.Detalle, auditoria.DireccionIP, ct);
    }
}
