using PortalProveedores.Application.DTOs;

namespace PortalProveedores.Application.Interfaces;

/// <summary>
/// Contrato de persistencia y despacho para sincronización entre PortalProveedores_DB y Punto_de_Venta.
/// </summary>
public interface ISyncRepository
{
    Task<IEnumerable<SyncColaItemDto>> ObtenerLotePendienteSpAsync(int tamanoLote = 10, CancellationToken ct = default);
    Task<decimal> EjecutarInsertCentralSpAsync(long facturaId, CancellationToken ct = default);
    Task ConfirmarSincronizacionSpAsync(long syncId, long facturaId, decimal numeroDocumentoCentral, CancellationToken ct = default);
    Task RegistrarFalloSpAsync(long syncId, long facturaId, string mensajeError, CancellationToken ct = default);
    Task<string> RefrescarCatalogosDesdeCentralSpAsync(CancellationToken ct = default);
    Task<int> ActualizarEstatusEntregaMercanciaSpAsync(CancellationToken ct = default);
    Task<EstadoSincronizacionResumenDto> ObtenerResumenEstadoSyncAsync(CancellationToken ct = default);
}

/// <summary>
/// Orquestador de las operaciones de sincronización continua entre el Portal y el ERP Central.
/// </summary>
public interface ISyncService
{
    Task<ResultadoProcesarLoteSyncDto> ProcesarColaFacturasAsync(int tamanoLote = 10, CancellationToken ct = default);
    Task<ResultadoSyncCatalogosDto> RefrescarCatalogosAsync(CancellationToken ct = default);
    Task<int> ActualizarEstatusEntregasAsync(CancellationToken ct = default);
    Task<EstadoSincronizacionResumenDto> ObtenerEstadoSincronizacionAsync(CancellationToken ct = default);
}
