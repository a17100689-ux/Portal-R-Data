namespace PortalProveedores.Application.DTOs;

public class ResultadoValidacionArchivo
{
    public bool EsValido { get; set; }
    public string? MensajeError { get; set; }
    public string? TipoDetectado { get; set; }
}

public class CargaFacturaDto
{
    public int ProveedorId { get; set; }
    public Stream XmlStream { get; set; } = Stream.Null;
    public string XmlNombreOriginal { get; set; } = string.Empty;
    public long XmlTamano { get; set; }

    public Stream PdfStream { get; set; } = Stream.Null;
    public string PdfNombreOriginal { get; set; } = string.Empty;
    public long PdfTamano { get; set; }

    public string? Observaciones { get; set; }
}

public class FacturaParsedXml
{
    public string UUID { get; set; } = string.Empty;
    public string? Serie { get; set; }
    public string? Folio { get; set; }
    public string RFCEmisor { get; set; } = string.Empty;
    public string NombreEmisor { get; set; } = string.Empty;
    public string RFCReceptor { get; set; } = string.Empty;
    public string NombreReceptor { get; set; } = string.Empty;
    public DateTime FechaEmision { get; set; }
    public decimal Subtotal { get; set; }
    public decimal ImpuestosTrasladados { get; set; }
    public decimal ImpuestosRetenidos { get; set; }
    public decimal Total { get; set; }
    public string Moneda { get; set; } = "MXN";
}

public class ResultadoCargaFactura
{
    public bool Exitoso { get; set; }
    public string Mensaje { get; set; } = string.Empty;
    public int? FacturaId { get; set; }
    public string? UUID { get; set; }
}

public class LoginDto
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class ResultadoLogin
{
    public bool Exitoso { get; set; }
    public string Mensaje { get; set; } = string.Empty;
    public int? UsuarioId { get; set; }
    public string? Username { get; set; }
    public string? Rol { get; set; }
    public int? ProveedorId { get; set; }
}
