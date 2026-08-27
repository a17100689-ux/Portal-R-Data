using PortalProveedores.Application.DTOs;
using PortalProveedores.Application.Interfaces;
using PortalProveedores.Application.Security;
using PortalProveedores.Core.Entities;

namespace PortalProveedores.Application.Services;

public class FacturaService : IFacturaService
{
    private readonly IFileSecurityValidator _fileValidator;
    private readonly ICfdiXmlParser _xmlParser;
    private readonly IStorageService _storageService;
    private readonly IFacturaRepository _facturaRepo;

    public FacturaService(
        IFileSecurityValidator fileValidator,
        ICfdiXmlParser xmlParser,
        IStorageService storageService,
        IFacturaRepository facturaRepo)
    {
        _fileValidator = fileValidator;
        _xmlParser = xmlParser;
        _storageService = storageService;
        _facturaRepo = facturaRepo;
    }

    public async Task<ResultadoCargaFactura> CargarFacturaAsync(CargaFacturaDto dto, CancellationToken ct = default)
    {
        // 1. Validar XML (Magic numbers, tamaño <= 5MB, sintaxis)
        var validacionXml = _fileValidator.ValidarXml(dto.XmlStream, dto.XmlTamano, dto.XmlNombreOriginal);
        if (!validacionXml.EsValido)
        {
            return new ResultadoCargaFactura
            {
                Exitoso = false,
                Mensaje = $"Error en archivo XML: {validacionXml.MensajeError}"
            };
        }

        // 2. Validar PDF (Magic numbers %PDF-, tamaño <= 5MB)
        var validacionPdf = _fileValidator.ValidarPdf(dto.PdfStream, dto.PdfTamano, dto.PdfNombreOriginal);
        if (!validacionPdf.EsValido)
        {
            return new ResultadoCargaFactura
            {
                Exitoso = false,
                Mensaje = $"Error en archivo PDF: {validacionPdf.MensajeError}"
            };
        }

        // 3. Parsear CFDI de manera segura (con protección contra XXE)
        FacturaParsedXml parsed;
        try
        {
            parsed = _xmlParser.ParsearCfdi(dto.XmlStream);
        }
        catch (Exception ex)
        {
            return new ResultadoCargaFactura
            {
                Exitoso = false,
                Mensaje = $"No fue posible procesar el comprobante XML CFDI: {ex.Message}"
            };
        }

        if (string.IsNullOrWhiteSpace(parsed.UUID))
        {
            return new ResultadoCargaFactura
            {
                Exitoso = false,
                Mensaje = "El archivo XML no contiene un UUID (Folio Fiscal) válido."
            };
        }

        // 4. Verificar existencia de duplicados vía Stored Procedure
        bool existe = await _facturaRepo.ExisteUuidSpAsync(parsed.UUID, ct);
        if (existe)
        {
            return new ResultadoCargaFactura
            {
                Exitoso = false,
                Mensaje = $"La factura con UUID {parsed.UUID} ya fue registrada previamente en el portal."
            };
        }

        // 5. Guardar archivos en almacenamiento seguro fuera de wwwroot
        string subCarpeta = $"{parsed.RFCEmisor}/{DateTime.UtcNow:yyyy}/{DateTime.UtcNow:MM}";
        string xmlNombreInterno;
        string pdfNombreInterno;

        try
        {
            xmlNombreInterno = await _storageService.GuardarArchivoAsync(dto.XmlStream, ".xml", subCarpeta, ct);
            pdfNombreInterno = await _storageService.GuardarArchivoAsync(dto.PdfStream, ".pdf", subCarpeta, ct);
        }
        catch
        {
            return new ResultadoCargaFactura
            {
                Exitoso = false,
                Mensaje = "Ocurrió un error al guardar los archivos de la factura en el almacenamiento seguro."
            };
        }

        // 6. Registrar en Base de Datos mediante Stored Procedure
        var factura = new Factura
        {
            ProveedorId = dto.ProveedorId,
            UUID = parsed.UUID,
            Serie = parsed.Serie,
            Folio = parsed.Folio,
            RFCEmisor = parsed.RFCEmisor,
            RFCReceptor = parsed.RFCReceptor,
            FechaEmision = parsed.FechaEmision,
            FechaCarga = DateTime.UtcNow,
            Subtotal = parsed.Subtotal,
            ImpuestosTrasladados = parsed.ImpuestosTrasladados,
            ImpuestosRetenidos = parsed.ImpuestosRetenidos,
            Total = parsed.Total,
            Moneda = parsed.Moneda,
            Estatus = EstadoFactura.Pendiente,
            ArchivoXmlNombreInterno = $"{subCarpeta}/{xmlNombreInterno}",
            ArchivoXmlNombreOriginal = Path.GetFileName(dto.XmlNombreOriginal),
            ArchivoPdfNombreInterno = $"{subCarpeta}/{pdfNombreInterno}",
            ArchivoPdfNombreOriginal = Path.GetFileName(dto.PdfNombreOriginal),
            Observaciones = dto.Observaciones
        };

        try
        {
            int facturaId = await _facturaRepo.InsertarFacturaSpAsync(factura, ct);
            return new ResultadoCargaFactura
            {
                Exitoso = true,
                FacturaId = facturaId,
                UUID = parsed.UUID,
                Mensaje = "Factura cargada y validada exitosamente."
            };
        }
        catch
        {
            // Limpieza de archivos en caso de fallo en BD
            await _storageService.EliminarArchivoAsync(xmlNombreInterno, subCarpeta, ct);
            await _storageService.EliminarArchivoAsync(pdfNombreInterno, subCarpeta, ct);

            return new ResultadoCargaFactura
            {
                Exitoso = false,
                Mensaje = "No fue posible registrar la factura en la base de datos."
            };
        }
    }

    public async Task<Factura?> ObtenerDetalleAsync(int facturaId, int? proveedorId, CancellationToken ct = default)
    {
        var factura = await _facturaRepo.ObtenerFacturaPorIdSpAsync(facturaId, ct);
        if (factura == null) return null;

        // Si es un proveedor, solo puede ver sus propias facturas
        if (proveedorId.HasValue && factura.ProveedorId != proveedorId.Value)
        {
            return null;
        }

        return factura;
    }

    public async Task<IEnumerable<Factura>> ObtenerFacturasProveedorAsync(int proveedorId, CancellationToken ct = default)
    {
        return await _facturaRepo.ListarFacturasPorProveedorSpAsync(proveedorId, ct);
    }
}
