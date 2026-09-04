using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortalProveedores.Application.DTOs;
using PortalProveedores.Application.Interfaces;
using PortalProveedores.Web.Models;

namespace PortalProveedores.Web.Controllers;

[Authorize]
public class FacturasController : Controller
{
    private readonly IFacturaService _facturaService;
    private readonly IStorageService _storageService;

    public FacturasController(IFacturaService facturaService, IStorageService storageService)
    {
        _facturaService = facturaService;
        _storageService = storageService;
    }

    private int ObtenerProveedorIdActual()
    {
        var claim = User.FindFirst("ProveedorId")?.Value;
        return int.TryParse(claim, out int id) ? id : 1; // Default a 1 en pruebas locales si no está asignado
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        int proveedorId = ObtenerProveedorIdActual();
        var facturas = await _facturaService.ObtenerFacturasProveedorAsync(proveedorId);
        return View(facturas);
    }

    [HttpGet]
    public IActionResult Cargar()
    {
        return View(new CargaFacturaViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(5 * 1024 * 1024)] // Límite estricto de 5 MB a nivel de Endpoint
    public async Task<IActionResult> Cargar(CargaFacturaViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (model.ArchivoXml == null || model.ArchivoPdf == null)
        {
            ModelState.AddModelError(string.Empty, "Debe adjuntar ambos archivos obligatoriamente (XML y PDF).");
            return View(model);
        }

        int proveedorId = ObtenerProveedorIdActual();

        using var xmlStream = model.ArchivoXml.OpenReadStream();
        using var pdfStream = model.ArchivoPdf.OpenReadStream();

        var dto = new CargaFacturaDto
        {
            ProveedorId = proveedorId,
            XmlStream = xmlStream,
            XmlNombreOriginal = model.ArchivoXml.FileName,
            XmlTamano = model.ArchivoXml.Length,
            PdfStream = pdfStream,
            PdfNombreOriginal = model.ArchivoPdf.FileName,
            PdfTamano = model.ArchivoPdf.Length,
            Observaciones = model.Observaciones
        };

        var resultado = await _facturaService.CargarFacturaAsync(dto);

        if (!resultado.Exitoso)
        {
            ModelState.AddModelError(string.Empty, resultado.Mensaje);
            return View(model);
        }

        TempData["MensajeExito"] = $"¡Factura cargada correctamente! Folio Fiscal (UUID): {resultado.UUID}";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> DescargarXml(int id)
    {
        int proveedorId = ObtenerProveedorIdActual();
        var factura = await _facturaService.ObtenerEntidadDetalleAsync(id, proveedorId);
        if (factura == null)
        {
            return NotFound("La factura solicitada no existe o no tiene permisos para acceder.");
        }

        string subCarpeta = Path.GetDirectoryName(factura.ArchivoXmlNombreInterno) ?? string.Empty;
        string nombreArchivo = Path.GetFileName(factura.ArchivoXmlNombreInterno);

        var stream = await _storageService.ObtenerArchivoStreamAsync(nombreArchivo, subCarpeta);
        if (stream == null)
        {
            return NotFound("El archivo XML no se encuentra en el almacenamiento.");
        }

        return File(stream, "application/xml", factura.ArchivoXmlNombreOriginal);
    }

    [HttpGet]
    public async Task<IActionResult> DescargarPdf(int id)
    {
        int proveedorId = ObtenerProveedorIdActual();
        var factura = await _facturaService.ObtenerEntidadDetalleAsync(id, proveedorId);
        if (factura == null)
        {
            return NotFound("La factura solicitada no existe o no tiene permisos para acceder.");
        }

        string subCarpeta = Path.GetDirectoryName(factura.ArchivoPdfNombreInterno) ?? string.Empty;
        string nombreArchivo = Path.GetFileName(factura.ArchivoPdfNombreInterno);

        var stream = await _storageService.ObtenerArchivoStreamAsync(nombreArchivo, subCarpeta);
        if (stream == null)
        {
            return NotFound("El archivo PDF no se encuentra en el almacenamiento.");
        }

        return File(stream, "application/pdf", factura.ArchivoPdfNombreOriginal);
    }
}
