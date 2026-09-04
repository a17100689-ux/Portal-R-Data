using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using PortalProveedores.Application.DTOs;
using PortalProveedores.Application.Interfaces;
using PortalProveedores.Application.Security;
using PortalProveedores.Core.Common;
using PortalProveedores.Core.Entities;

namespace PortalProveedores.Application.Services;

/// <summary>
/// Cerebro central de orquestación de la lógica de negocio para la recepción y gestión de facturas.
/// Conecta la validación de archivos, comprobación fiscal previa (sp_Portal_ValidarFacturaPrevia),
/// persistencia transaccional con TVPs (sp_Portal_RegistrarFacturaCompleta) y consultas paginadas.
/// </summary>
public class FacturaService : IFacturaService
{
    private readonly IFileSecurityValidator _fileValidator;
    private readonly ICfdiXmlParser _xmlParser;
    private readonly IStorageService _storageService;
    private readonly IFacturaRepository _facturaRepo;
    private readonly IProveedorRepository _proveedorRepo;
    private readonly IUsuarioRepository _usuarioRepo;
    private readonly ILogger<FacturaService> _logger;

    public FacturaService(
        IFileSecurityValidator fileValidator,
        ICfdiXmlParser xmlParser,
        IStorageService storageService,
        IFacturaRepository facturaRepo,
        IProveedorRepository proveedorRepo,
        IUsuarioRepository usuarioRepo,
        ILogger<FacturaService> logger)
    {
        _fileValidator = fileValidator;
        _xmlParser = xmlParser;
        _storageService = storageService;
        _facturaRepo = facturaRepo;
        _proveedorRepo = proveedorRepo;
        _usuarioRepo = usuarioRepo;
        _logger = logger;
    }

