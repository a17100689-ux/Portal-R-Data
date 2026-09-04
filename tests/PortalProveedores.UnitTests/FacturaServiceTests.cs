using Microsoft.Extensions.Logging;
using Moq;
using PortalProveedores.Application.DTOs;
using PortalProveedores.Application.Interfaces;
using PortalProveedores.Application.Security;
using PortalProveedores.Application.Services;
using PortalProveedores.Core.Common;
using PortalProveedores.Core.Entities;
using Xunit;

namespace PortalProveedores.UnitTests;

public class FacturaServiceTests
{
    private readonly Mock<IFileSecurityValidator> _fileValidatorMock = new();
    private readonly Mock<ICfdiXmlParser> _xmlParserMock = new();
    private readonly Mock<IStorageService> _storageServiceMock = new();
    private readonly Mock<IFacturaRepository> _facturaRepoMock = new();
    private readonly Mock<IProveedorRepository> _proveedorRepoMock = new();
    private readonly Mock<IUsuarioRepository> _usuarioRepoMock = new();
    private readonly Mock<ILogger<FacturaService>> _loggerMock = new();

    private readonly FacturaService _service;

    public FacturaServiceTests()
    {
        _service = new FacturaService(
            _fileValidatorMock.Object,
            _xmlParserMock.Object,
            _storageServiceMock.Object,
            _facturaRepoMock.Object,
            _proveedorRepoMock.Object,
            _usuarioRepoMock.Object,
            _loggerMock.Object
        );
    }

    [Fact]
    public async Task CargarFacturaAsync_ProveedorInexistente_DebeRetornarError()
    {
        // Arrange
        _proveedorRepoMock.Setup(r => r.ObtenerPorIdSpAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Proveedor?)null);

        var dto = new CargaFacturaDto { ProveedorId = 999 };

        // Act
        var resultado = await _service.CargarFacturaAsync(dto);

