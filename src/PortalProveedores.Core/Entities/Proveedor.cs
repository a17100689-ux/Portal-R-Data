namespace PortalProveedores.Core.Entities;

public class Proveedor
{
    public int Id { get; set; }
    public string RFC { get; set; } = string.Empty;
    public string RazonSocial { get; set; } = string.Empty;
    public string EmailContacto { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
    public ICollection<Factura> Facturas { get; set; } = new List<Factura>();
}
