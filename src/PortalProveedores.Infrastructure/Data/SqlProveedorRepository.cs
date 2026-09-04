using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using PortalProveedores.Application.DTOs;
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

    public async Task<ResultadoCrearProveedorDto> CrearProveedorCompletoAsync(
        CrearProveedorDto dto,
        string passwordHash,
        int adminUsuarioId,
        string direccionIp,
        CancellationToken ct = default)
    {
        using var connection = CreateConnection();
        if (connection.State != ConnectionState.Open)
        {
            if (connection is SqlConnection sqlConn)
            {
                await sqlConn.OpenAsync(ct);
            }
            else
            {
                connection.Open();
            }
        }

        using var transaction = connection.BeginTransaction();
        try
        {
            // 1. Detectar si la tabla es Cat_Proveedores (PortalProveedores_DB) o Proveedores (PortalProveedoresDB)
            bool esCatProveedores = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM sys.tables WHERE name = 'Cat_Proveedores';",
                transaction: transaction) > 0;

            int proveedorId = 0;
            int usuarioId = 0;

            if (esCatProveedores)
            {
                // Verificar duplicados de CodigoProveedor y RFC
                var existeCodigo = await connection.ExecuteScalarAsync<int>(
                    "SELECT COUNT(1) FROM dbo.Cat_Proveedores WHERE CodigoProveedor = @CodigoProveedor;",
                    new { dto.CodigoProveedor },
                    transaction: transaction) > 0;

                if (existeCodigo)
                {
                    return new ResultadoCrearProveedorDto
                    {
                        Exitoso = false,
                        Mensaje = $"El código de proveedor '{dto.CodigoProveedor}' ya se encuentra registrado."
                    };
                }

                var existeRfc = await connection.ExecuteScalarAsync<int>(
                    "SELECT COUNT(1) FROM dbo.Cat_Proveedores WHERE RFC = @RFC;",
                    new { dto.RFC },
                    transaction: transaction) > 0;

                if (existeRfc)
                {
                    return new ResultadoCrearProveedorDto
                    {
                        Exitoso = false,
                        Mensaje = $"El RFC '{dto.RFC}' ya está registrado en el catálogo de proveedores."
                    };
                }

                var existeEmail = await connection.ExecuteScalarAsync<int>(
                    "SELECT COUNT(1) FROM dbo.Usuarios_Proveedor WHERE Email = @Email;",
                    new { dto.Email },
                    transaction: transaction) > 0;

                if (existeEmail)
                {
                    return new ResultadoCrearProveedorDto
                    {
                        Exitoso = false,
                        Mensaje = $"El correo electrónico '{dto.Email}' ya está asignado a otro usuario."
                    };
                }

                // Insertar en Cat_Proveedores
                const string sqlCat = @"
                    INSERT INTO dbo.Cat_Proveedores (
                        CodigoProveedor, RFC, RazonSocial, CondicionesPago,
                        RequiereValidarCompra, OrdenCompraObligatoria, EsProveedorNacional, Activo,
                        CreatedAt, UpdatedAt
                    )
                    VALUES (
                        @CodigoProveedor, @RFC, @RazonSocial, @CondicionesPago,
                        @RequiereValidarCompra, @OrdenCompraObligatoria, @EsProveedorNacional, @Activo,
                        SYSUTCDATETIME(), SYSUTCDATETIME()
                    );
                    SELECT CAST(SCOPE_IDENTITY() AS INT);";

                proveedorId = await connection.ExecuteScalarAsync<int>(
                    sqlCat,
                    new
                    {
                        dto.CodigoProveedor,
                        dto.RFC,
                        dto.RazonSocial,
                        dto.CondicionesPago,
                        dto.RequiereValidarCompra,
                        dto.OrdenCompraObligatoria,
                        dto.EsProveedorNacional,
                        dto.Activo
                    },
                    transaction: transaction);

                // Insertar en Usuarios_Proveedor
                const string sqlUserProv = @"
                    INSERT INTO dbo.Usuarios_Proveedor (
                        ProveedorId, RFC, Email, PasswordHash, IntentosFallidos,
                        BloqueadoHasta, Activo, CreatedAt, UpdatedAt
                    )
                    VALUES (
                        @ProveedorId, @RFC, @Email, @PasswordHash, 0,
                        NULL, @Activo, SYSUTCDATETIME(), SYSUTCDATETIME()
                    );
                    SELECT CAST(SCOPE_IDENTITY() AS INT);";

                usuarioId = await connection.ExecuteScalarAsync<int>(
                    sqlUserProv,
                    new
                    {
                        ProveedorId = proveedorId,
                        dto.RFC,
                        dto.Email,
                        PasswordHash = passwordHash,
                        dto.Activo
                    },
                    transaction: transaction);

                // Auditoría si existe tabla
                var existeAuditoria = await connection.ExecuteScalarAsync<int>(
                    "SELECT COUNT(1) FROM sys.tables WHERE name = 'Auditoria_Eventos';",
                    transaction: transaction) > 0;

                if (existeAuditoria)
                {
                    const string sqlAudit = @"
                        INSERT INTO dbo.Auditoria_Eventos (UsuarioId, Modulo, Accion, Detalle, DireccionIP, CreatedAt)
                        VALUES (@UsuarioId, 'ADMIN_PROVEEDORES', 'CREAR_PROVEEDOR', @Detalle, @DireccionIP, SYSUTCDATETIME());";

                    await connection.ExecuteAsync(
                        sqlAudit,
                        new
                        {
                            UsuarioId = adminUsuarioId,
                            Detalle = $"Proveedor registrado: {dto.RazonSocial} (RFC: {dto.RFC}, Código: {dto.CodigoProveedor})",
                            DireccionIP = direccionIp
                        },
                        transaction: transaction);
                }
            }
            else
            {
                // Esquema alternativo Proveedores / Usuarios
                var existeRfc = await connection.ExecuteScalarAsync<int>(
                    "SELECT COUNT(1) FROM dbo.Proveedores WHERE RFC = @RFC;",
                    new { dto.RFC },
                    transaction: transaction) > 0;

                if (existeRfc)
                {
                    return new ResultadoCrearProveedorDto
                    {
                        Exitoso = false,
                        Mensaje = $"El RFC '{dto.RFC}' ya está registrado en el catálogo."
                    };
                }

                const string sqlProv = @"
                    INSERT INTO dbo.Proveedores (RFC, RazonSocial, EmailContacto, Telefono, Activo, FechaRegistro)
                    VALUES (@RFC, @RazonSocial, @Email, @Telefono, @Activo, SYSUTCDATETIME());
                    SELECT CAST(SCOPE_IDENTITY() AS INT);";

                proveedorId = await connection.ExecuteScalarAsync<int>(
                    sqlProv,
                    new
                    {
                        dto.RFC,
                        dto.RazonSocial,
                        dto.Email,
                        dto.Telefono,
                        dto.Activo
                    },
                    transaction: transaction);

                const string sqlUser = @"
                    INSERT INTO dbo.Usuarios (Username, Email, PasswordHash, Salt, Rol, ProveedorId, Activo, IntentosFallidosLogin, FechaCreacion)
                    VALUES (@Username, @Email, @PasswordHash, '', 1, @ProveedorId, @Activo, 0, SYSUTCDATETIME());
                    SELECT CAST(SCOPE_IDENTITY() AS INT);";

                usuarioId = await connection.ExecuteScalarAsync<int>(
                    sqlUser,
                    new
                    {
                        Username = dto.RFC,
                        dto.Email,
                        PasswordHash = passwordHash,
                        ProveedorId = proveedorId,
                        dto.Activo
                    },
                    transaction: transaction);
            }

            transaction.Commit();

            return new ResultadoCrearProveedorDto
            {
                Exitoso = true,
                Mensaje = $"Proveedor '{dto.RazonSocial}' dado de alta exitosamente con código {dto.CodigoProveedor}.",
                ProveedorId = proveedorId,
                UsuarioId = usuarioId,
                CodigoProveedor = dto.CodigoProveedor,
                RFC = dto.RFC
            };
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            return new ResultadoCrearProveedorDto
            {
                Exitoso = false,
                Mensaje = $"Error al registrar proveedor: {ex.Message}"
            };
        }
    }
}
