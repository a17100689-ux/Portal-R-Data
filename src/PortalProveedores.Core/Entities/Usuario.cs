namespace PortalProveedores.Core.Entities;

public class Usuario
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Salt { get; set; } = string.Empty;
    public RolUsuario Rol { get; set; } = RolUsuario.Proveedor;
    public int? ProveedorId { get; set; }
    public Proveedor? Proveedor { get; set; }
    public bool Activo { get; set; } = true;
    public int IntentosFallidosLogin { get; set; }
    public DateTime? BloqueadoHasta { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
}
