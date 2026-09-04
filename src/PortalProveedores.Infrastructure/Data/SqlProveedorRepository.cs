using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using PortalProveedores.Application.Interfaces;
using PortalProveedores.Core.Entities;

namespace PortalProveedores.Infrastructure.Data;

public class SqlProveedorRepository : IProveedorRepository
{
    private readonly string _connectionString;

    public SqlProveedorRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Cadena de conexión 'DefaultConnection' no encontrada.");
    }

    private IDbConnection CreateConnection() => new SqlConnection(_connectionString);

    public async Task<Proveedor?> ObtenerPorIdSpAsync(int id, CancellationToken ct = default)
    {
        using var connection = CreateConnection();
        try
        {
            var parameters = new DynamicParameters();
            parameters.Add("@Id", id, DbType.Int32);

            var cmd = new CommandDefinition(
                commandText: "sp_Proveedor_ObtenerPorId",
                parameters: parameters,
                commandType: CommandType.StoredProcedure,
                cancellationToken: ct
            );

            return await connection.QueryFirstOrDefaultAsync<Proveedor>(cmd);
        }
        catch
        {
            return null;
        }
    }

    public async Task<Proveedor?> ObtenerPorRfcSpAsync(string rfc, CancellationToken ct = default)
    {
        using var connection = CreateConnection();

        // 1. Intentar mediante el procedimiento oficial sp_Portal_Usuario_ObtenerPorLogin
        try
        {
            var parameters = new DynamicParameters();
            parameters.Add("@Identificador", rfc, DbType.String, size: 120);

            var cmd = new CommandDefinition(
                commandText: "dbo.sp_Portal_Usuario_ObtenerPorLogin",
                parameters: parameters,
                commandType: CommandType.StoredProcedure,
                cancellationToken: ct
            );

            var usuario = await connection.QueryFirstOrDefaultAsync<UsuarioProveedor>(cmd);
            if (usuario != null)
            {
                return new Proveedor
                {
                    ProveedorId = usuario.ProveedorId,
                    CodigoProveedor = usuario.CodigoProveedor,
                    RFC = usuario.RFC,
                    RazonSocial = usuario.RazonSocial,
                    CondicionesPago = usuario.CondicionesPago,
                    RequiereValidarCompra = usuario.RequiereValidarCompra,
                    OrdenCompraObligatoria = usuario.OrdenCompraObligatoria,
                    Activo = usuario.ProveedorActivo
                };
            }
        }
        catch
        {
            // Continuar al procedimiento alternativo si existe
        }

        // 2. Procedimiento alternativo por RFC
        try
        {
            var p = new DynamicParameters();
            p.Add("@RFC", rfc, DbType.String, size: 15);
            var cmd = new CommandDefinition("sp_Proveedor_ObtenerPorRfc", p, commandType: CommandType.StoredProcedure, cancellationToken: ct);
            return await connection.QueryFirstOrDefaultAsync<Proveedor>(cmd);
        }
        catch
        {
            return null;
        }
    }

    public async Task<Proveedor?> ObtenerPorCodigoSpAsync(string codigoProveedor, CancellationToken ct = default)
    {
        using var connection = CreateConnection();
        try
        {
            var parameters = new DynamicParameters();
            parameters.Add("@CodigoProveedor", codigoProveedor, DbType.String, size: 15);

            var cmd = new CommandDefinition(
                commandText: "sp_Proveedor_ObtenerPorCodigo",
                parameters: parameters,
                commandType: CommandType.StoredProcedure,
                cancellationToken: ct
            );

            return await connection.QueryFirstOrDefaultAsync<Proveedor>(cmd);
        }
        catch
        {
            return null;
        }
    }
}