        // Assert
        Assert.False(resultado.Exitoso);
        Assert.Contains("proveedor asociado", resultado.Mensaje);
    }

    [Fact]
    public async Task CargarFacturaAsync_RfcEmisorNoCoincideConProveedor_DebeRechazarPorDiscrepanciaFiscal()
    {
        // Arrange
        var proveedor = new Proveedor { Id = 1, RFC = "AAA010101AAA", RazonSocial = "PROVEEDOR 1 SA" };
        _proveedorRepoMock.Setup(r => r.ObtenerPorIdSpAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(proveedor);

        _fileValidatorMock.Setup(v => v.ValidarXml(It.IsAny<Stream>(), It.IsAny<long>(), It.IsAny<string>()))
            .Returns(new ResultadoValidacionArchivo { EsValido = true, TipoDetectado = "application/xml" });

        _fileValidatorMock.Setup(v => v.ValidarPdf(It.IsAny<Stream>(), It.IsAny<long>(), It.IsAny<string>()))
            .Returns(new ResultadoValidacionArchivo { EsValido = true, TipoDetectado = "application/pdf" });

        _xmlParserMock.Setup(p => p.ParsearCfdi(It.IsAny<Stream>()))
            .Returns(new FacturaParsedXml
            {
                UUID = "UUID-12345",
                RFCEmisor = "OTR999999ZZZ", // RFC diferente al del proveedor
                RFCReceptor = "RDA200101XYZ",
                Total = 1500m
            });

        var dto = new CargaFacturaDto
        {
            ProveedorId = 1,
            XmlStream = new MemoryStream(),
            PdfStream = new MemoryStream(),
            XmlNombreOriginal = "fac.xml",
            PdfNombreOriginal = "fac.pdf"
        };

        // Act
        var resultado = await _service.CargarFacturaAsync(dto);

        // Assert
        Assert.False(resultado.Exitoso);
        Assert.Contains("no coincide con su RFC registrado", resultado.Mensaje);
        _facturaRepoMock.Verify(r => r.InsertarFacturaSpAsync(It.IsAny<Factura>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CargarFacturaAsync_UuidDuplicado_DebeRechazarPorDuplicidad()
    {
        // Arrange
        var proveedor = new Proveedor { Id = 1, RFC = "AAA010101AAA", RazonSocial = "PROVEEDOR 1 SA" };
        _proveedorRepoMock.Setup(r => r.ObtenerPorIdSpAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(proveedor);

        _fileValidatorMock.Setup(v => v.ValidarXml(It.IsAny<Stream>(), It.IsAny<long>(), It.IsAny<string>()))
            .Returns(new ResultadoValidacionArchivo { EsValido = true });

        _fileValidatorMock.Setup(v => v.ValidarPdf(It.IsAny<Stream>(), It.IsAny<long>(), It.IsAny<string>()))
            .Returns(new ResultadoValidacionArchivo { EsValido = true });

        _xmlParserMock.Setup(p => p.ParsearCfdi(It.IsAny<Stream>()))
            .Returns(new FacturaParsedXml
            {
                UUID = "UUID-EXISTENTE",
                RFCEmisor = "AAA010101AAA",
                Total = 1000m
            });

        _facturaRepoMock.Setup(r => r.ValidarFacturaPreviaSpAsync(It.IsAny<string>(), It.IsAny<string>(), "UUID-EXISTENTE", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidacionFacturaPreviaDto
            {
                EsValido = false,
                ExisteUUID = true,
                MensajeError = "La factura con Folio Fiscal (UUID) UUID-EXISTENTE ya fue registrada previamente en el portal."
            });

        var dto = new CargaFacturaDto { ProveedorId = 1 };

        // Act
        var resultado = await _service.CargarFacturaAsync(dto);

        // Assert
        Assert.False(resultado.Exitoso);
        Assert.Contains("ya fue registrada previamente", resultado.Mensaje);
        _storageServiceMock.Verify(s => s.GuardarArchivoAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CargarFacturaAsync_FalloEnBaseDeDatos_DebeEjecutarRollbackDeArchivos()
    {
        // Arrange
        var proveedor = new Proveedor { Id = 1, RFC = "AAA010101AAA", RazonSocial = "PROVEEDOR 1 SA" };
        _proveedorRepoMock.Setup(r => r.ObtenerPorIdSpAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(proveedor);

        _fileValidatorMock.Setup(v => v.ValidarXml(It.IsAny<Stream>(), It.IsAny<long>(), It.IsAny<string>()))
            .Returns(new ResultadoValidacionArchivo { EsValido = true });

        _fileValidatorMock.Setup(v => v.ValidarPdf(It.IsAny<Stream>(), It.IsAny<long>(), It.IsAny<string>()))
            .Returns(new ResultadoValidacionArchivo { EsValido = true });

        _xmlParserMock.Setup(p => p.ParsearCfdi(It.IsAny<Stream>()))
            .Returns(new FacturaParsedXml
            {
                UUID = "UUID-VALIDO",
                RFCEmisor = "AAA010101AAA",
                Total = 500m
            });

        _facturaRepoMock.Setup(r => r.ValidarFacturaPreviaSpAsync(It.IsAny<string>(), It.IsAny<string>(), "UUID-VALIDO", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidacionFacturaPreviaDto { EsValido = true });

        _storageServiceMock.Setup(s => s.GuardarArchivoAsync(It.IsAny<Stream>(), ".xml", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("xml_interno.xml");

        _storageServiceMock.Setup(s => s.GuardarArchivoAsync(It.IsAny<Stream>(), ".pdf", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("pdf_interno.pdf");

        // Simular excepción al insertar en BD
        _facturaRepoMock.Setup(r => r.RegistrarFacturaCompletaSpAsync(It.IsAny<Factura>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection failure"));

        var dto = new CargaFacturaDto { ProveedorId = 1, XmlNombreOriginal = "f.xml", PdfNombreOriginal = "f.pdf" };

        // Act
        var resultado = await _service.CargarFacturaAsync(dto);

        // Assert
        Assert.False(resultado.Exitoso);
        Assert.Contains("No fue posible registrar la factura en la base de datos", resultado.Mensaje);

        // Verificar que se invocó la eliminación / rollback de ambos archivos
        _storageServiceMock.Verify(s => s.EliminarArchivoAsync("xml_interno.xml", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        _storageServiceMock.Verify(s => s.EliminarArchivoAsync("pdf_interno.pdf", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ObtenerDetalleAsync_ProveedorDistinto_DebePrevenirIdorYRetornarNull()
    {
        // Arrange: Factura pertenece al ProveedorId 10
        var factura = new Factura { Id = 100, ProveedorId = 10, UUID = "UUID-100" };
        _facturaRepoMock.Setup(r => r.ObtenerFacturaPorIdSpAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(factura);

        // Act: El usuario autenticado es ProveedorId 20 (intento de IDOR)
        var resultado = await _service.ObtenerDetalleAsync(100, proveedorId: 20);

        // Assert
        Assert.Null(resultado);
    }

    [Fact]
    public async Task CambiarEstatusFacturaAsync_RechazoSinMotivo_DebeRetornarBadRequest()
    {
        // Arrange
        var factura = new Factura { Id = 5, Estatus = EstadoFactura.Pendiente };
        _facturaRepoMock.Setup(r => r.ObtenerFacturaPorIdSpAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(factura);

        var dto = new ActualizarEstatusFacturaDto
        {
            NuevoEstatus = EstadoFactura.Rechazada,
            MotivoRechazo = null // Sin motivo obligatorio
        };

        // Act
        var resultado = await _service.CambiarEstatusFacturaAsync(5, dto, usuarioId: 1, rol: "Revisor", ipAddress: "127.0.0.1");

        // Assert
        Assert.False(resultado.Success);
        Assert.Equal(400, resultado.StatusCode);
        Assert.Contains("motivo de rechazo", resultado.Message);
    }

    [Fact]
    public async Task CambiarEstatusFacturaAsync_FacturaYaPagada_DebeRetornarBadRequest()
    {
        // Arrange
        var factura = new Factura { Id = 5, Estatus = EstadoFactura.Pagada };
        _facturaRepoMock.Setup(r => r.ObtenerFacturaPorIdSpAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(factura);

        var dto = new ActualizarEstatusFacturaDto
        {
            NuevoEstatus = EstadoFactura.Rechazada,
            MotivoRechazo = "Error contable"
        };

        // Act
        var resultado = await _service.CambiarEstatusFacturaAsync(5, dto, usuarioId: 1, rol: "Revisor", ipAddress: "127.0.0.1");

        // Assert
        Assert.False(resultado.Success);
        Assert.Equal(400, resultado.StatusCode);
        Assert.Contains("Pagada", resultado.Message);
    }

    [Fact]
    public async Task CambiarEstatusFacturaAsync_Valido_DebeActualizarYRegistrarAuditoria()
    {
        // Arrange
        var factura = new Factura { Id = 5, UUID = "UUID-VALID", Estatus = EstadoFactura.Pendiente };
        _facturaRepoMock.Setup(r => r.ObtenerFacturaPorIdSpAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(factura);

        _facturaRepoMock.Setup(r => r.ActualizarEstatusFacturaSpAsync(5, EstadoFactura.Validada, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var dto = new ActualizarEstatusFacturaDto
        {
            NuevoEstatus = EstadoFactura.Validada
        };

        // Act
        var resultado = await _service.CambiarEstatusFacturaAsync(5, dto, usuarioId: 7, rol: "Revisor", ipAddress: "192.168.1.50");

        // Assert
        Assert.True(resultado.Success);
        _usuarioRepoMock.Verify(u => u.RegistrarAuditoriaSpAsync(
            7,
            "FACTURACION",
            "CAMBIO_ESTATUS_FACTURA",
            It.IsAny<string>(),
            "192.168.1.50",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ListarFacturasPaginadasAsync_DebeMapearPaginacionYDtosCorrectamente()
    {
        // Arrange
        var facturas = new List<FacturaResumenDto>
        {
            new() { FacturaId = 1, ProveedorId = 2, UUID = "U1", Total = 100m, Estatus = EstadoFactura.Pendiente },
            new() { FacturaId = 2, ProveedorId = 2, UUID = "U2", Total = 200m, Estatus = EstadoFactura.Validada }
        };

        var resultadoPaginadoRepo = new PaginatedResult<FacturaResumenDto>(facturas, totalRecords: 2, pageNumber: 1, pageSize: 10);

        _facturaRepoMock.Setup(r => r.ListarFacturasProveedorSpAsync(2, It.IsAny<FacturaFiltroDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultadoPaginadoRepo);

        var filtro = new FacturaFiltroDto { Pagina = 1, RegistrosPorPagina = 10 };

        // Act
        var resultado = await _service.ListarFacturasPaginadasAsync(filtro, proveedorId: 2);

        // Assert
        Assert.NotNull(resultado);
        Assert.Equal(2, resultado.TotalRecords);
        Assert.Equal(2, resultado.Items.Count);
        Assert.Equal("U1", resultado.Items.First().UUID);
        Assert.False(resultado.HasNextPage);
        Assert.False(resultado.HasPreviousPage);
    }
}
