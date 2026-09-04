using PortalProveedores.Application.DTOs;
using PortalProveedores.Application.Interfaces;
using PortalProveedores.Application.Security;

namespace PortalProveedores.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUsuarioRepository _usuarioRepo;
    private readonly ICryptoService _cryptoService;

    public AuthService(IUsuarioRepository usuarioRepo, ICryptoService cryptoService)
    {
        _usuarioRepo = usuarioRepo;
        _cryptoService = cryptoService;
    }

    public async Task<ResultadoLogin> ValidarCredencialesAsync(LoginDto dto, string ipAddress, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password))
        {
            return new ResultadoLogin { Exitoso = false, Mensaje = "Credenciales incompletas." };
        }

        string identificador = dto.Username.Trim();

        // 1. Intentar autenticación como Administrador / Revisor Interno (sp_Portal_Admin_Login)
        var admin = await _usuarioRepo.ObtenerAdminPorLoginSpAsync(identificador, ct);
        if (admin != null)
        {
            if (!admin.Activo)
            {
                return new ResultadoLogin { Exitoso = false, Mensaje = "Esta cuenta de administrador se encuentra inactiva." };
            }

            bool passwordAdminValido = _cryptoService.VerifyPassword(dto.Password, admin.PasswordHash);
            if (!passwordAdminValido)
            {
                await _usuarioRepo.RegistrarAuditoriaSpAsync(admin.AdminId, "SEGURIDAD", "LOGIN_FALLIDO", "Contraseña incorrecta para administrador", ipAddress, ct);
                return new ResultadoLogin { Exitoso = false, Mensaje = "Usuario o contraseña incorrectos." };
            }

            // Éxito Admin
            await _usuarioRepo.RegistrarAuditoriaSpAsync(admin.AdminId, "SEGURIDAD", "LOGIN_EXITOSO", "Inicio de sesión de administrador exitoso", ipAddress, ct);

            return new ResultadoLogin
            {
                Exitoso = true,
                UsuarioId = admin.AdminId,
                Username = admin.Email,
                Email = admin.Email,
                RazonSocial = admin.NombreCompleto,
                Rol = string.Equals(admin.Rol, "ADMIN", StringComparison.OrdinalIgnoreCase) ? "Administrador" : admin.Rol,
                ProveedorId = null,
                EsAdmin = true,
                Mensaje = "Autenticación de administrador exitosa."
            };
        }

        // 2. Intentar autenticación como Proveedor (sp_Portal_Usuario_ObtenerPorLogin)
        var proveedor = await _usuarioRepo.ObtenerProveedorPorLoginSpAsync(identificador, ct);
        if (proveedor != null)
        {
            // Validar bloqueo por intentos fallidos
            if (proveedor.BloqueadoHasta.HasValue && proveedor.BloqueadoHasta.Value > DateTime.UtcNow)
            {
                await _usuarioRepo.RegistrarAuditoriaSpAsync(proveedor.UsuarioId, "SEGURIDAD", "LOGIN_BLOQUEADO", 
                    $"Intento de acceso a cuenta de proveedor bloqueada hasta {proveedor.BloqueadoHasta.Value:yyyy-MM-dd HH:mm:ss}", ipAddress, ct);

                return new ResultadoLogin
                {
                    Exitoso = false,
                    Mensaje = "La cuenta se encuentra temporalmente bloqueada por exceso de intentos fallidos. Intente más tarde (15 minutos)."
                };
            }

            if (!proveedor.UsuarioActivo || !proveedor.ProveedorActivo)
            {
                return new ResultadoLogin { Exitoso = false, Mensaje = "La cuenta o razón social del proveedor se encuentra inactiva." };
            }

            bool passwordProvValido = _cryptoService.VerifyPassword(dto.Password, proveedor.PasswordHash);
            if (!passwordProvValido)
            {
                await _usuarioRepo.RegistrarIntentoFallidoSpAsync(proveedor.UsuarioId, ct);
                return new ResultadoLogin { Exitoso = false, Mensaje = "Usuario o contraseña incorrectos." };
            }

            // Éxito Proveedor: registrar login exitoso en bitácora
            await _usuarioRepo.RegistrarLoginExitosoSpAsync(proveedor.UsuarioId, ipAddress, ct);

            return new ResultadoLogin
            {
                Exitoso = true,
                UsuarioId = proveedor.UsuarioId,
                ProveedorId = proveedor.ProveedorId,
                CodigoProveedor = proveedor.CodigoProveedor,
                RFC = proveedor.RFC,
                Username = proveedor.Email,
                Email = proveedor.Email,
                RazonSocial = proveedor.RazonSocial,
                Rol = "Proveedor",
                EsAdmin = false,
                Mensaje = "Autenticación exitosa."
            };
        }

        // 3. Respuesta genérica defensiva ante identificador no encontrado
        return new ResultadoLogin { Exitoso = false, Mensaje = "Usuario o contraseña incorrectos." };
    }
}
