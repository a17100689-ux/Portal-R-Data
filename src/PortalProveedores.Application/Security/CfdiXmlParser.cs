using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using PortalProveedores.Application.DTOs;

namespace PortalProveedores.Application.Security;

public interface ICfdiXmlParser
{
    FacturaParsedXml ParsearCfdi(Stream xmlStream);
}

public class CfdiXmlParser : ICfdiXmlParser
{
    public FacturaParsedXml ParsearCfdi(Stream xmlStream)
    {
        if (xmlStream.CanSeek)
            xmlStream.Position = 0;

        // Configuración segura contra ataques XXE (XML External Entity Injection) y Billion Laughs
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = 10 * 1024 * 1024 // Máximo 10MB expandido
        };

        using var reader = XmlReader.Create(xmlStream, settings);
        var doc = XDocument.Load(reader);

        var comprobante = doc.Root;
        if (comprobante == null)
            throw new InvalidOperationException("El XML no contiene un nodo raíz válido.");

        XNamespace cfdiNs = comprobante.Name.Namespace;
        XNamespace tfdNs = "http://www.sat.gob.mx/TimbreFiscalDigital";

        var timbre = doc.Descendants(tfdNs + "TimbreFiscalDigital").FirstOrDefault()
                     ?? doc.Descendants().FirstOrDefault(x => x.Name.LocalName == "TimbreFiscalDigital");
        string uuid = timbre?.Attribute("UUID")?.Value ?? string.Empty;

        if (string.IsNullOrWhiteSpace(uuid))
        {
            throw new InvalidOperationException("No se encontró el UUID / Timbre Fiscal Digital en el XML.");
        }

        var emisor = doc.Descendants(cfdiNs + "Emisor").FirstOrDefault()
                     ?? doc.Descendants().FirstOrDefault(x => x.Name.LocalName == "Emisor");

        var receptor = doc.Descendants(cfdiNs + "Receptor").FirstOrDefault()
                       ?? doc.Descendants().FirstOrDefault(x => x.Name.LocalName == "Receptor");

        var impuestos = doc.Descendants(cfdiNs + "Impuestos").FirstOrDefault()
                        ?? doc.Descendants().FirstOrDefault(x => x.Name.LocalName == "Impuestos");

        decimal subtotal = ParseDecimal(comprobante.Attribute("SubTotal")?.Value ?? comprobante.Attribute("subTotal")?.Value);
        decimal descuento = ParseDecimal(comprobante.Attribute("Descuento")?.Value ?? comprobante.Attribute("descuento")?.Value);
        decimal total = ParseDecimal(comprobante.Attribute("Total")?.Value ?? comprobante.Attribute("total")?.Value);
        decimal tipoCambio = ParseDecimal(comprobante.Attribute("TipoCambio")?.Value);
        if (tipoCambio <= 0) tipoCambio = 1.0000m;

        decimal traslados = ParseDecimal(impuestos?.Attribute("TotalImpuestosTrasladados")?.Value);
        decimal retenciones = ParseDecimal(impuestos?.Attribute("TotalImpuestosRetenidos")?.Value);

        DateTime.TryParse(comprobante.Attribute("Fecha")?.Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fechaEmision);
        if (fechaEmision == default)
            fechaEmision = DateTime.UtcNow;

        string usoCfdi = receptor?.Attribute("UsoCFDI")?.Value ?? receptor?.Attribute("usoCFDI")?.Value ?? "G03";
        string metodoPago = comprobante.Attribute("MetodoPago")?.Value ?? comprobante.Attribute("metodoPago")?.Value ?? "PPD";
        string formaPago = comprobante.Attribute("FormaPago")?.Value ?? comprobante.Attribute("formaPago")?.Value ?? "99";
        string regEmisor = emisor?.Attribute("RegimenFiscal")?.Value ?? emisor?.Attribute("regimenFiscal")?.Value ?? "601";
        string regReceptor = receptor?.Attribute("RegimenFiscalReceptor")?.Value ?? receptor?.Attribute("regimenFiscalReceptor")?.Value ?? "601";

