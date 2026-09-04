using Moq;
using PortalProveedores.Application.DTOs;
using PortalProveedores.Application.Interfaces;
using PortalProveedores.Application.Security;
using PortalProveedores.Application.Services;
using PortalProveedores.Core.Entities;
using Xunit;

namespace PortalProveedores.UnitTests;

public class AuthServiceTests
{
    private readonly Mock<IUsuarioRepository> _usuarioRepoMock = new();
    private readonly Mock<ICryptoService> _cryptoServiceMock = new();
    private readonly AuthService _authService;

    public AuthServiceTests()
    {
        _authService = new AuthService(_usuarioRepoMock.Object, _cryptoServiceMock.Object);
    }

    [Fact]
    public async Task ValidarCredencialesAsync_AdminValido_DebeRetornarExito()
    {
        // Arrange
        var admin = new UsuarioAdministrador
        {
            AdminId = 1,
            Email = "admin@portalrdata.com",
            PasswordHash = "stored-hash",
            Rol = "ADMIN",
            Activo = true
        };

        _usuarioRepoMock.Setup(r => r.ObtenerAdminPorLoginSpAsync("admin@portalrdata.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(admin);

        _cryptoServiceMock.Setup(c => c.VerifyPassword("Admin@Portal2026!", "stored-hash"))
            .Returns(true);

        var dto = new LoginDto { Username = "admin@portalrdata.com", Password = "Admin@Portal2026!" };

        // Act
        var resultado = await _authService.ValidarCredencialesAsync(dto, "10.0.0.1");

        // Assert
        Assert.True(resultado.Exitoso);
        Assert.True(resultado.EsAdmin);
        Assert.Equal("Administrador", resultado.Rol);
        Assert.Equal(1, resultado.UsuarioId);
        _usuarioRepoMock.Verify(r => r.RegistrarAuditoriaSpAsync(1, "SEGURIDAD", "LOGIN_EXITOSO", It.IsAny<string>(), "10.0.0.1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ValidarCredencialesAsync_AdminPasswordInvalido_DebeRetornarErrorYAuditar()
    {
        // Arrange
        var admin = new UsuarioAdministrador
        {
            AdminId = 1,
            Email = "admin@portalrdata.com",
            PasswordHash = "stored-hash",
            Rol = "ADMIN",
            Activo = true
        };

        _usuarioRepoMock.Setup(r => r.ObtenerAdminPorLoginSpAsync("admin@portalrdata.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(admin);

        _cryptoServiceMock.Setup(c => c.VerifyPassword("WrongPassword", "stored-hash"))
            .Returns(false);

        var dto = new LoginDto { Username = "admin@portalrdata.com", Password = "WrongPassword" };

        // Act
        var resultado = await _authService.ValidarCredencialesAsync(dto, "10.0.0.1");

        // Assert
        Assert.False(resultado.Exitoso);
        Assert.Contains("incorrectos", resultado.Mensaje);
        _usuarioRepoMock.Verify(r => r.RegistrarAuditoriaSpAsync(1, "SEGURIDAD", "LOGIN_FALLIDO", It.IsAny<string>(), "10.0.0.1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ValidarCredencialesAsync_ProveedorValido_DebeRetornarExitoYRegistrarLogin()
    {
        // Arrange
        var proveedor = new UsuarioProveedor
        {
            UsuarioId = 10,
            ProveedorId = 5,
            CodigoProveedor = "PRV-DEMO",
            RFC = "AAA010101AAA",
            Email = "proveedor@demo.com",
            PasswordHash = "stored-prov-hash",
            UsuarioActivo = true,
            ProveedorActivo = true
        };

        _usuarioRepoMock.Setup(r => r.ObtenerAdminPorLoginSpAsync("proveedor@demo.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((UsuarioAdministrador?)null);

        _usuarioRepoMock.Setup(r => r.ObtenerProveedorPorLoginSpAsync("proveedor@demo.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(proveedor);

        _cryptoServiceMock.Setup(c => c.VerifyPassword("Prov@2026", "stored-prov-hash"))
            .Returns(true);

        var dto = new LoginDto { Username = "proveedor@demo.com", Password = "Prov@2026" };

        // Act
        var resultado = await _authService.ValidarCredencialesAsync(dto, "192.168.1.100");

        // Assert
        Assert.True(resultado.Exitoso);
        Assert.False(resultado.EsAdmin);
        Assert.Equal("Proveedor", resultado.Rol);
        Assert.Equal(5, resultado.ProveedorId);
        Assert.Equal("AAA010101AAA", resultado.RFC);
        _usuarioRepoMock.Verify(r => r.RegistrarLoginExitosoSpAsync(10, "192.168.1.100", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ValidarCredencialesAsync_ProveedorBloqueado_DebeRechazarAcceso()
    {
        // Arrange
        var proveedor = new UsuarioProveedor
        {
            UsuarioId = 10,
            Email = "bloqueado@demo.com",
            BloqueadoHasta = DateTime.UtcNow.AddMinutes(10),
            UsuarioActivo = true,
            ProveedorActivo = true
        };

        _usuarioRepoMock.Setup(r => r.ObtenerAdminPorLoginSpAsync("bloqueado@demo.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((UsuarioAdministrador?)null);

        _usuarioRepoMock.Setup(r => r.ObtenerProveedorPorLoginSpAsync("bloqueado@demo.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(proveedor);

        var dto = new LoginDto { Username = "bloqueado@demo.com", Password = "any" };

        // Act
        var resultado = await _authService.ValidarCredencialesAsync(dto, "192.168.1.100");

        // Assert
        Assert.False(resultado.Exitoso);
        Assert.Contains("bloqueada", resultado.Mensaje);
        _cryptoServiceMock.Verify(c => c.VerifyPassword(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }
}
