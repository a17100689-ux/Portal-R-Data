using System.ComponentModel.DataAnnotations;

namespace PortalProveedores.Application.DTOs;

/// <summary>
/// Resultado de la verificación de existencia y estado de un proveedor en el catálogo central (Cat_Proveedores).
/// </summary>
public class VerificarProveedorCatalogoDto
{
    public bool EnCatalogo { get; set; }
    public int? ProveedorId { get; set; }
    public string? CodigoProveedor { get; set; }
    public string? RFC { get; set; }
    public string? RazonSocial { get; set; }
    public string? CondicionesPago { get; set; }
    public bool RequiereValidarCompra { get; set; } = true;
    public bool OrdenCompraObligatoria { get; set; } = true;
    public bool EsProveedorNacional { get; set; } = true;
    public bool Activo { get; set; }
    public string? CodigoPostal { get; set; }
    public string? Telefono { get; set; }
    public string? EmailContacto { get; set; }
    public string? Email => EmailContacto;
    public string? RegimenFiscal { get; set; }
    public bool TieneUsuarioRegistrado { get; set; }
    public string? EmailRegistrado { get; set; }
    public string Mensaje { get; set; } = string.Empty;
}

/// <summary>
/// Elemento de resultado para el buscador de proveedores en catálogo central.
/// </summary>
public class ProveedorCatalogoItemDto
{
    public int ProveedorId { get; set; }
    public string CodigoProveedor { get; set; } = string.Empty;
    public string RFC { get; set; } = string.Empty;
    public string RazonSocial { get; set; } = string.Empty;
    public string? CondicionesPago { get; set; }
    public bool RequiereValidarCompra { get; set; } = true;
    public bool OrdenCompraObligatoria { get; set; } = true;
    public bool EsProveedorNacional { get; set; } = true;
    public bool Activo { get; set; } = true;
    public string? CodigoPostal { get; set; }
    public string? Telefono { get; set; }
    public string? EmailContacto { get; set; }
    public string? Email => EmailContacto;
    public string? RegimenFiscal { get; set; }
    public bool TieneUsuarioRegistrado { get; set; }
    public string? EmailRegistrado { get; set; }
}

/// <summary>
/// Filtros de búsqueda de proveedores en el catálogo central.
/// </summary>
public class ProveedorCatalogoFiltroDto
{
    public string? Termino { get; set; }
    public int Pagina { get; set; } = 1;
    public int TamanoPagina { get; set; } = 20;
}

/// <summary>
/// Contrato de solicitud para registro de un proveedor en el portal mediante API REST.
/// </summary>
public class RegistroProveedorRequestDto
{
    [Required(ErrorMessage = "El RFC del proveedor es obligatorio.")]
    [RegularExpression(@"^[A-Za-zÑñ&]{3,4}\d{6}[A-Za-z\d]{3}$", ErrorMessage = "El formato del RFC no es válido (12 o 13 caracteres SAT).")]
    public string RFC { get; set; } = string.Empty;

    [Required(ErrorMessage = "El código interno de proveedor es obligatorio.")]
    [StringLength(15, MinimumLength = 3, ErrorMessage = "El código debe tener entre 3 y 15 caracteres.")]
    public string CodigoProveedor { get; set; } = string.Empty;

    [StringLength(200, ErrorMessage = "La razón social no debe exceder 200 caracteres.")]
    public string? RazonSocial { get; set; }

    [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
    [EmailAddress(ErrorMessage = "El formato de correo no es válido.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres.")]
    public string Password { get; set; } = string.Empty;

    [Compare("Password", ErrorMessage = "Las contraseñas no coinciden.")]
    public string? ConfirmPassword { get; set; }

    public string? Telefono { get; set; }
    public string? RegimenFiscal { get; set; }
    public string? CodigoPostal { get; set; }
    public string? CondicionesPago { get; set; }
    public bool RequiereValidarCompra { get; set; } = true;
    public bool OrdenCompraObligatoria { get; set; } = true;
    public bool EsProveedorNacional { get; set; } = true;
}