        // Extracción de Partidas / Conceptos para TVP typePortal_FacturaDetalle
        var conceptosXml = new List<FacturaConceptoXml>();
        var conceptoNodes = doc.Descendants(cfdiNs + "Concepto").ToList();
        if (!conceptoNodes.Any())
        {
            conceptoNodes = doc.Descendants().Where(x => x.Name.LocalName == "Concepto").ToList();
        }

        short renglonIdx = 1;
        foreach (var c in conceptoNodes)
        {
            string claveProdServ = c.Attribute("ClaveProdServ")?.Value ?? string.Empty;
            string noId = c.Attribute("NoIdentificacion")?.Value ?? claveProdServ;
            string desc = c.Attribute("Descripcion")?.Value ?? string.Empty;
            string unidad = c.Attribute("ClaveUnidad")?.Value ?? c.Attribute("Unidad")?.Value ?? "PZA";
            decimal cant = ParseDecimal(c.Attribute("Cantidad")?.Value);
            decimal valorUnit = ParseDecimal(c.Attribute("ValorUnitario")?.Value);
            decimal descPartida = ParseDecimal(c.Attribute("Descuento")?.Value);
            decimal importe = ParseDecimal(c.Attribute("Importe")?.Value);

            decimal precioConDesc = cant > 0 && descPartida > 0 
                ? (valorUnit - (descPartida / cant)) 
                : valorUnit;

            conceptosXml.Add(new FacturaConceptoXml
            {
                Renglon = renglonIdx++,
                CodigoArticulo = !string.IsNullOrWhiteSpace(noId) ? noId.Trim() : claveProdServ.Trim(),
                ClaveProdServSat = claveProdServ.Trim(),
                Descripcion = desc.Trim(),
                Unidad = unidad.Trim(),
                Cantidad = cant,
                PrecioUnitarioSinDescuento = valorUnit,
                Descuento = descPartida.ToString("F2", CultureInfo.InvariantCulture),
                PrecioUnitarioConDescuento = precioConDesc,
                Importe = importe,
                PorcentajeRetencion = 0,
                MontoRetencion = 0,
                CantidadReal = cant
            });
        }

        return new FacturaParsedXml
        {
            UUID = uuid.Trim().ToUpperInvariant(),
            Serie = comprobante.Attribute("Serie")?.Value?.Trim(),
            Folio = comprobante.Attribute("Folio")?.Value?.Trim(),
            RFCEmisor = (emisor?.Attribute("Rfc")?.Value ?? emisor?.Attribute("rfc")?.Value ?? string.Empty).Trim().ToUpperInvariant(),
            NombreEmisor = emisor?.Attribute("Nombre")?.Value ?? emisor?.Attribute("nombre")?.Value ?? string.Empty,
            RFCReceptor = (receptor?.Attribute("Rfc")?.Value ?? receptor?.Attribute("rfc")?.Value ?? string.Empty).Trim().ToUpperInvariant(),
            NombreReceptor = receptor?.Attribute("Nombre")?.Value ?? receptor?.Attribute("nombre")?.Value ?? string.Empty,
            FechaEmision = fechaEmision,
            Subtotal = subtotal,
            Descuento = descuento,
            ImpuestosTrasladados = traslados,
            ImpuestosRetenidos = retenciones,
            Total = total,
            Moneda = comprobante.Attribute("Moneda")?.Value?.Trim() ?? "MXN",
            TipoCambio = tipoCambio,
            CodigoUsoCFDI = usoCfdi.Trim(),
            CodigoCFDIMetodoPago = metodoPago.Trim(),
            CodigoCFDIFormaPago = formaPago.Trim(),
            RegimenFiscalEmisor = regEmisor.Trim(),
            RegimenFiscalReceptor = regReceptor.Trim(),
            Conceptos = conceptosXml
        };
    }

    private static decimal ParseDecimal(string? val)
    {
        if (string.IsNullOrWhiteSpace(val)) return 0m;
        if (decimal.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
            return result;
        return 0m;
    }
}
