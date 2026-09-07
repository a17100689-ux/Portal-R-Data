using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using PortalProveedores.Application.DTOs;
using PortalProveedores.Application.Interfaces;
using PortalProveedores.Application.Security;
using PortalProveedores.Core.Common;
using PortalProveedores.Core.Entities;

namespace PortalProveedores.Application.Services;

/// <summary>
/// Orquestador de lógica de negocio para la gestión, verificación de catálogo y registro de proveedores.
/// Aplica la regla fundamental: ningún proveedor puede registrarse en el portal si no existe previamente en Cat_Proveedores.
/// </summary>
public class ProveedorService : IProveedorService
{
    private readonly IProveedorRepository _proveedorRepo;
    private readonly ICryptoService _cryptoService;
    private readonly IUsuarioRepository _usuarioRepo;
    private readonly ILogger<ProveedorService> _logger;

    private static readonly Regex RfcRegex = new(
        @"^[A-Za-zÑñ&]{3,4}\d{6}[A-Za-z\d]{3}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public ProveedorService(
        IProveedorRepository proveedorRepo,
        ICryptoService cryptoService,
        IUsuarioRepository usuarioRepo,
        ILogger<ProveedorService> logger)
    {
        _proveedorRepo = proveedorRepo;
        _cryptoService = cryptoService;
        _usuarioRepo = usuarioRepo;
        _logger = logger;
    }

    /// <summary>
    /// Consulta el catálogo oficial (Cat_Proveedores) para validar si el proveedor es un socio comercial
    /// registrado en el ERP de Radial Llantas y verifica si ya cuenta con credenciales activas en el portal.
    /// </summary>
    public async Task<ApiResponse<VerificarProveedorCatalogoDto>> VerificarEnCatalogoAsync(
        string? rfc,
        string? codigoProveedor,
        CancellationToken ct = default)
    {
        string? rfcLimpio = !string.IsNullOrWhiteSpace(rfc) ? rfc.Trim().ToUpperInvariant() : null;
        string? codigoLimpio = !string.IsNullOrWhiteSpace(codigoProveedor) ? codigoProveedor.Trim().ToUpperInvariant() : null;

        if (string.IsNullOrWhiteSpace(rfcLimpio) && string.IsNullOrWhiteSpace(codigoLimpio))
        {
            return ApiResponse<VerificarProveedorCatalogoDto>.Fail(
                "Debe proporcionar al menos el RFC o el Código de Proveedor para consultar el catálogo.",
                statusCode: 400);
        }

        var resultado = await _proveedorRepo.VerificarEnCatalogoSpAsync(rfcLimpio, codigoLimpio, ct);
        return ApiResponse<VerificarProveedorCatalogoDto>.Ok(resultado, resultado.Mensaje);
    }

    /// <summary>
    /// Búsqueda sargable y paginada de proveedores en Cat_Proveedores para el buscador reactivo del portal.
    /// </summary>
    public async Task<ApiResponse<PaginatedResult<ProveedorCatalogoItemDto>>> BuscarEnCatalogoAsync(
        string? termino,
        int pagina = 1,
        int tamanoPagina = 20,
        CancellationToken ct = default)
    {
        string? terminoLimpio = !string.IsNullOrWhiteSpace(termino) ? termino.Trim() : null;
        pagina = pagina < 1 ? 1 : pagina;
        tamanoPagina = tamanoPagina switch { < 1 => 20, > 100 => 100, _ => tamanoPagina };

        var resultado = await _proveedorRepo.BuscarEnCatalogoSpAsync(terminoLimpio, pagina, tamanoPagina, ct);
        return ApiResponse<PaginatedResult<ProveedorCatalogoItemDto>>.Ok(
            resultado,
            $"Se encontraron {resultado.TotalRecords} proveedores en el catálogo.");
    }

