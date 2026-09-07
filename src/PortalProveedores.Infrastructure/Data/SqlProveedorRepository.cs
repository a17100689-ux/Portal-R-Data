using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using PortalProveedores.Application.DTOs;
using PortalProveedores.Application.Interfaces;
using PortalProveedores.Core.Common;
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
        var parameters = new DynamicParameters();
        parameters.Add("@ProveedorId", id, DbType.Int32);

        var cmd = new CommandDefinition(
            commandText: "dbo.sp_Portal_Proveedor_ObtenerPorId",
            parameters: parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct
        );

        return await connection.QueryFirstOrDefaultAsync<Proveedor>(cmd);
    }

    public async Task<Proveedor?> ObtenerPorRfcSpAsync(string rfc, CancellationToken ct = default)
    {
        using var connection = CreateConnection();
        var parameters = new DynamicParameters();
        parameters.Add("@RFC", rfc, DbType.String, size: 15);

        var cmd = new CommandDefinition(
            commandText: "dbo.sp_Portal_Proveedor_ObtenerPorRfc",
            parameters: parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct
        );

        return await connection.QueryFirstOrDefaultAsync<Proveedor>(cmd);
    }

    public async Task<Proveedor?> ObtenerPorCodigoSpAsync(string codigoProveedor, CancellationToken ct = default)
    {
        using var connection = CreateConnection();
        var parameters = new DynamicParameters();
        parameters.Add("@CodigoProveedor", codigoProveedor, DbType.String, size: 15);

        var cmd = new CommandDefinition(
            commandText: "dbo.sp_Portal_Proveedor_ObtenerPorCodigo",
            parameters: parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct
        );

        return await connection.QueryFirstOrDefaultAsync<Proveedor>(cmd);
    }

    /// <summary>
    /// Consulta el catálogo oficial Cat_Proveedores mediante el Stored Procedure dbo.sp_Portal_Proveedor_VerificarEnCatalogo.
    /// Valida que el proveedor exista previamente en el ERP central antes de permitir el registro en el portal.
    /// </summary>
    public async Task<VerificarProveedorCatalogoDto> VerificarEnCatalogoSpAsync(
        string? rfc,
        string? codigoProveedor,
        CancellationToken ct = default)
    {
        using var connection = CreateConnection();
        var parameters = new DynamicParameters();
        parameters.Add("@RFC", rfc, DbType.String, size: 15);
        parameters.Add("@CodigoProveedor", codigoProveedor, DbType.String, size: 15);

        var cmd = new CommandDefinition(
            commandText: "dbo.sp_Portal_Proveedor_VerificarEnCatalogo",
            parameters: parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct
        );

        var resultadoSp = await connection.QueryFirstOrDefaultAsync<VerificarProveedorCatalogoDto>(cmd);
        return resultadoSp ?? new VerificarProveedorCatalogoDto
        {
            EnCatalogo = false,
            CodigoProveedor = codigoProveedor,
            RFC = rfc,
            Mensaje = "El proveedor no se encuentra en el Catálogo de Proveedores de Radial Llantas (Cat_Proveedores)."
        };
    }

    /// <summary>
    /// Consulta paginada y búsqueda sargable de proveedores mediante el Stored Procedure dbo.sp_Portal_Proveedor_BuscarEnCatalogo.
    /// </summary>
    public async Task<PaginatedResult<ProveedorCatalogoItemDto>> BuscarEnCatalogoSpAsync(
        string? termino,
        int pagina = 1,
        int tamanoPagina = 20,
        CancellationToken ct = default)
    {
        using var connection = CreateConnection();
        pagina = pagina < 1 ? 1 : pagina;
        tamanoPagina = tamanoPagina switch { < 1 => 20, > 100 => 100, _ => tamanoPagina };

        var parameters = new DynamicParameters();
        parameters.Add("@Termino", termino, DbType.String, size: 100);
        parameters.Add("@Pagina", pagina, DbType.Int32);
        parameters.Add("@TamanoPagina", tamanoPagina, DbType.Int32);

        var cmd = new CommandDefinition(
            commandText: "dbo.sp_Portal_Proveedor_BuscarEnCatalogo",
            parameters: parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct
        );

        using var multi = await connection.QueryMultipleAsync(cmd);
        int totalRegistros = await multi.ReadFirstOrDefaultAsync<int>();
        var items = (await multi.ReadAsync<ProveedorCatalogoItemDto>()).ToList();

        return new PaginatedResult<ProveedorCatalogoItemDto>(items, totalRegistros, pagina, tamanoPagina);
    }

    /// <summary>
    /// Registra el usuario de acceso al portal para un proveedor que YA EXISTE en Cat_Proveedores.
    /// REGLA ESTRICTA: No se inserta un nuevo registro en Cat_Proveedores. Si el proveedor no existe, se rechaza.
    /// </summary>
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

        // 1. REGLA FUNDAMENTAL: Verificar existencia previa en Cat_Proveedores
        var verificacion = await VerificarEnCatalogoSpAsync(dto.RFC, dto.CodigoProveedor, ct);
        if (!verificacion.EnCatalogo || !verificacion.ProveedorId.HasValue)
        {
            return new ResultadoCrearProveedorDto
            {
                Exitoso = false,
                Mensaje = $"No se puede registrar el proveedor: El RFC '{dto.RFC}' o Código '{dto.CodigoProveedor}' no se encuentra en el Catálogo Oficial de Proveedores de Radial Llantas (Cat_Proveedores)."
            };
        }

        if (!verificacion.Activo)
        {
            return new ResultadoCrearProveedorDto
            {
                Exitoso = false,
                Mensaje = $"El proveedor '{verificacion.RazonSocial}' existe en el catálogo pero se encuentra inactivo."
            };
        }

        if (verificacion.TieneUsuarioRegistrado)
        {
            return new ResultadoCrearProveedorDto
            {
                Exitoso = false,
                Mensaje = $"El proveedor '{verificacion.RazonSocial}' ya cuenta con una cuenta de usuario en el portal ({verificacion.EmailRegistrado})."
            };
        }

        int proveedorId = verificacion.ProveedorId.Value;
        string codigoProveedorOficial = verificacion.CodigoProveedor ?? dto.CodigoProveedor;
        string rfcOficial = verificacion.RFC ?? dto.RFC;

        // 2. Intentar inserción mediante el Stored Procedure oficial sp_Portal_Admin_CrearUsuarioProveedor
        try
        {
            var spParams = new DynamicParameters();
            spParams.Add("@AdminUsuarioId", adminUsuarioId, DbType.Int32);
            spParams.Add("@CodigoProveedor", codigoProveedorOficial, DbType.String, size: 15);
            spParams.Add("@RFC", rfcOficial, DbType.String, size: 15);
            spParams.Add("@Email", dto.Email, DbType.String, size: 120);
            spParams.Add("@PasswordHash", passwordHash, DbType.String, size: 255);
            spParams.Add("@DireccionIP", direccionIp, DbType.String, size: 45);
            spParams.Add("@CodigoPostal", dto.CodigoPostal, DbType.String, size: 10);
            spParams.Add("@Telefono", dto.Telefono, DbType.String, size: 50);
            spParams.Add("@EmailContacto", dto.Email, DbType.String, size: 150);
            spParams.Add("@RegimenFiscal", dto.RegimenFiscal, DbType.String, size: 10);
            spParams.Add("@RequiereValidarCompra", dto.RequiereValidarCompra, DbType.Boolean);
            spParams.Add("@OrdenCompraObligatoria", dto.OrdenCompraObligatoria, DbType.Boolean);
            spParams.Add("@EsProveedorNacional", dto.EsProveedorNacional, DbType.Boolean);
            spParams.Add("@CondicionesPago", dto.CondicionesPago, DbType.String, size: 50);
            spParams.Add("@NuevoUsuarioId", dbType: DbType.Int32, direction: ParameterDirection.Output);

            var spCmd = new CommandDefinition(
                commandText: "dbo.sp_Portal_Admin_CrearUsuarioProveedor",
                parameters: spParams,
                commandType: CommandType.StoredProcedure,
                cancellationToken: ct
            );

            await connection.ExecuteAsync(spCmd);
            int nuevoUsuarioId = spParams.Get<int>("@NuevoUsuarioId");

            return new ResultadoCrearProveedorDto
            {
                Exitoso = true,
                Mensaje = $"Proveedor '{verificacion.RazonSocial}' habilitado exitosamente en el portal.",
                ProveedorId = proveedorId,
                UsuarioId = nuevoUsuarioId,
                CodigoProveedor = codigoProveedorOficial,
                RFC = rfcOficial
            };
        }
        catch (SqlException ex)
        {
            return new ResultadoCrearProveedorDto
            {
                Exitoso = false,
                Mensaje = ex.Message
            };
        }
        catch (Exception ex)
        {
            return new ResultadoCrearProveedorDto
            {
                Exitoso = false,
                Mensaje = $"Error al registrar usuario de proveedor: {ex.Message}"
            };
        }
    }
}
