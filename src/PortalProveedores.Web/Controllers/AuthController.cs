using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortalProveedores.Application.DTOs;
using PortalProveedores.Application.Interfaces;
using PortalProveedores.Application.Security;
using PortalProveedores.Web.Models;

namespace PortalProveedores.Web.Controllers;

public class AuthController : Controller
{
    private readonly IAuthService _authService;
    private readonly IProveedorRepository _proveedorRepo;
    private readonly ICryptoService _cryptoService;

    public AuthController(
        IAuthService authService,
        IProveedorRepository proveedorRepo,
        ICryptoService cryptoService)
    {
        _authService = authService;
        _proveedorRepo = proveedorRepo;
        _cryptoService = cryptoService;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null, string? tab = null)
    {
        bool esAdmin = User.Identity?.IsAuthenticated == true &&
            (User.IsInRole("Administrador") || User.IsInRole("Admin") || User.HasClaim("EsAdmin", "true"));

        if (User.Identity?.IsAuthenticated == true && !esAdmin)
        {
            return RedirectToAction("Index", "Facturas");
        }

        ViewData["ReturnUrl"] = returnUrl;
        ViewData["ActiveTab"] = !string.IsNullOrEmpty(tab) ? tab : (esAdmin ? "registro" : "login");
        ViewData["EsAdmin"] = esAdmin;

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";
        var resultado = await _authService.ValidarCredencialesAsync(new LoginDto
        {
            Username = model.Username,
            Password = model.Password
        }, ipAddress);

        if (!resultado.Exitoso)
        {
            ModelState.AddModelError(string.Empty, resultado.Mensaje);
            return View(model);
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, resultado.UsuarioId?.ToString() ?? "0"),
            new Claim(ClaimTypes.Name, !string.IsNullOrEmpty(resultado.RazonSocial) ? resultado.RazonSocial : (resultado.Username ?? string.Empty)),
            new Claim(ClaimTypes.Role, resultado.Rol ?? "Proveedor"),
            new Claim("EsAdmin", resultado.EsAdmin ? "true" : "false")
        };

        if (!string.IsNullOrEmpty(resultado.Email))
        {
            claims.Add(new Claim(ClaimTypes.Email, resultado.Email));
        }

        if (!string.IsNullOrEmpty(resultado.RFC))
        {
            claims.Add(new Claim("RFC", resultado.RFC));
        }

        if (resultado.ProveedorId.HasValue)
        {
            claims.Add(new Claim("ProveedorId", resultado.ProveedorId.Value.ToString()));
        }

        if (!string.IsNullOrEmpty(resultado.CodigoProveedor))
        {
            claims.Add(new Claim("CodigoProveedor", resultado.CodigoProveedor));
        }

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = false,
            ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30)
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity),
            authProperties);

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Index", "Facturas");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login", "Auth");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegistrarProveedor(RegistroProveedorViewModel model, CancellationToken ct)
    {
        bool esAdmin = User.Identity?.IsAuthenticated == true &&
            (User.IsInRole("Administrador") || User.IsInRole("Admin") || User.HasClaim("EsAdmin", "true"));

        if (!esAdmin)
        {
            TempData["MensajeError"] = "Acceso restringido. Solo los usuarios con rol de Administrador pueden dar de alta proveedores.";
            return RedirectToAction("Login", new { tab = "login" });
        }

        if (!ModelState.IsValid)
        {
            ViewData["ActiveTab"] = "registro";
            ViewData["EsAdmin"] = true;
            ViewData["RegistroModel"] = model;
            return View("Login", new LoginViewModel());
        }

        string passwordHash = _cryptoService.HashPassword(model.Password);
        int adminId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out int parsedId) ? parsedId : 1;
        string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";

        var resultado = await _proveedorRepo.CrearProveedorCompletoAsync(new CrearProveedorDto
        {
            CodigoProveedor = model.CodigoProveedor.Trim().ToUpperInvariant(),
            RFC = model.RFC.Trim().ToUpperInvariant(),
            RazonSocial = model.RazonSocial.Trim().ToUpperInvariant(),
            RegimenFiscal = model.RegimenFiscal,
            CodigoPostal = model.CodigoPostal,
            CondicionesPago = model.CondicionesPago,
            Telefono = model.Telefono,
            Email = model.Email.Trim().ToLowerInvariant(),
            Password = model.Password,
            RequiereValidarCompra = model.RequiereValidarCompra,
            OrdenCompraObligatoria = model.OrdenCompraObligatoria,
            EsProveedorNacional = model.EsProveedorNacional,
            Activo = model.Activo
        }, passwordHash, adminId, ipAddress, ct);

        if (!resultado.Exitoso)
        {
            ModelState.AddModelError(string.Empty, resultado.Mensaje);
            ViewData["ActiveTab"] = "registro";
            ViewData["EsAdmin"] = true;
            ViewData["RegistroModel"] = model;
            return View("Login", new LoginViewModel());
        }

        TempData["MensajeExito"] = $"Proveedor '{model.RazonSocial}' (RFC: {model.RFC}) registrado con éxito con código '{model.CodigoProveedor}'.";
        return RedirectToAction("Login", new { tab = "registro" });
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }
}
