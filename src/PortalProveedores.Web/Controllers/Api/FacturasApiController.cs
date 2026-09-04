using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortalProveedores.Application.DTOs;
using PortalProveedores.Application.Interfaces;
using PortalProveedores.Core.Common;
using PortalProveedores.Web.Models;

namespace PortalProveedores.Web.Controllers.Api;

/// <summary>
/// Endpoints REST para la recepción, consulta paginada y gestión de facturas de proveedores.
/// </summary>
[Authorize]
[Route("api/v1/facturas")]
public class FacturasApiController : BaseApiController
{
    private readonly IFacturaService _facturaService;
    private readonly IStorageService _storageService;
    private readonly ILogger<FacturasApiController> _logger;

    public FacturasApiController(
        IFacturaService facturaService,
        IStorageService storageService,
        ILogger<FacturasApiController> logger)
    {
        _facturaService = facturaService;
        _storageService = storageService;
        _logger = logger;
    }

    /// <summary>
    /// Consulta paginada y filtrada de facturas.
    /// Para usuarios con rol Proveedor, restringe automáticamente los resultados a sus propias facturas.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PaginatedResult<FacturaResumenDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<PaginatedResult<FacturaResumenDto>>>> ListarPaginado(
        [FromQuery] FacturaFiltroDto filtro,
        CancellationToken ct)
    {
        string rol = ObtenerRol();
        int? proveedorId = string.Equals(rol, "Proveedor", StringComparison.OrdinalIgnoreCase)
            ? ObtenerProveedorId()
            : null;

        var resultado = await _facturaService.ListarFacturasPaginadasAsync(filtro, proveedorId, ct);
        return OkResponse(resultado, "Facturas consultadas correctamente.");
    }

    /// <summary>
    /// Obtiene el detalle fiscal de una factura por su ID con protección contra IDOR.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<FacturaDetalleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<FacturaDetalleDto>>> ObtenerDetalle(int id, CancellationToken ct)
    {
        string rol = ObtenerRol();
        int? proveedorId = string.Equals(rol, "Proveedor", StringComparison.OrdinalIgnoreCase)
            ? ObtenerProveedorId()
            : null;

        var factura = await _facturaService.ObtenerDetalleAsync(id, proveedorId, ct);
        if (factura == null)
        {
            return NotFoundResponse<FacturaDetalleDto>("La factura solicitada no existe o no tiene permisos suficientes para consultarla.");
        }

        return OkResponse(factura);
    }

    /// <summary>
    /// Carga, valida y orquesta el registro de una nueva factura (XML CFDI y PDF).
    /// Aplica validación estricta de Magic Numbers y límite de 5 MB.
    /// </summary>
    [HttpPost("cargar")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(5 * 1024 * 1024)] // Límite estricto de 5 MB
    [ProducesResponseType(typeof(ApiResponse<ResultadoCargaFactura>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<ResultadoCargaFactura>>> CargarFactura(
        [FromForm] CargaFacturaViewModel model,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            var errores = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return BadRequestResponse<ResultadoCargaFactura>("Datos de formulario incompletos o inválidos.", errores);
        }

        if (model.ArchivoXml == null || model.ArchivoPdf == null)
        {
            return BadRequestResponse<ResultadoCargaFactura>("Debe adjuntar obligatoriamente ambos archivos: Comprobante XML CFDI y representación PDF.");
        }

        int? proveedorIdClaim = ObtenerProveedorId();
        if (!proveedorIdClaim.HasValue)
        {
            return BadRequestResponse<ResultadoCargaFactura>("No se encontró una cuenta de proveedor asociada a la sesión activa.");
        }

        using var xmlStream = model.ArchivoXml.OpenReadStream();
        using var pdfStream = model.ArchivoPdf.OpenReadStream();

        var dto = new CargaFacturaDto
        {
            ProveedorId = proveedorIdClaim.Value,
            XmlStream = xmlStream,
            XmlNombreOriginal = model.ArchivoXml.FileName,
            XmlTamano = model.ArchivoXml.Length,
            PdfStream = pdfStream,
            PdfNombreOriginal = model.ArchivoPdf.FileName,
            PdfTamano = model.ArchivoPdf.Length,
            Observaciones = model.Observaciones
        };

        var resultado = await _facturaService.CargarFacturaAsync(dto, ct);
        if (!resultado.Exitoso)
        {
            return BadRequestResponse<ResultadoCargaFactura>(resultado.Mensaje);
        }

        return CreatedResponse(
            nameof(ObtenerDetalle),
            new { id = resultado.FacturaId },
            resultado,
            "Factura procesada y validada exitosamente."
        );
    }

    /// <summary>
    /// Actualiza el estatus de revisión o pago de una factura.
    /// Exclusivo para usuarios con roles de Revisor o Administrador.
    /// </summary>
    [HttpPatch("{id:int}/estatus")]
    [Authorize(Roles = "Revisor,Administrador")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<bool>>> CambiarEstatus(
        int id,
        [FromBody] ActualizarEstatusFacturaDto dto,
        CancellationToken ct)
    {
        int usuarioId = ObtenerUsuarioId() ?? 0;
        string rol = ObtenerRol();
        string ipAddress = ObtenerIpAddress();

        var resultado = await _facturaService.CambiarEstatusFacturaAsync(id, dto, usuarioId, rol, ipAddress, ct);

        return resultado.StatusCode switch
        {
            StatusCodes.Status200OK => Ok(resultado),
            StatusCodes.Status400BadRequest => BadRequest(resultado),
            StatusCodes.Status403Forbidden => StatusCode(StatusCodes.Status403Forbidden, resultado),
            StatusCodes.Status404NotFound => NotFound(resultado),
            _ => StatusCode(resultado.StatusCode, resultado)
        };
    }

    /// <summary>
    /// Descarga segura del archivo XML CFDI con control de acceso por proveedor.
    /// </summary>
    [HttpGet("{id:int}/descargar/xml")]
    public async Task<IActionResult> DescargarXml(int id, CancellationToken ct)
    {
        string rol = ObtenerRol();
        int? proveedorId = string.Equals(rol, "Proveedor", StringComparison.OrdinalIgnoreCase)
            ? ObtenerProveedorId()
            : null;

        var factura = await _facturaService.ObtenerEntidadDetalleAsync(id, proveedorId, ct);
        if (factura == null)
        {
            return NotFoundResponse<object>("La factura solicitada no existe o no tiene permisos para descargar sus archivos.");
        }

        string subCarpeta = Path.GetDirectoryName(factura.ArchivoXmlNombreInterno) ?? string.Empty;
        string nombreArchivo = Path.GetFileName(factura.ArchivoXmlNombreInterno);

        var stream = await _storageService.ObtenerArchivoStreamAsync(nombreArchivo, subCarpeta, ct);
        if (stream == null)
        {
            return NotFoundResponse<object>("El archivo XML no fue encontrado en el almacenamiento seguro.");
        }

        return File(stream, "application/xml", factura.ArchivoXmlNombreOriginal);
    }

    /// <summary>
    /// Descarga segura del archivo PDF con control de acceso por proveedor.
    /// </summary>
    [HttpGet("{id:int}/descargar/pdf")]
    public async Task<IActionResult> DescargarPdf(int id, CancellationToken ct)
    {
        string rol = ObtenerRol();
        int? proveedorId = string.Equals(rol, "Proveedor", StringComparison.OrdinalIgnoreCase)
            ? ObtenerProveedorId()
            : null;

        var factura = await _facturaService.ObtenerEntidadDetalleAsync(id, proveedorId, ct);
        if (factura == null)
        {
            return NotFoundResponse<object>("La factura solicitada no existe o no tiene permisos para descargar sus archivos.");
        }

        string subCarpeta = Path.GetDirectoryName(factura.ArchivoPdfNombreInterno) ?? string.Empty;
        string nombreArchivo = Path.GetFileName(factura.ArchivoPdfNombreInterno);

        var stream = await _storageService.ObtenerArchivoStreamAsync(nombreArchivo, subCarpeta, ct);
        if (stream == null)
        {
            return NotFoundResponse<object>("El archivo PDF no fue encontrado en el almacenamiento seguro.");
        }

        return File(stream, "application/pdf", factura.ArchivoPdfNombreOriginal);
    }
}
