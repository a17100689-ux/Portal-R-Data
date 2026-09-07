using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortalProveedores.Application.DTOs;
using PortalProveedores.Application.Interfaces;
using PortalProveedores.Core.Common;
using PortalProveedores.Core.Entities;

namespace PortalProveedores.Web.Controllers.Api;

/// <summary>
/// Endpoints REST para la verificación en catálogo oficial y registro de cuentas de proveedores.
/// </summary>
[Route("api/v1/proveedores")]
public class ProveedoresApiController : BaseApiController
{
    private readonly IProveedorService _proveedorService;
    private readonly ISyncService _syncService;
    private readonly ILogger<ProveedoresApiController> _logger;

    public ProveedoresApiController(
        IProveedorService proveedorService,
        ISyncService syncService,
        ILogger<ProveedoresApiController> logger)
    {
        _proveedorService = proveedorService;
        _syncService = syncService;
        _logger = logger;
    }

    /// <summary>
    /// Sincroniza manualmente el catálogo de proveedores, sucursales y artículos desde la base central Punto_de_Venta.
    /// </summary>
    [HttpPost("catalogo/sincronizar")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<ResultadoSyncCatalogosDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ResultadoSyncCatalogosDto>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<ResultadoSyncCatalogosDto>>> SincronizarCatalogo(CancellationToken ct)
    {
        _logger.LogInformation("Solicitud manual de sincronización de catálogo recibida desde el frontend.");
        var resultado = await _syncService.RefrescarCatalogosAsync(ct);
        if (!resultado.Exitoso)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<ResultadoSyncCatalogosDto>.Fail(resultado.Mensaje, statusCode: 500));
        }

        return Ok(ApiResponse<ResultadoSyncCatalogosDto>.Ok(resultado, resultado.Mensaje));
    }

    /// <summary>
    /// Verifica si un RFC o Código de Proveedor existe en el Catálogo oficial de Radial Llantas (Cat_Proveedores)
    /// y comprueba si ya cuenta con un usuario registrado en el portal.
    /// </summary>
    /// <param name="rfc">RFC del proveedor a validar (12 o 13 caracteres).</param>
    /// <param name="codigoProveedor">Código interno ERP asignado al proveedor.</param>
    /// <param name="ct">Token de cancelación.</param>
    [HttpGet("catalogo/verificar")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<VerificarProveedorCatalogoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<VerificarProveedorCatalogoDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<VerificarProveedorCatalogoDto>>> VerificarEnCatalogo(
        [FromQuery] string? rfc,
        [FromQuery] string? codigoProveedor,
        CancellationToken ct)
    {
        var response = await _proveedorService.VerificarEnCatalogoAsync(rfc, codigoProveedor, ct);
        if (!response.Success)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }

    /// <summary>
    /// Buscador de proveedores en el catálogo oficial de Radial Llantas (Cat_Proveedores).
    /// Permite filtrar por Código ERP, RFC o Razón Social con paginación server-side.
    /// Si no arroja resultados locales, sincroniza automáticamente con el ERP central y reintenta.
    /// </summary>
    /// <param name="termino">Término de búsqueda (SKU/Código ERP, RFC o nombre de la empresa).</param>
    /// <param name="pagina">Número de página (por defecto 1).</param>
    /// <param name="tamanoPagina">Tamaño de página (por defecto 20, máximo 100).</param>
    /// <param name="ct">Token de cancelación.</param>
    [HttpGet("catalogo/buscar")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<PaginatedResult<ProveedorCatalogoItemDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PaginatedResult<ProveedorCatalogoItemDto>>>> BuscarEnCatalogo(
        [FromQuery] string? termino,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanoPagina = 20,
        CancellationToken ct = default)
    {
        var response = await _proveedorService.BuscarEnCatalogoAsync(termino, pagina, tamanoPagina, ct);
        return Ok(response);
    }

    /// <summary>
    /// Registra y habilita el usuario de acceso al portal para un proveedor que YA EXISTE en Cat_Proveedores.
    /// REGLA ESTRICTA: La solicitud será rechazada si el proveedor no está previamente registrado en Cat_Proveedores.
    /// </summary>
    /// <param name="model">Datos del formulario de registro y credenciales solicitadas.</param>
    /// <param name="ct">Token de cancelación.</param>
    [HttpPost("registro")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<ResultadoCrearProveedorDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<ResultadoCrearProveedorDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<ResultadoCrearProveedorDto>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<ResultadoCrearProveedorDto>>> RegistrarProveedor(
        [FromBody] RegistroProveedorRequestDto model,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            var errores = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return BadRequestResponse<ResultadoCrearProveedorDto>("Datos de registro incompletos o con formato inválido.", errores);
        }

        int adminId = ObtenerUsuarioId() ?? 1;
        string ipAddress = ObtenerIpAddress();

        var dto = new CrearProveedorDto
        {
            CodigoProveedor = model.CodigoProveedor,
            RFC = model.RFC,
            RazonSocial = model.RazonSocial ?? string.Empty,
            Email = model.Email,
            Password = model.Password,
            Telefono = model.Telefono,
            RegimenFiscal = model.RegimenFiscal,
            CodigoPostal = model.CodigoPostal,
            CondicionesPago = model.CondicionesPago ?? "30",
            RequiereValidarCompra = model.RequiereValidarCompra,
            OrdenCompraObligatoria = model.OrdenCompraObligatoria,
            EsProveedorNacional = model.EsProveedorNacional,
            Activo = true
        };

        var resultado = await _proveedorService.RegistrarProveedorAsync(dto, adminId, ipAddress, ct);

        return resultado.StatusCode switch
        {
            StatusCodes.Status201Created => StatusCode(StatusCodes.Status201Created, resultado),
            StatusCodes.Status400BadRequest => BadRequest(resultado),
            StatusCodes.Status409Conflict => StatusCode(StatusCodes.Status409Conflict, resultado),
            _ => StatusCode(resultado.StatusCode, resultado)
        };
    }

    /// <summary>
    /// Consulta el perfil del proveedor por su ID.
    /// Aplica validación de pertenencia e IDOR prevention para proveedores autenticados.
    /// </summary>
    /// <param name="id">Identificador del proveedor.</param>
    /// <param name="ct">Token de cancelación.</param>
    [HttpGet("{id:int}")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<Proveedor>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<Proveedor>>> ObtenerPorId(int id, CancellationToken ct)
    {
        string rol = ObtenerRol();
        int? proveedorIdClaim = ObtenerProveedorId();

        if (string.Equals(rol, "Proveedor", StringComparison.OrdinalIgnoreCase))
        {
            if (!proveedorIdClaim.HasValue || proveedorIdClaim.Value != id)
            {
                return ForbiddenResponse<Proveedor>("No tiene permisos para consultar información de otro proveedor.");
            }
        }

        var proveedor = await _proveedorService.ObtenerPorIdAsync(id, ct);
        if (proveedor == null)
        {
            return NotFoundResponse<Proveedor>($"El proveedor con ID {id} no fue encontrado.");
        }

        return OkResponse(proveedor, "Proveedor consultado correctamente.");
    }
}
