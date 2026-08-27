using PortalProveedores.Application.DTOs;
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
    Task<int> InsertarFacturaSpAsync(Factura factura, CancellationToken ct = default);
    Task<Factura?> ObtenerFacturaPorIdSpAsync(int id, CancellationToken ct = default);
    Task<Factura?> ObtenerFacturaPorUuidSpAsync(string uuid, CancellationToken ct = default);
    Task<IEnumerable<Factura>> ListarFacturasPorProveedorSpAsync(int proveedorId, CancellationToken ct = default);
    Task<IEnumerable<Factura>> ListarTodasFacturasSpAsync(EstadoFactura? estatus, CancellationToken ct = default);
    Task<bool> ActualizarEstatusFacturaSpAsync(int facturaId, EstadoFactura nuevoEstatus, string? motivoRechazo, CancellationToken ct = default);
    Task<bool> ExisteUuidSpAsync(string uuid, CancellationToken ct = default);
}

public interface IUsuarioRepository
{
    Task<Usuario?> ObtenerPorUsernameSpAsync(string username, CancellationToken ct = default);
    Task<Usuario?> ObtenerPorIdSpAsync(int id, CancellationToken ct = default);
    Task RegistrarIntentoFallidoSpAsync(int usuarioId, CancellationToken ct = default);
    Task ResetearIntentosFallidosSpAsync(int usuarioId, CancellationToken ct = default);
    Task RegistrarAuditoriaSpAsync(RegistroAuditoria auditoria, CancellationToken ct = default);
}

public interface IFacturaService
{
    Task<ResultadoCargaFactura> CargarFacturaAsync(CargaFacturaDto dto, CancellationToken ct = default);
    Task<Factura?> ObtenerDetalleAsync(int facturaId, int? proveedorId, CancellationToken ct = default);
    Task<IEnumerable<Factura>> ObtenerFacturasProveedorAsync(int proveedorId, CancellationToken ct = default);
}

public interface IAuthService
{
    Task<ResultadoLogin> ValidarCredencialesAsync(LoginDto dto, string ipAddress, CancellationToken ct = default);
}