    public async Task<ResultadoCargaFactura> CargarFacturaAsync(CargaFacturaDto dto, CancellationToken ct = default)
    {
        // 1. Validar existencia del proveedor en el catálogo de base de datos
        var proveedor = await _proveedorRepo.ObtenerPorIdSpAsync(dto.ProveedorId, ct);
        if (proveedor == null || !proveedor.Activo)
        {
            _logger.LogWarning("Intento de carga de factura para proveedor inválido o inactivo. ProveedorId: {ProveedorId}", dto.ProveedorId);
            return new ResultadoCargaFactura
            {
                Exitoso = false,
                Mensaje = "El proveedor asociado a la cuenta no es válido o se encuentra inactivo."
            };
        }

        // 2. Validar firmas de bytes (Magic numbers) y tamaños (máx 5MB)
        var validacionXml = _fileValidator.ValidarXml(dto.XmlStream, dto.XmlTamano, dto.XmlNombreOriginal);
        if (!validacionXml.EsValido)
        {
            return new ResultadoCargaFactura
            {
                Exitoso = false,
                Mensaje = $"Error en archivo XML: {validacionXml.MensajeError}"
            };
        }

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
        string xmlTextoCompleto;
        try
        {
            if (dto.XmlStream.CanSeek) dto.XmlStream.Position = 0;
            using (var reader = new StreamReader(dto.XmlStream, Encoding.UTF8, leaveOpen: true))
            {
                xmlTextoCompleto = await reader.ReadToEndAsync(ct);
            }
            if (dto.XmlStream.CanSeek) dto.XmlStream.Position = 0;

            parsed = _xmlParser.ParsearCfdi(dto.XmlStream);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al parsear CFDI para ProveedorId: {ProveedorId}", dto.ProveedorId);
            return new ResultadoCargaFactura
            {
                Exitoso = false,
                Mensaje = $"No fue posible procesar el comprobante XML CFDI: {ex.Message}"
            };
        }

        // 4. Validar reglas de negocio básicas del CFDI
        if (string.IsNullOrWhiteSpace(parsed.UUID))
        {
            return new ResultadoCargaFactura
            {
                Exitoso = false,
                Mensaje = "El archivo XML no contiene un Folio Fiscal (UUID) válido."
            };
        }

        // 4.1 Coherencia de Identidad Fiscal: El RFC Emisor debe coincidir con el RFC registrado del proveedor
        if (!string.Equals(parsed.RFCEmisor.Trim(), proveedor.RFC.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Discrepancia fiscal detectada: Emisor '{RFCEmisor}' vs Proveedor '{ProveedorRfc}'", parsed.RFCEmisor, proveedor.RFC);
            return new ResultadoCargaFactura
            {
                Exitoso = false,
                Mensaje = $"El RFC Emisor de la factura ({parsed.RFCEmisor}) no coincide con su RFC registrado ({proveedor.RFC}). Solo puede cargar facturas emitidas por su razón social."
            };
        }

        if (parsed.Total <= 0)
        {
            return new ResultadoCargaFactura
            {
                Exitoso = false,
                Mensaje = "El importe total de la factura debe ser mayor a cero."
            };
        }

        // 5. Validación previa sargable en BD mediante sp_Portal_ValidarFacturaPrevia
        var valPrevia = await _facturaRepo.ValidarFacturaPreviaSpAsync(proveedor.CodigoProveedor, parsed.Folio ?? string.Empty, parsed.UUID, ct);
        if (!valPrevia.EsValido)
        {
            _logger.LogInformation("Validación previa de factura rechazada: {Mensaje}. UUID: {UUID}", valPrevia.MensajeError, parsed.UUID);
            return new ResultadoCargaFactura
            {
                Exitoso = false,
                Mensaje = valPrevia.MensajeError
            };
        }

        // 6. Almacenamiento seguro de archivos físicos fuera de wwwroot
        string subCarpeta = $"{parsed.RFCEmisor}/{DateTime.UtcNow:yyyy}/{DateTime.UtcNow:MM}";
        string xmlNombreInterno;
        string pdfNombreInterno;
        string hashXml;
        string hashPdf;

        try
        {
            if (dto.XmlStream.CanSeek) dto.XmlStream.Position = 0;
            hashXml = CalcularSha256(dto.XmlStream);
            if (dto.XmlStream.CanSeek) dto.XmlStream.Position = 0;

            if (dto.PdfStream.CanSeek) dto.PdfStream.Position = 0;
            hashPdf = CalcularSha256(dto.PdfStream);
            if (dto.PdfStream.CanSeek) dto.PdfStream.Position = 0;

            xmlNombreInterno = await _storageService.GuardarArchivoAsync(dto.XmlStream, ".xml", subCarpeta, ct);
            pdfNombreInterno = await _storageService.GuardarArchivoAsync(dto.PdfStream, ".pdf", subCarpeta, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo al escribir archivos en almacenamiento seguro para UUID {UUID}", parsed.UUID);
            return new ResultadoCargaFactura
            {
                Exitoso = false,
                Mensaje = "Ocurrió un error al persistir los archivos de la factura en el almacenamiento seguro."
            };
        }

        // 7. Construcción de entidad Factura y armado de TVPs
        var factura = new Factura
        {
            ProveedorId = proveedor.ProveedorId,
            CodigoProveedor = proveedor.CodigoProveedor,
            UUID = parsed.UUID,
            Serie = parsed.Serie,
            FolioFactura = parsed.Folio ?? string.Empty,
            FechaFactura = parsed.FechaEmision,
            FechaRecepcion = DateTime.UtcNow,
            Subtotal = parsed.Subtotal,
            Descuento = parsed.Descuento,
            IvaTrasladado = parsed.ImpuestosTrasladados,
            IvaRetenido = parsed.ImpuestosRetenidos,
            CostoTotal = parsed.Total,
            Moneda = parsed.Moneda,
            TipoCambio = parsed.TipoCambio,
            CodigoUsoCFDI = parsed.CodigoUsoCFDI,
            CodigoCFDIMetodoPago = parsed.CodigoCFDIMetodoPago,
            CodigoCFDIFormaPago = parsed.CodigoCFDIFormaPago,
            RegimenFiscalEmisor = parsed.RegimenFiscalEmisor,
            RegimenFiscalReceptor = parsed.RegimenFiscalReceptor,
            NumeroCortoSucursal = dto.NumeroCortoSucursal,
            OrdenCompra = dto.OrdenCompra,
            EsMesaDeControl = dto.EsMesaDeControl,
            EstatusValidacion = 2, // Validada OK
            Observaciones = dto.Observaciones,
            ArchivoXmlNombreInterno = $"{subCarpeta}/{xmlNombreInterno}",
            ArchivoXmlNombreOriginal = Path.GetFileName(dto.XmlNombreOriginal),
            ArchivoPdfNombreInterno = $"{subCarpeta}/{pdfNombreInterno}",
            ArchivoPdfNombreOriginal = Path.GetFileName(dto.PdfNombreOriginal)
        };

        // Partidas extraídas del CFDI
        factura.Detalle = parsed.Conceptos.Select(c => new FacturaDetalleItem
        {
            Renglon = c.Renglon,
            CodigoArticulo = c.CodigoArticulo,
            ClaveProdServSat = c.ClaveProdServSat,
            Descripcion = c.Descripcion,
            Unidad = c.Unidad,
            Cantidad = c.Cantidad,
            PrecioUnitarioSinDescuento = c.PrecioUnitarioSinDescuento,
            Descuento = c.Descuento,
            PrecioUnitarioConDescuento = c.PrecioUnitarioConDescuento,
            Importe = c.Importe,
            PorcentajeRetencion = c.PorcentajeRetencion,
            MontoRetencion = c.MontoRetencion,
            CantidadReal = c.CantidadReal
        }).ToList();

        // Metadatos de Archivos para TVP
        factura.Archivos = new List<FacturaArchivoItem>
        {
            new()
            {
                TipoArchivo = "XML",
                NombreOriginal = Path.GetFileName(dto.XmlNombreOriginal),
                NombreAlmacenamiento = xmlNombreInterno,
                RutaFisicaSegura = $"{subCarpeta}/{xmlNombreInterno}",
                HashSha256 = hashXml,
                TamanoBytes = (int)dto.XmlTamano,
                XmlContenido = xmlTextoCompleto
            },
            new()
            {
                TipoArchivo = "PDF",
                NombreOriginal = Path.GetFileName(dto.PdfNombreOriginal),
                NombreAlmacenamiento = pdfNombreInterno,
                RutaFisicaSegura = $"{subCarpeta}/{pdfNombreInterno}",
                HashSha256 = hashPdf,
                TamanoBytes = (int)dto.PdfTamano,
                XmlContenido = null
            }
        };

        // 8. Inserción atómica en base de datos mediante sp_Portal_RegistrarFacturaCompleta
        try
        {
            long facturaId = await _facturaRepo.RegistrarFacturaCompletaSpAsync(factura, xmlTextoCompleto, ct);

            _logger.LogInformation("Factura registrada exitosamente en PortalProveedores_DB. Id: {FacturaId}, UUID: {UUID}", facturaId, factura.UUID);

            return new ResultadoCargaFactura
            {
                Exitoso = true,
                FacturaId = facturaId,
                UUID = parsed.UUID,
                Mensaje = "Factura cargada, validada y registrada exitosamente en el portal."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo al insertar factura en base de datos. Ejecutando rollback de archivos. UUID: {UUID}", parsed.UUID);

            // Rollback de archivos en caso de fallo en BD
            await _storageService.EliminarArchivoAsync(xmlNombreInterno, subCarpeta, ct);
            await _storageService.EliminarArchivoAsync(pdfNombreInterno, subCarpeta, ct);

            return new ResultadoCargaFactura
            {
                Exitoso = false,
                Mensaje = "No fue posible registrar la factura en la base de datos."
            };
        }
    }

    public async Task<FacturaDetalleDto?> ObtenerDetalleAsync(int facturaId, int? proveedorId, CancellationToken ct = default)
    {
        var detalle = await _facturaRepo.ObtenerFacturaDetalleSpAsync(facturaId, proveedorId ?? 0, ct);
        if (detalle != null)
        {
            return detalle;
        }

        // Si no se encontró por sp específico o es admin consultando, intentar vía método de compatibilidad
        var factura = await _facturaRepo.ObtenerFacturaPorIdSpAsync(facturaId, ct);
        if (factura == null) return null;

        if (proveedorId.HasValue && proveedorId.Value > 0 && factura.ProveedorId != proveedorId.Value)
        {
            return null; // IDOR protection
        }

        return new FacturaDetalleDto
        {
            FacturaId = factura.FacturaId,
            ProveedorId = factura.ProveedorId,
            CodigoProveedor = factura.CodigoProveedor,
            UUID = factura.UUID,
            Serie = factura.Serie,
            FolioFactura = factura.FolioFactura,
            FechaFactura = factura.FechaFactura,
            FechaRecepcion = factura.FechaRecepcion,
            Subtotal = factura.Subtotal,
            Descuento = factura.Descuento,
            IvaTrasladado = factura.IvaTrasladado,
            IvaRetenido = factura.IvaRetenido,
            CostoTotal = factura.CostoTotal,
            Moneda = factura.Moneda,
            TipoCambio = factura.TipoCambio,
            CodigoUsoCFDI = factura.CodigoUsoCFDI,
            CodigoCFDIMetodoPago = factura.CodigoCFDIMetodoPago,
            CodigoCFDIFormaPago = factura.CodigoCFDIFormaPago,
            RegimenFiscalEmisor = factura.RegimenFiscalEmisor,
            RegimenFiscalReceptor = factura.RegimenFiscalReceptor,
            NumeroCortoSucursal = factura.NumeroCortoSucursal,
            NombreSucursal = factura.NombreSucursal,
            OrdenCompra = factura.OrdenCompra,
            EsMesaDeControl = factura.EsMesaDeControl,
            EstatusValidacion = factura.EstatusValidacion,
            MotivoRechazo = factura.MotivoRechazo,
            Observaciones = factura.Observaciones,
            ArchivoXmlNombreOriginal = factura.ArchivoXmlNombreOriginal,
            ArchivoPdfNombreOriginal = factura.ArchivoPdfNombreOriginal,
            Partidas = factura.Detalle,
            Archivos = factura.Archivos
        };
    }

    public async Task<Factura?> ObtenerEntidadDetalleAsync(int facturaId, int? proveedorId, CancellationToken ct = default)
    {
        var factura = await _facturaRepo.ObtenerFacturaPorIdSpAsync(facturaId, ct);
        if (factura == null) return null;

        if (proveedorId.HasValue && proveedorId.Value > 0 && factura.ProveedorId != proveedorId.Value)
        {
            _logger.LogWarning("Violación de acceso en descarga: ProveedorId {ProveedorId} no tiene permiso para FacturaId {FacturaId}", proveedorId, facturaId);
            return null;
        }

        return factura;
    }

    public async Task<IEnumerable<Factura>> ObtenerFacturasProveedorAsync(int proveedorId, CancellationToken ct = default)
    {
        return await _facturaRepo.ListarFacturasPorProveedorSpAsync(proveedorId, ct);
    }

    public async Task<PaginatedResult<FacturaResumenDto>> ListarFacturasPaginadasAsync(FacturaFiltroDto filtro, int? proveedorId, CancellationToken ct = default)
    {
        return await _facturaRepo.ListarFacturasProveedorSpAsync(proveedorId ?? 0, filtro, ct);
    }

    public async Task<ApiResponse<bool>> CambiarEstatusFacturaAsync(
        int facturaId,
        ActualizarEstatusFacturaDto dto,
        int usuarioId,
        string rol,
        string ipAddress,
        CancellationToken ct = default)
    {
        if (!string.Equals(rol, "Revisor", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(rol, "Administrador", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(rol, "ADMIN", StringComparison.OrdinalIgnoreCase))
        {
            return ApiResponse<bool>.Fail("No tiene permisos para modificar el estatus de la factura.", statusCode: 403);
        }

        var factura = await _facturaRepo.ObtenerFacturaPorIdSpAsync(facturaId, ct);
        if (factura == null)
        {
            return ApiResponse<bool>.Fail($"La factura con identificador {facturaId} no fue encontrada.", statusCode: 404);
        }

        if (dto.NuevoEstatus == EstadoFactura.Rechazada && string.IsNullOrWhiteSpace(dto.MotivoRechazo))
        {
            return ApiResponse<bool>.Fail("Debe especificar obligatoriamente un motivo de rechazo.", statusCode: 400);
        }

        if (factura.Estatus == EstadoFactura.Pagada)
        {
            return ApiResponse<bool>.Fail("Una factura con estatus 'Pagada' no puede ser modificada.", statusCode: 400);
        }

        bool actualizado = await _facturaRepo.ActualizarEstatusFacturaSpAsync(facturaId, dto.NuevoEstatus, dto.MotivoRechazo, ct);
        if (!actualizado)
        {
            return ApiResponse<bool>.Fail("No se pudo actualizar el estatus de la factura.", statusCode: 500);
        }

        await _usuarioRepo.RegistrarAuditoriaSpAsync(
            usuarioId,
            "FACTURACION",
            "CAMBIO_ESTATUS_FACTURA",
            $"FacturaId {facturaId} (UUID: {factura.UUID}) cambió a '{dto.NuevoEstatus}'. Motivo: {dto.MotivoRechazo ?? "N/A"}",
            ipAddress,
            ct
        );

        return ApiResponse<bool>.Ok(true, $"El estatus de la factura ha sido actualizado exitosamente a '{dto.NuevoEstatus}'.");
    }

    private static string CalcularSha256(Stream stream)
    {
        using var sha256 = SHA256.Create();
        byte[] hash = sha256.ComputeHash(stream);
        var sb = new StringBuilder(64);
        foreach (byte b in hash)
        {
            sb.Append(b.ToString("x2"));
        }
        return sb.ToString();
    }
}
