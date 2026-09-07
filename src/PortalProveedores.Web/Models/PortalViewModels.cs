using System.ComponentModel.DataAnnotations;

namespace PortalProveedores.Web.Models;

public class LoginViewModel
{
    [Required(ErrorMessage = "El nombre de usuario es obligatorio.")]
    [Display(Name = "Usuario")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [DataType(DataType.Password)]
    [Display(Name = "Contraseña")]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

public class CargaFacturaViewModel
{
    [Required(ErrorMessage = "El archivo XML del CFDI es obligatorio.")]
    [Display(Name = "Archivo XML (CFDI)")]
    public IFormFile? ArchivoXml { get; set; }

    [Required(ErrorMessage = "La representación impresa en PDF es obligatoria.")]
    [Display(Name = "Representación Impresa (PDF)")]
    public IFormFile? ArchivoPdf { get; set; }

    [StringLength(500, ErrorMessage = "Las observaciones no deben exceder 500 caracteres.")]
    [Display(Name = "Observaciones o Notas")]
    public string? Observaciones { get; set; }
}

public class ErrorViewModel
{
    public string? RequestId { get; set; }
    public string? TicketId { get; set; }
    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
}

public class RegistroProveedorViewModel
{
    [Required(ErrorMessage = "El RFC del proveedor es obligatorio.")]
    [RegularExpression(@"^[A-Za-zÑñ&]{3,4}\d{6}[A-Za-z\d]{3}$", ErrorMessage = "El formato de RFC no es válido (12 o 13 caracteres SAT).")]
    [StringLength(15, MinimumLength = 12, ErrorMessage = "El RFC debe tener entre 12 y 13 caracteres.")]
    [Display(Name = "RFC del Proveedor")]
    public string RFC { get; set; } = string.Empty;

    [Required(ErrorMessage = "La razón social es obligatoria.")]
    [StringLength(200, MinimumLength = 3, ErrorMessage = "La razón social debe tener entre 3 y 200 caracteres.")]
    [Display(Name = "Razón Social")]
    public string RazonSocial { get; set; } = string.Empty;

    [Required(ErrorMessage = "El código interno de proveedor es obligatorio.")]
    [StringLength(15, MinimumLength = 3, ErrorMessage = "El código debe tener entre 3 y 15 caracteres.")]
    [Display(Name = "Código Interno de Proveedor (ERP)")]
    public string CodigoProveedor { get; set; } = string.Empty;

    [Display(Name = "Régimen Fiscal (SAT)")]
    public string? RegimenFiscal { get; set; } = "601";

    [Required(ErrorMessage = "El código postal fiscal es obligatorio.")]
    [RegularExpression(@"^\d{5}$", ErrorMessage = "El código postal debe ser numérico de 5 dígitos.")]
    [Display(Name = "Código Postal Fiscal")]
    public string CodigoPostal { get; set; } = string.Empty;

    [Display(Name = "Condiciones de Pago")]
    public string CondicionesPago { get; set; } = "30";

    [Phone(ErrorMessage = "El formato del teléfono no es válido.")]
    [Display(Name = "Teléfono de Contacto")]
    public string? Telefono { get; set; }

    [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
    [EmailAddress(ErrorMessage = "El formato de correo no es válido.")]
    [Display(Name = "Correo Electrónico Oficial")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contraseña de acceso inicial es obligatoria.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres.")]
    [DataType(DataType.Password)]
    [Display(Name = "Contraseña Inicial de Acceso")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "La confirmación de contraseña es obligatoria.")]
    [DataType(DataType.Password)]
    [Compare("Password", ErrorMessage = "Las contraseñas no coinciden.")]
    [Display(Name = "Confirmar Contraseña")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Display(Name = "Requiere Validación de Recepción en Almacén")]
    public bool RequiereValidarCompra { get; set; } = true;

    [Display(Name = "Orden de Compra Obligatoria")]
    public bool OrdenCompraObligatoria { get; set; } = true;

    [Display(Name = "Proveedor Nacional")]
    public bool EsProveedorNacional { get; set; } = true;

    [Display(Name = "Activo en el Portal")]
    public bool Activo { get; set; } = true;
}
