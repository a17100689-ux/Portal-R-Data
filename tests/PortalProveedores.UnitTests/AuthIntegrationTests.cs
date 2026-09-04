using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using PortalProveedores.Application.DTOs;
using PortalProveedores.Application.Security;
using PortalProveedores.Application.Services;
using PortalProveedores.Infrastructure.Data;
using Xunit;

namespace PortalProveedores.UnitTests;

[Trait("Category", "Integration")]
public class AuthIntegrationTests
{
    private const string ConnectionStringWeb = "Server=10.0.3.5;Database=PortalProveedores_DB;User Id=PortalAppLogin;Password=P0rt@l_Pr0v33d0r3s_2026!Sec#Web;TrustServerCertificate=True;Encrypt=True;";
    private const string ConnectionStringAdmin = "Server=10.0.3.5;Database=PortalProveedores_DB;User Id=ekt;Password=3qT3l3515-Erp;TrustServerCertificate=True;Connect Timeout=20;";

    private readonly AuthService _authService;
    private readonly CryptoService _cryptoService;

    public AuthIntegrationTests()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = ConnectionStringWeb
            })
            .Build();

        var repo = new SqlUsuarioRepository(config);
        _cryptoService = new CryptoService();
        _authService = new AuthService(repo, _cryptoService);
    }

    [Fact]
    public async Task Admin_Login_ConCredencialesValidas_DebeRetornarExitoso()
    {
        var dto = new LoginDto
        {
            Username = "admin@radial.com.mx",
            Password = "AdminPortal2026!#"
        };

        var resultado = await _authService.ValidarCredencialesAsync(dto, "127.0.0.1");

        Assert.True(resultado.Exitoso, resultado.Mensaje);
        Assert.Equal("ADMIN", resultado.Rol);
        Assert.True(resultado.EsAdmin);
        Assert.NotNull(resultado.UsuarioId);
        Assert.Equal("admin@radial.com.mx", resultado.Email);
    }

    [Fact]
    public async Task Proveedor_Login_PorRFC_Y_PorEmail_DebeRetornarExitoso()
    {
        // 1. Obtener datos de un proveedor activo y asegurar usuario en DB
        int provId;
        string provRfc;
        string provCod;
        string provRazon;

        using (var conn = new SqlConnection(ConnectionStringAdmin))
        {
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT TOP (1) ProveedorId, CodigoProveedor, RFC, RazonSocial FROM dbo.Cat_Proveedores WHERE Activo = 1;";
            using var reader = await cmd.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync(), "Debe existir al menos un proveedor activo en Cat_Proveedores");
            provId = reader.GetInt32(0);
            provCod = reader.GetString(1);
            provRfc = reader.GetString(2);
            provRazon = reader.GetString(3);
        }

        string rawPassword = "ProvDemo2026!#";
        string combinedHash = _cryptoService.HashPasswordCombined(rawPassword);
        string provEmail = "demo.prov.test@radial.com.mx";

        using (var conn = new SqlConnection(ConnectionStringAdmin))
        {
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
            IF NOT EXISTS (SELECT 1 FROM dbo.Usuarios_Proveedor WHERE Email = @Email OR RFC = @RFC)
            BEGIN
                INSERT INTO dbo.Usuarios_Proveedor (
                    ProveedorId, RFC, Email, PasswordHash, IntentosFallidos, BloqueadoHasta, Activo, CreatedAt, UpdatedAt
                )
                VALUES (
                    @ProveedorId, @RFC, @Email, @Hash, 0, NULL, 1, SYSUTCDATETIME(), SYSUTCDATETIME()
                );
            END
            ELSE
            BEGIN
                UPDATE dbo.Usuarios_Proveedor
                SET PasswordHash = @Hash, IntentosFallidos = 0, BloqueadoHasta = NULL, Activo = 1, UpdatedAt = SYSUTCDATETIME()
                WHERE Email = @Email OR RFC = @RFC;
            END";
            cmd.Parameters.AddWithValue("@ProveedorId", provId);
            cmd.Parameters.AddWithValue("@RFC", provRfc);
            cmd.Parameters.AddWithValue("@Email", provEmail);
            cmd.Parameters.AddWithValue("@Hash", combinedHash);
            await cmd.ExecuteNonQueryAsync();
        }

        // 2. Login por RFC
        var dtoRfc = new LoginDto { Username = provRfc, Password = rawPassword };
        var resRfc = await _authService.ValidarCredencialesAsync(dtoRfc, "127.0.0.1");

        Assert.True(resRfc.Exitoso, resRfc.Mensaje);
        Assert.Equal("Proveedor", resRfc.Rol);
        Assert.False(resRfc.EsAdmin);
        Assert.Equal(provId, resRfc.ProveedorId);
        Assert.Equal(provCod, resRfc.CodigoProveedor);

        // 3. Login por Email
        var dtoEmail = new LoginDto { Username = provEmail, Password = rawPassword };
        var resEmail = await _authService.ValidarCredencialesAsync(dtoEmail, "127.0.0.1");

        Assert.True(resEmail.Exitoso, resEmail.Mensaje);
        Assert.Equal(provId, resEmail.ProveedorId);
    }

    [Fact]
    public async Task Login_ContrasenaIncorrecta_CincoVeces_DebeBloquearCuenta()
    {
        // 1. Obtener RFC del proveedor de pruebas y asegurar que no esté bloqueado
        string provEmail = "demo.prov.test@radial.com.mx";
        using (var conn = new SqlConnection(ConnectionStringAdmin))
        {
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE dbo.Usuarios_Proveedor SET IntentosFallidos = 0, BloqueadoHasta = NULL WHERE Email = @Email;";
            cmd.Parameters.AddWithValue("@Email", provEmail);
            await cmd.ExecuteNonQueryAsync();
        }

        var dtoErroneo = new LoginDto
        {
            Username = provEmail,
            Password = "PasswordEquivocada123!"
        };

        // Ejecutar 5 intentos fallidos
        for (int i = 0; i < 5; i++)
        {
            var res = await _authService.ValidarCredencialesAsync(dtoErroneo, "192.168.1.50");
            Assert.False(res.Exitoso);
            Assert.Equal("Usuario o contraseña incorrectos.", res.Mensaje);
        }

        // El 6to intento debe indicar cuenta bloqueada
        var resBloqueado = await _authService.ValidarCredencialesAsync(dtoErroneo, "192.168.1.50");
        Assert.False(resBloqueado.Exitoso);
        Assert.Contains("bloqueada", resBloqueado.Mensaje, StringComparison.OrdinalIgnoreCase);

        // Desbloquear al finalizar
        using (var conn = new SqlConnection(ConnectionStringAdmin))
        {
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE dbo.Usuarios_Proveedor SET IntentosFallidos = 0, BloqueadoHasta = NULL WHERE Email = @Email;";
            cmd.Parameters.AddWithValue("@Email", provEmail);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task Login_UsuarioNoExistente_DebeRetornarMensajeGenerico()
    {
        var dto = new LoginDto
        {
            Username = "usuario_inexistente_999@noexiste.com",
            Password = "CualquierPassword123!"
        };

        var res = await _authService.ValidarCredencialesAsync(dto, "127.0.0.1");

        Assert.False(res.Exitoso);
        Assert.Equal("Usuario o contraseña incorrectos.", res.Mensaje);
    }
}
