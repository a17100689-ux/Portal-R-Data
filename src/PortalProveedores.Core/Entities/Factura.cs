namespace PortalProveedores.Core.Entities;

public class Factura
{
    public int Id { get; set; }
    public int ProveedorId { get; set; }
    public Proveedor? Proveedor { get; set; }

    public string UUID { get; set; } = string.Empty;
    public string? Serie { get; set; }
    public string? Folio { get; set; }
    public string RFCEmisor { get; set; } = string.Empty;
    public string RFCReceptor { get; set; } = string.Empty;
    public DateTime FechaEmision { get; set; }
    public DateTime FechaCarga { get; set; } = DateTime.UtcNow;

    public decimal Subtotal { get; set; }
    public decimal ImpuestosTrasladados { get; set; }
    public decimal ImpuestosRetenidos { get; set; }
    public decimal Total { get; set; }
    public string Moneda { get; set; } = "MXN";

    public EstadoFactura Estatus { get; set; } = EstadoFactura.Pendiente;

    // Rutas protegidas de almacenamiento fuera de wwwroot
    public string ArchivoXmlNombreInterno { get; set; } = string.Empty;
    public string ArchivoXmlNombreOriginal { get; set; } = string.Empty;
    public string ArchivoPdfNombreInterno { get; set; } = string.Empty;
    public string ArchivoPdfNombreOriginal { get; set; } = string.Empty;

    public string? MotivoRechazo { get; set; }
    public string? Observaciones { get; set; }
}
