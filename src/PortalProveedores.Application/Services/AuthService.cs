using PortalProveedores.Application.DTOs;
using PortalProveedores.Application.Interfaces;
using PortalProveedores.Application.Security;
using PortalProveedores.Core.Entities;

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

        var usuario = await _usuarioRepo.ObtenerPorUsernameSpAsync(dto.Username.Trim(), ct);
        if (usuario == null)
        {
            // Mensaje genérico para prevenir enumeración de usuarios
            return new ResultadoLogin { Exitoso = false, Mensaje = "Usuario o contraseña incorrectos." };
        }

        // Validar si está bloqueado por múltiples intentos fallidos
        if (usuario.BloqueadoHasta.HasValue && usuario.BloqueadoHasta.Value > DateTime.UtcNow)
        {
            await _usuarioRepo.RegistrarAuditoriaSpAsync(new RegistroAuditoria
            {
                UsuarioId = usuario.Id,
                Accion = "LOGIN_BLOQUEADO",
                Detalle = $"Intento de acceso a cuenta bloqueada hasta {usuario.BloqueadoHasta.Value:yyyy-MM-dd HH:mm:ss}",
                DireccionIP = ipAddress
            }, ct);

            return new ResultadoLogin
            {
                Exitoso = false,
                Mensaje = "La cuenta se encuentra temporalmente bloqueada por exceso de intentos fallidos. Intente más tarde."
            };
        }

        if (!usuario.Activo)
        {
            return new ResultadoLogin { Exitoso = false, Mensaje = "Esta cuenta de usuario se encuentra inactiva." };
        }

        bool passwordValido = _cryptoService.VerifyPassword(dto.Password, usuario.PasswordHash, usuario.Salt);

        if (!passwordValido)
        {
            await _usuarioRepo.RegistrarIntentoFallidoSpAsync(usuario.Id, ct);
            await _usuarioRepo.RegistrarAuditoriaSpAsync(new RegistroAuditoria
            {
                UsuarioId = usuario.Id,
                Accion = "LOGIN_FALLIDO",
                Detalle = "Contraseña incorrecta ingresada",
                DireccionIP = ipAddress
            }, ct);

            return new ResultadoLogin { Exitoso = false, Mensaje = "Usuario o contraseña incorrectos." };
        }

        // Login exitoso: Resetear intentos y registrar auditoría
        await _usuarioRepo.ResetearIntentosFallidosSpAsync(usuario.Id, ct);
        await _usuarioRepo.RegistrarAuditoriaSpAsync(new RegistroAuditoria
        {
            UsuarioId = usuario.Id,
            Accion = "LOGIN_EXITOSO",
            Detalle = "Inicio de sesión correcto",
            DireccionIP = ipAddress
        }, ct);

        return new ResultadoLogin
        {
            Exitoso = true,
            UsuarioId = usuario.Id,
            Username = usuario.Username,
            Rol = usuario.Rol.ToString(),
            ProveedorId = usuario.ProveedorId,
            Mensaje = "Autenticación exitosa."
        };
    }
}
