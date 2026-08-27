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
