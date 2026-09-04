using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using PortalProveedores.Core.Common;

namespace PortalProveedores.Web.Controllers.Api;

/// <summary>
/// Controlador base para la API REST. Provee extracción centralizada de identidad, claims y respuestas estándar ApiResponse<T>.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public abstract class BaseApiController : ControllerBase
{
    protected int? ObtenerUsuarioId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out int id) ? id : null;
    }

    protected int? ObtenerProveedorId()
    {
        var claim = User.FindFirst("ProveedorId")?.Value;
        return int.TryParse(claim, out int id) ? id : null;
    }

    protected string ObtenerRol()
    {
        return User.FindFirst(ClaimTypes.Role)?.Value ?? "Proveedor";
    }

    protected string ObtenerIpAddress()
    {
        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";
    }

    protected OkObjectResult OkResponse<T>(T data, string message = "Operación realizada con éxito.")
    {
        return Ok(ApiResponse<T>.Ok(data, message, StatusCodes.Status200OK));
    }

    protected CreatedAtActionResult CreatedResponse<T>(string actionName, object routeValues, T data, string message = "Recurso creado exitosamente.")
    {
        return CreatedAtAction(actionName, routeValues, ApiResponse<T>.Ok(data, message, StatusCodes.Status201Created));
    }

    protected BadRequestObjectResult BadRequestResponse<T>(string message, List<string>? errors = null)
    {
        return BadRequest(ApiResponse<T>.Fail(message, errors, StatusCodes.Status400BadRequest));
    }

    protected NotFoundObjectResult NotFoundResponse<T>(string message)
    {
        return NotFound(ApiResponse<T>.Fail(message, null, StatusCodes.Status404NotFound));
    }

    protected ObjectResult ForbiddenResponse<T>(string message = "No tiene permisos para acceder o modificar este recurso.")
    {
        return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<T>.Fail(message, null, StatusCodes.Status403Forbidden));
    }
}
