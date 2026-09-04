namespace PortalProveedores.Core.Entities;

public class UsuarioAdministrador
{
    public int AdminId { get; set; }
    public string CodigoEmpleado { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Rol { get; set; } = "ADMIN"; // 'ADMIN', 'COMPRAS', 'MESA_CONTROL'
    public bool Activo { get; set; } = true;
    public DateTime? UltimoAcceso { get; set; }
}

public class UsuarioProveedor
{
    public int UsuarioId { get; set; }
    public int ProveedorId { get; set; }
    public string RFC { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public int IntentosFallidos { get; set; }
    public DateTime? BloqueadoHasta { get; set; }
    public bool UsuarioActivo { get; set; } = true;
    public string CodigoProveedor { get; set; } = string.Empty;
    public string RazonSocial { get; set; } = string.Empty;
    public bool RequiereValidarCompra { get; set; } = true;
    public bool OrdenCompraObligatoria { get; set; } = true;
    public string? CondicionesPago { get; set; }
    public bool ProveedorActivo { get; set; } = true;
}

public class Usuario
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? Salt { get; set; }
    public RolUsuario Rol { get; set; } = RolUsuario.Proveedor;
    public int? ProveedorId { get; set; }
    public string? CodigoProveedor { get; set; }
    public Proveedor? Proveedor { get; set; }
    public bool Activo { get; set; } = true;
    public int IntentosFallidosLogin { get; set; }
    public DateTime? BloqueadoHasta { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
}
