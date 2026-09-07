using Microsoft.Extensions.Logging;
using Moq;
using PortalProveedores.Application.DTOs;
using PortalProveedores.Application.Interfaces;
using PortalProveedores.Application.Security;
using PortalProveedores.Application.Services;
using PortalProveedores.Core.Common;
using Xunit;

namespace PortalProveedores.UnitTests;

public class ProveedorServiceTests
{
    private readonly Mock<IProveedorRepository> _proveedorRepoMock = new();
    private readonly Mock<ICryptoService> _cryptoServiceMock = new();
    private readonly Mock<IUsuarioRepository> _usuarioRepoMock = new();
    private readonly Mock<ILogger<ProveedorService>> _loggerMock = new();

    private readonly ProveedorService _service;

    public ProveedorServiceTests()
    {
        _service = new ProveedorService(
            _proveedorRepoMock.Object,
            _cryptoServiceMock.Object,
            _usuarioRepoMock.Object,
            _loggerMock.Object
        );
    }

    [Fact]
    public async Task RegistrarProveedor_CuandoNoExisteEnCatProveedores_DebeRetornarErrorYNoRegistrar()
    {
        // Arrange
        var dto = new CrearProveedorDto
        {
            RFC = "PRV920415XXX",
            CodigoProveedor = "PRV-99999",
            Email = "contacto@proveedor.com",
            Password = "PasswordSeguro123!"
        };

        _proveedorRepoMock.Setup(r => r.VerificarEnCatalogoSpAsync(dto.RFC, dto.CodigoProveedor, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VerificarProveedorCatalogoDto
            {
                EnCatalogo = false,
                Mensaje = "No existe en Cat_Proveedores"
            });

        // Act
        var response = await _service.RegistrarProveedorAsync(dto, 1, "127.0.0.1");

        // Assert
        Assert.False(response.Success);
        Assert.Equal(400, response.StatusCode);
        Assert.Contains("Catálogo de Proveedores de Radial Llantas (Cat_Proveedores)", response.Message);
        _proveedorRepoMock.Verify(r => r.CrearProveedorCompletoAsync(It.IsAny<CrearProveedorDto>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegistrarProveedor_CuandoExisteEnCatProveedoresPeroEstaInactivo_DebeRetornarError()
    {
        // Arrange
        var dto = new CrearProveedorDto
        {
            RFC = "PRV920415XXX",
            CodigoProveedor = "PRV-00100",
            Email = "contacto@proveedor.com",
            Password = "PasswordSeguro123!"
        };

        _proveedorRepoMock.Setup(r => r.VerificarEnCatalogoSpAsync(dto.RFC, dto.CodigoProveedor, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VerificarProveedorCatalogoDto
            {
                EnCatalogo = true,
                ProveedorId = 10,
                RazonSocial = "LLANTAS INACTIVAS SA DE CV",
                Activo = false
            });

        // Act
        var response = await _service.RegistrarProveedorAsync(dto, 1, "127.0.0.1");

        // Assert
        Assert.False(response.Success);
        Assert.Equal(400, response.StatusCode);
        Assert.Contains("inactivo", response.Message);
        _proveedorRepoMock.Verify(r => r.CrearProveedorCompletoAsync(It.IsAny<CrearProveedorDto>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegistrarProveedor_CuandoProveedorYaTieneUsuarioActivo_DebeRetornarConflicto409()
    {
        // Arrange
        var dto = new CrearProveedorDto
        {
            RFC = "PRV920415XXX",
            CodigoProveedor = "PRV-00100",
            Email = "contacto@proveedor.com",
            Password = "PasswordSeguro123!"
        };

        _proveedorRepoMock.Setup(r => r.VerificarEnCatalogoSpAsync(dto.RFC, dto.CodigoProveedor, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VerificarProveedorCatalogoDto
            {
                EnCatalogo = true,
                ProveedorId = 10,
                RazonSocial = "DISTRIBUIDORA DE NEUMATICOS SA",
                Activo = true,
                TieneUsuarioRegistrado = true,
                EmailRegistrado = "admin@proveedor.com"
            });

        // Act
        var response = await _service.RegistrarProveedorAsync(dto, 1, "127.0.0.1");

        // Assert
        Assert.False(response.Success);
        Assert.Equal(409, response.StatusCode);
        Assert.Contains("ya cuenta con una cuenta de usuario", response.Message);
        _proveedorRepoMock.Verify(r => r.CrearProveedorCompletoAsync(It.IsAny<CrearProveedorDto>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegistrarProveedor_CuandoProveedorEsValidoEnCatProveedores_DebeRegistrarExitosamente201()
    {
        // Arrange
        var dto = new CrearProveedorDto
        {
            RFC = "PRV920415XXX",
            CodigoProveedor = "PRV-00100",
            Email = "nuevo@proveedor.com",
            Password = "PasswordSeguro123!"
        };

        _proveedorRepoMock.Setup(r => r.VerificarEnCatalogoSpAsync(dto.RFC, dto.CodigoProveedor, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VerificarProveedorCatalogoDto
            {
                EnCatalogo = true,
                ProveedorId = 25,
                CodigoProveedor = "PRV-00100",
                RFC = "PRV920415XXX",
                RazonSocial = "MICHELIN DISTRIBUIDOR SA DE CV",
                Activo = true,
                TieneUsuarioRegistrado = false
            });

        _cryptoServiceMock.Setup(c => c.HashPassword(dto.Password))
            .Returns("HASH_SEGURO_PBKDF2");

        _proveedorRepoMock.Setup(r => r.CrearProveedorCompletoAsync(
                It.IsAny<CrearProveedorDto>(),
                "HASH_SEGURO_PBKDF2",
                1,
                "127.0.0.1",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResultadoCrearProveedorDto
            {
                Exitoso = true,
                ProveedorId = 25,
                UsuarioId = 100,
                CodigoProveedor = "PRV-00100",
                RFC = "PRV920415XXX",
                Mensaje = "Proveedor habilitado con éxito."
            });

        // Act
        var response = await _service.RegistrarProveedorAsync(dto, 1, "127.0.0.1");

        // Assert
        Assert.True(response.Success);
        Assert.Equal(201, response.StatusCode);
        Assert.NotNull(response.Data);
        Assert.Equal(25, response.Data.ProveedorId);
        Assert.Equal(100, response.Data.UsuarioId);
    }

    [Fact]
    public async Task VerificarEnCatalogo_CuandoExiste_DebeRetornarDatosDeCatalogo()
    {
        // Arrange
        _proveedorRepoMock.Setup(r => r.VerificarEnCatalogoSpAsync("PRV920415XXX", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VerificarProveedorCatalogoDto
            {
                EnCatalogo = true,
                ProveedorId = 15,
                CodigoProveedor = "PRV-00015",
                RFC = "PRV920415XXX",
                RazonSocial = "LLANTAS Y RINES DE OCCIDENTE SA",
                Activo = true,
                TieneUsuarioRegistrado = false
            });

        // Act
        var response = await _service.VerificarEnCatalogoAsync("PRV920415XXX", null);

        // Assert
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.True(response.Data.EnCatalogo);
        Assert.Equal("LLANTAS Y RINES DE OCCIDENTE SA", response.Data.RazonSocial);
    }

    [Fact]
    public async Task BuscarEnCatalogo_DebeRetornarListaPaginadaDeProveedores()
    {
        // Arrange
        var itemsEsperados = new List<ProveedorCatalogoItemDto>
        {
            new()
            {
                ProveedorId = 1,
                CodigoProveedor = "PRV-00001",
                RFC = "MIC850101XYZ",
                RazonSocial = "MICHELIN MEXICO SERVICES SA DE CV",
                Activo = true,
                TieneUsuarioRegistrado = false
            },
            new()
            {
                ProveedorId = 2,
                CodigoProveedor = "PRV-00002",
                RFC = "BRI901010ABC",
                RazonSocial = "BRIDGESTONE DE MEXICO SA DE CV",
                Activo = true,
                TieneUsuarioRegistrado = true,
                EmailRegistrado = "admin@bridgestone.com"
            }
        };

        var paginacion = new PaginatedResult<ProveedorCatalogoItemDto>(itemsEsperados, 2, 1, 10);

        _proveedorRepoMock.Setup(r => r.BuscarEnCatalogoSpAsync("MICHELIN", 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginacion);

        // Act
        var response = await _service.BuscarEnCatalogoAsync("MICHELIN", 1, 10);

        // Assert
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal(2, response.Data.TotalRecords);
        Assert.Equal(2, response.Data.Items.Count);
        Assert.Equal("PRV-00001", response.Data.Items.ElementAt(0).CodigoProveedor);
        Assert.False(response.Data.Items.ElementAt(0).TieneUsuarioRegistrado);
        Assert.True(response.Data.Items.ElementAt(1).TieneUsuarioRegistrado);
    }

    [Fact]
    public async Task RegistrarProveedor_CuandoCamposDeContactoVienenVacios_DebeAutocompletarlosDesdeCatProveedores()
    {
        // Arrange
        var dto = new CrearProveedorDto
        {
            RFC = "AOF870529IU7",
            CodigoProveedor = "1000126",
            Password = "PasswordSeguro123!",
            // Dejar en blanco campos que deben autocompletarse
            RazonSocial = "",
            CodigoPostal = "",
            Telefono = "",
            Email = "",
            RegimenFiscal = ""
        };

        _proveedorRepoMock.Setup(r => r.VerificarEnCatalogoSpAsync(dto.RFC, dto.CodigoProveedor, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VerificarProveedorCatalogoDto
            {
                EnCatalogo = true,
                ProveedorId = 1499,
                CodigoProveedor = "1000126",
                RFC = "AOF870529IU7",
                RazonSocial = "ABASTECEDORA DE OFICINAS S.A. DE C.V.",
                CodigoPostal = "64000",
                Telefono = "818 158 15 00",
                EmailContacto = "vtaesp1@adosa.com.mx",
                RegimenFiscal = "601",
                CondicionesPago = "30",
                RequiereValidarCompra = false,
                OrdenCompraObligatoria = false,
                EsProveedorNacional = false,
                Activo = true,
                TieneUsuarioRegistrado = false
            });

        _cryptoServiceMock.Setup(c => c.HashPassword(dto.Password))
            .Returns("HASH_SEGURO_PBKDF2");

        CrearProveedorDto? dtoPersistido = null;
        _proveedorRepoMock.Setup(r => r.CrearProveedorCompletoAsync(
                It.IsAny<CrearProveedorDto>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<CrearProveedorDto, string, int, string, CancellationToken>((d, _, _, _, _) => dtoPersistido = d)
            .ReturnsAsync(new ResultadoCrearProveedorDto
            {
                Exitoso = true,
                ProveedorId = 1499,
                UsuarioId = 200,
                CodigoProveedor = "1000126",
                RFC = "AOF870529IU7"
            });

        // Act
        var response = await _service.RegistrarProveedorAsync(dto, 1, "127.0.0.1");

        // Assert
        Assert.True(response.Success);
        Assert.NotNull(dtoPersistido);
        Assert.Equal("ABASTECEDORA DE OFICINAS S.A. DE C.V.", dtoPersistido.RazonSocial);
        Assert.Equal("64000", dtoPersistido.CodigoPostal);
        Assert.Equal("818 158 15 00", dtoPersistido.Telefono);
        Assert.Equal("vtaesp1@adosa.com.mx", dtoPersistido.Email);
        Assert.Equal("601", dtoPersistido.RegimenFiscal);
    }
}