    /// <summary>
    /// Valida y registra un proveedor en el portal.
    /// Exige obligatoriamente que el proveedor exista en Cat_Proveedores y que no tenga ya un usuario activo.
    /// </summary>
    public async Task<ApiResponse<ResultadoCrearProveedorDto>> RegistrarProveedorAsync(
        CrearProveedorDto dto,
        int adminUsuarioId,
        string ipAddress,
        CancellationToken ct = default)
    {
        // 1. Sanitización de datos
        dto.RFC = dto.RFC?.Trim().ToUpperInvariant() ?? string.Empty;
        dto.CodigoProveedor = dto.CodigoProveedor?.Trim().ToUpperInvariant() ?? string.Empty;
        dto.Email = dto.Email?.Trim().ToLowerInvariant() ?? string.Empty;
        dto.RazonSocial = dto.RazonSocial?.Trim().ToUpperInvariant() ?? string.Empty;

        // 2. Validaciones de formato previas a base de datos
        if (string.IsNullOrWhiteSpace(dto.RFC))
        {
            return ApiResponse<ResultadoCrearProveedorDto>.Fail("El RFC del proveedor es obligatorio.", statusCode: 400);
        }

        if (!RfcRegex.IsMatch(dto.RFC))
        {
            return ApiResponse<ResultadoCrearProveedorDto>.Fail("El RFC ingresado no cumple con el formato fiscal válido del SAT (12 o 13 caracteres).", statusCode: 400);
        }

        if (string.IsNullOrWhiteSpace(dto.CodigoProveedor))
        {
            return ApiResponse<ResultadoCrearProveedorDto>.Fail("El código interno de proveedor es obligatorio.", statusCode: 400);
        }

        if (string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 8)
        {
            return ApiResponse<ResultadoCrearProveedorDto>.Fail("La contraseña debe tener una longitud mínima de 8 caracteres.", statusCode: 400);
        }

        // 3. REGLA FUNDAMENTAL DE NEGOCIO:
        // No se puede registrar un proveedor en el portal si no se encuentra en nuestro Cat_Proveedores.
        var verificacion = await _proveedorRepo.VerificarEnCatalogoSpAsync(dto.RFC, dto.CodigoProveedor, ct);
        if (!verificacion.EnCatalogo)
        {
            _logger.LogWarning("Rechazo de registro: Proveedor no existe en Cat_Proveedores. RFC: {RFC}, Código: {Codigo}", dto.RFC, dto.CodigoProveedor);
            return ApiResponse<ResultadoCrearProveedorDto>.Fail(
                $"No se puede registrar el proveedor en el portal porque no se encuentra registrado en el Catálogo de Proveedores de Radial Llantas (Cat_Proveedores). Para tener acceso al portal, el proveedor debe existir previamente en el sistema ERP central.",
                statusCode: 400
            );
        }

        if (!verificacion.Activo)
        {
            _logger.LogWarning("Rechazo de registro: Proveedor inactivo en Cat_Proveedores. RFC: {RFC}", dto.RFC);
            return ApiResponse<ResultadoCrearProveedorDto>.Fail(
                $"El proveedor '{verificacion.RazonSocial}' existe en el catálogo pero se encuentra inactivo. Contacte al área de Compras o Cuentas por Pagar.",
                statusCode: 400
            );
        }

        if (verificacion.TieneUsuarioRegistrado)
        {
            _logger.LogInformation("Rechazo de registro: Proveedor ya cuenta con usuario activo. RFC: {RFC}, Email: {Email}", dto.RFC, verificacion.EmailRegistrado);
            return ApiResponse<ResultadoCrearProveedorDto>.Fail(
                $"El proveedor '{verificacion.RazonSocial}' ya cuenta con una cuenta de usuario en el portal ({verificacion.EmailRegistrado}). Inicie sesión o utilice la recuperación de contraseña.",
                statusCode: 409
            );
        }

        // Auto-completar datos oficiales del catálogo si no venían en la solicitud
        if (string.IsNullOrWhiteSpace(dto.RazonSocial) && !string.IsNullOrWhiteSpace(verificacion.RazonSocial))
        {
            dto.RazonSocial = verificacion.RazonSocial;
        }
        if (string.IsNullOrWhiteSpace(dto.CodigoPostal) && !string.IsNullOrWhiteSpace(verificacion.CodigoPostal))
        {
            dto.CodigoPostal = verificacion.CodigoPostal;
        }
        if (string.IsNullOrWhiteSpace(dto.Telefono) && !string.IsNullOrWhiteSpace(verificacion.Telefono))
        {
            dto.Telefono = verificacion.Telefono;
        }
        if (string.IsNullOrWhiteSpace(dto.Email) && !string.IsNullOrWhiteSpace(verificacion.EmailContacto))
        {
            dto.Email = verificacion.EmailContacto.Trim().ToLowerInvariant();
        }
        if (string.IsNullOrWhiteSpace(dto.RegimenFiscal) && !string.IsNullOrWhiteSpace(verificacion.RegimenFiscal))
        {
            dto.RegimenFiscal = verificacion.RegimenFiscal;
        }
        if (string.IsNullOrWhiteSpace(dto.CondicionesPago) && !string.IsNullOrWhiteSpace(verificacion.CondicionesPago))
        {
            dto.CondicionesPago = verificacion.CondicionesPago;
        }

        // Validar formato de correo final
        if (string.IsNullOrWhiteSpace(dto.Email) || !dto.Email.Contains('@'))
        {
            return ApiResponse<ResultadoCrearProveedorDto>.Fail("El correo electrónico es obligatorio y debe tener un formato válido.", statusCode: 400);
        }

        // 4. Hashing de contraseña con algoritmo criptográfico robusto (PBKDF2/SHA-512)
        string passwordHash = _cryptoService.HashPassword(dto.Password);

        // 5. Persistencia mediante el repositorio (invoca Stored Procedures)
        var resultado = await _proveedorRepo.CrearProveedorCompletoAsync(dto, passwordHash, adminUsuarioId, ipAddress, ct);
        if (!resultado.Exitoso)
        {
            return ApiResponse<ResultadoCrearProveedorDto>.Fail(resultado.Mensaje, statusCode: 400);
        }

        _logger.LogInformation(
            "Proveedor registrado y habilitado exitosamente en el portal. ProveedorId: {ProveedorId}, UsuarioId: {UsuarioId}, RFC: {RFC}",
            resultado.ProveedorId, resultado.UsuarioId, resultado.RFC);

        return ApiResponse<ResultadoCrearProveedorDto>.Ok(resultado, resultado.Mensaje, statusCode: 201);
    }

    public async Task<Proveedor?> ObtenerPorIdAsync(int id, CancellationToken ct = default)
    {
        return await _proveedorRepo.ObtenerPorIdSpAsync(id, ct);
    }
}
