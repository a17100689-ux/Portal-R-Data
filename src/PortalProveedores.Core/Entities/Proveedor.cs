namespace PortalProveedores.Core.Entities;

public class Proveedor
{
    public int ProveedorId { get; set; }
    public int Id { get => ProveedorId; set => ProveedorId = value; }
    public string CodigoProveedor { get; set; } = string.Empty;
    public string RFC { get; set; } = string.Empty;
    public string RazonSocial { get; set; } = string.Empty;
    public string? CondicionesPago { get; set; }
    public bool RequiereValidarCompra { get; set; } = true;
    public bool OrdenCompraObligatoria { get; set; } = true;
    public bool EsProveedorNacional { get; set; } = true;
    public bool Activo { get; set; } = true;
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
    public ICollection<Factura> Facturas { get; set; } = new List<Factura>();
}
