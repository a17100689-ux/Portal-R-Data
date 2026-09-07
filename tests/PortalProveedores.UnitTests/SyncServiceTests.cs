using Microsoft.Extensions.Logging;
using Moq;
using PortalProveedores.Application.DTOs;
using PortalProveedores.Application.Interfaces;
using PortalProveedores.Application.Services;
using Xunit;

namespace PortalProveedores.UnitTests;

public class SyncServiceTests
{
    private readonly Mock<ISyncRepository> _syncRepoMock = new();
    private readonly Mock<ILogger<SyncService>> _loggerMock = new();
    private readonly SyncService _syncService;

    public SyncServiceTests()
    {
        _syncService = new SyncService(_syncRepoMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task ProcesarColaFacturas_CuandoNoHayPendientes_DebeRetornarLoteVacio()
    {
        // Arrange
        _syncRepoMock.Setup(r => r.ObtenerLotePendienteSpAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<SyncColaItemDto>());

        // Act
        var resultado = await _syncService.ProcesarColaFacturasAsync(10);

        // Assert
        Assert.NotNull(resultado);
        Assert.Equal(0, resultado.TotalLote);
        Assert.Equal(0, resultado.Exitosos);
        Assert.Equal(0, resultado.Fallidos);
        _syncRepoMock.Verify(r => r.EjecutarInsertCentralSpAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcesarColaFacturas_CuandoFacturaValida_DebeInsertarEnCentralYConfirmar()
    {
        // Arrange
        var item = new SyncColaItemDto
        {
            SyncId = 101,
            FacturaId = 202,
            UUID = "UUID-TEST-1234",
            FolioFactura = "F-555",
            CodigoProveedor = "1001707"
        };

        _syncRepoMock.Setup(r => r.ObtenerLotePendienteSpAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SyncColaItemDto> { item });

        _syncRepoMock.Setup(r => r.EjecutarInsertCentralSpAsync(item.FacturaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(889900m); // NumeroDocumentoCentral

        _syncRepoMock.Setup(r => r.ConfirmarSincronizacionSpAsync(item.SyncId, item.FacturaId, 889900m, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var resultado = await _syncService.ProcesarColaFacturasAsync(10);

        // Assert
        Assert.Equal(1, resultado.TotalLote);
        Assert.Equal(1, resultado.Exitosos);
        Assert.Equal(0, resultado.Fallidos);
        Assert.Contains("DocCentral: 889900", resultado.Detalles.First());

        _syncRepoMock.Verify(r => r.EjecutarInsertCentralSpAsync(item.FacturaId, It.IsAny<CancellationToken>()), Times.Once);
        _syncRepoMock.Verify(r => r.ConfirmarSincronizacionSpAsync(item.SyncId, item.FacturaId, 889900m, It.IsAny<CancellationToken>()), Times.Once);
        _syncRepoMock.Verify(r => r.RegistrarFalloSpAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcesarColaFacturas_CuandoFallaInsertCentral_DebeRegistrarFalloEnCola()
    {
        // Arrange
        var item = new SyncColaItemDto
        {
            SyncId = 105,
            FacturaId = 205,
            UUID = "UUID-FAIL-9999",
            FolioFactura = "F-ERR",
            CodigoProveedor = "1001707"
        };

        _syncRepoMock.Setup(r => r.ObtenerLotePendienteSpAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SyncColaItemDto> { item });

        _syncRepoMock.Setup(r => r.EjecutarInsertCentralSpAsync(item.FacturaId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Fallo de conexión simulado con ERP central"));

        // Act
        var resultado = await _syncService.ProcesarColaFacturasAsync(10);

        // Assert
        Assert.Equal(1, resultado.TotalLote);
        Assert.Equal(0, resultado.Exitosos);
        Assert.Equal(1, resultado.Fallidos);
        Assert.Contains("Fallo de conexión simulado con ERP central", resultado.Detalles.First());

        _syncRepoMock.Verify(r => r.RegistrarFalloSpAsync(item.SyncId, item.FacturaId, It.Is<string>(s => s.Contains("Fallo de conexión")), It.IsAny<CancellationToken>()), Times.Once);
        _syncRepoMock.Verify(r => r.ConfirmarSincronizacionSpAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RefrescarCatalogos_DebeInvocarRepositorioYRetornarMensaje()
    {
        // Arrange
        _syncRepoMock.Setup(r => r.RefrescarCatalogosDesdeCentralSpAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("Catálogos sincronizados exitosamente");

        // Act
        var resultado = await _syncService.RefrescarCatalogosAsync();

        // Assert
        Assert.True(resultado.Exitoso);
        Assert.Equal("Catálogos sincronizados exitosamente", resultado.Mensaje);
        _syncRepoMock.Verify(r => r.RefrescarCatalogosDesdeCentralSpAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ActualizarEstatusEntregas_DebeRetornarCantidadDeFacturasActualizadas()
    {
        // Arrange
        _syncRepoMock.Setup(r => r.ActualizarEstatusEntregaMercanciaSpAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        // Act
        int actualizadas = await _syncService.ActualizarEstatusEntregasAsync();

        // Assert
        Assert.Equal(3, actualizadas);
        _syncRepoMock.Verify(r => r.ActualizarEstatusEntregaMercanciaSpAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
