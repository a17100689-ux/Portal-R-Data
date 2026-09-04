using PortalProveedores.Application.DTOs;
using PortalProveedores.Core.Common;
using PortalProveedores.Core.Entities;

namespace PortalProveedores.Application.Interfaces;

public interface IStorageService
{
    Task<string> GuardarArchivoAsync(Stream stream, string extension, string subCarpeta, CancellationToken ct = default);
    Task<Stream?> ObtenerArchivoStreamAsync(string nombreInterno, string subCarpeta, CancellationToken ct = default);
    Task<bool> EliminarArchivoAsync(string nombreInterno, string subCarpeta, CancellationToken ct = default);
}

public interface IFacturaRepository
{
    // Métodos oficiales alineados con PortalProveedores_DB
    Task<ValidacionFacturaPreviaDto> ValidarFacturaPreviaSpAsync(string codigoProveedor, string folioFactura, string uuid, CancellationToken ct = default);
    Task<long> RegistrarFacturaCompletaSpAsync(Factura factura, string xmlContenido, CancellationToken ct = default);
    Task<PaginatedResult<FacturaResumenDto>> ListarFacturasProveedorSpAsync(int proveedorId, FacturaFiltroDto filtro, CancellationToken ct = default);
    Task<FacturaDetalleDto?> ObtenerFacturaDetalleSpAsync(long facturaId, int proveedorId, CancellationToken ct = default);

    // Métodos de compatibilidad de servicios
    Task<int> InsertarFacturaSpAsync(Factura factura, CancellationToken ct = default);
    Task<Factura?> ObtenerFacturaPorIdSpAsync(int id, CancellationToken ct = default);
    Task<Factura?> ObtenerFacturaPorUuidSpAsync(string uuid, CancellationToken ct = default);
    Task<IEnumerable<Factura>> ListarFacturasPorProveedorSpAsync(int proveedorId, CancellationToken ct = default);
    Task<IEnumerable<Factura>> ListarTodasFacturasSpAsync(EstadoFactura? estatus, CancellationToken ct = default);
    Task<PaginatedResult<Factura>> ListarPaginadoSpAsync(FacturaFiltroDto filtro, int? proveedorId, CancellationToken ct = default);
    Task<bool> ActualizarEstatusFacturaSpAsync(int facturaId, EstadoFactura nuevoEstatus, string? motivoRechazo, CancellationToken ct = default);
    Task<bool> ExisteUuidSpAsync(string uuid, CancellationToken ct = default);
}

public interface IProveedorRepository
{
    Task<Proveedor?> ObtenerPorIdSpAsync(int id, CancellationToken ct = default);
    Task<Proveedor?> ObtenerPorRfcSpAsync(string rfc, CancellationToken ct = default);
    Task<Proveedor?> ObtenerPorCodigoSpAsync(string codigoProveedor, CancellationToken ct = default);
}

public interface IUsuarioRepository
{
    // Métodos oficiales alineados con PortalProveedores_DB
    Task<UsuarioAdministrador?> ObtenerAdminPorLoginSpAsync(string identificador, CancellationToken ct = default);
    Task<UsuarioProveedor?> ObtenerProveedorPorLoginSpAsync(string identificador, CancellationToken ct = default);
    Task RegistrarIntentoFallidoSpAsync(int usuarioId, CancellationToken ct = default);
    Task RegistrarLoginExitosoSpAsync(int usuarioId, string direccionIp, CancellationToken ct = default);
    Task RegistrarAuditoriaSpAsync(int? usuarioId, string modulo, string accion, string detalle, string direccionIp, CancellationToken ct = default);

    // Métodos de compatibilidad
    Task<Usuario?> ObtenerPorUsernameSpAsync(string username, CancellationToken ct = default);
    Task<Usuario?> ObtenerPorIdSpAsync(int id, CancellationToken ct = default);
    Task ResetearIntentosFallidosSpAsync(int usuarioId, CancellationToken ct = default);
    Task RegistrarAuditoriaSpAsync(RegistroAuditoria auditoria, CancellationToken ct = default);
}

public interface IFacturaService
{
    Task<ResultadoCargaFactura> CargarFacturaAsync(CargaFacturaDto dto, CancellationToken ct = default);
    Task<FacturaDetalleDto?> ObtenerDetalleAsync(int facturaId, int? proveedorId, CancellationToken ct = default);
    Task<Factura?> ObtenerEntidadDetalleAsync(int facturaId, int? proveedorId, CancellationToken ct = default);
    Task<IEnumerable<Factura>> ObtenerFacturasProveedorAsync(int proveedorId, CancellationToken ct = default);
    Task<PaginatedResult<FacturaResumenDto>> ListarFacturasPaginadasAsync(FacturaFiltroDto filtro, int? proveedorId, CancellationToken ct = default);
    Task<ApiResponse<bool>> CambiarEstatusFacturaAsync(int facturaId, ActualizarEstatusFacturaDto dto, int usuarioId, string rol, string ipAddress, CancellationToken ct = default);
}

public interface IAuthService
{
    Task<ResultadoLogin> ValidarCredencialesAsync(LoginDto dto, string ipAddress, CancellationToken ct = default);
}
