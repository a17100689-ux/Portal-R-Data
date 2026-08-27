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

        var timbre = doc.Descendants(tfdNs + "TimbreFiscalDigital").FirstOrDefault();
        string uuid = timbre?.Attribute("UUID")?.Value ?? string.Empty;

        if (string.IsNullOrWhiteSpace(uuid))
        {
            // Intentar buscar por nombre local sin namespace estricto
            var altTimbre = doc.Descendants().FirstOrDefault(x => x.Name.LocalName == "TimbreFiscalDigital");
            uuid = altTimbre?.Attribute("UUID")?.Value ?? string.Empty;
        }

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
        decimal total = ParseDecimal(comprobante.Attribute("Total")?.Value ?? comprobante.Attribute("total")?.Value);
        decimal traslados = ParseDecimal(impuestos?.Attribute("TotalImpuestosTrasladados")?.Value);
        decimal retenciones = ParseDecimal(impuestos?.Attribute("TotalImpuestosRetenidos")?.Value);

        DateTime.TryParse(comprobante.Attribute("Fecha")?.Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fechaEmision);
        if (fechaEmision == default)
            fechaEmision = DateTime.UtcNow;

        return new FacturaParsedXml
        {
            UUID = uuid.Trim().ToUpperInvariant(),
            Serie = comprobante.Attribute("Serie")?.Value,
            Folio = comprobante.Attribute("Folio")?.Value,
            RFCEmisor = emisor?.Attribute("Rfc")?.Value ?? emisor?.Attribute("rfc")?.Value ?? string.Empty,
            NombreEmisor = emisor?.Attribute("Nombre")?.Value ?? emisor?.Attribute("nombre")?.Value ?? string.Empty,
            RFCReceptor = receptor?.Attribute("Rfc")?.Value ?? receptor?.Attribute("rfc")?.Value ?? string.Empty,
            NombreReceptor = receptor?.Attribute("Nombre")?.Value ?? receptor?.Attribute("nombre")?.Value ?? string.Empty,
            FechaEmision = fechaEmision,
            Subtotal = subtotal,
            ImpuestosTrasladados = traslados,
            ImpuestosRetenidos = retenciones,
            Total = total,
            Moneda = comprobante.Attribute("Moneda")?.Value ?? "MXN"
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
