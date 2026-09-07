using Microsoft.Extensions.Logging;
using PortalProveedores.Application.DTOs;
using PortalProveedores.Application.Interfaces;

namespace PortalProveedores.Application.Services;

public class SyncService : ISyncService
{
    private readonly ISyncRepository _syncRepo;
    private readonly ILogger<SyncService> _logger;

    public SyncService(ISyncRepository syncRepo, ILogger<SyncService> logger)
    {
        _syncRepo = syncRepo;
        _logger = logger;
    }

    public async Task<ResultadoProcesarLoteSyncDto> ProcesarColaFacturasAsync(int tamanoLote = 10, CancellationToken ct = default)
    {
        var resultado = new ResultadoProcesarLoteSyncDto();

        try
        {
            var lote = (await _syncRepo.ObtenerLotePendienteSpAsync(tamanoLote, ct)).ToList();
            resultado.TotalLote = lote.Count;

            if (lote.Count == 0)
            {
                return resultado;
            }

            _logger.LogInformation("Iniciando despacho de sincronización para {Count} facturas pendientes.", lote.Count);

            foreach (var item in lote)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    _logger.LogInformation("Procesando sincronización FacturaId: {FacturaId}, UUID: {UUID}, Folio: {Folio}",
                        item.FacturaId, item.UUID, item.FolioFactura);

                    // 1. Ejecutar inserción atómica en base central
                    decimal numeroDocumentoCentral = await _syncRepo.EjecutarInsertCentralSpAsync(item.FacturaId, ct);

                    // 2. Confirmar sincronización exitosa en la cola
                    await _syncRepo.ConfirmarSincronizacionSpAsync(item.SyncId, item.FacturaId, numeroDocumentoCentral, ct);

                    resultado.Exitosos++;
                    resultado.Detalles.Add($"Factura {item.FolioFactura} sincronizada OK -> DocCentral: {numeroDocumentoCentral}");

                    _logger.LogInformation("FacturaId: {FacturaId} sincronizada exitosamente con Documento Central #{NumDoc}",
                        item.FacturaId, numeroDocumentoCentral);
                }
                catch (Exception ex)
                {
                    resultado.Fallidos++;
                    string mensajeError = ex.Message.Length > 950 ? ex.Message.Substring(0, 950) : ex.Message;
                    resultado.Detalles.Add($"Error en Factura {item.FolioFactura}: {mensajeError}");

                    _logger.LogError(ex, "Error al sincronizar FacturaId: {FacturaId} hacia Punto_de_Venta. Registrando fallo en cola.", item.FacturaId);

                    try
                    {
                        await _syncRepo.RegistrarFalloSpAsync(item.SyncId, item.FacturaId, mensajeError, ct);
                    }
                    catch (Exception regEx)
                    {
                        _logger.LogError(regEx, "Fallo crítico al registrar reintento para SyncId: {SyncId}", item.SyncId);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error general al obtener lote de sincronización.");
            resultado.Detalles.Add($"Error general de lote: {ex.Message}");
        }

        return resultado;
    }

    public async Task<ResultadoSyncCatalogosDto> RefrescarCatalogosAsync(CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Iniciando refresco de catálogos desde Punto_de_Venta...");
            string mensaje = await _syncRepo.RefrescarCatalogosDesdeCentralSpAsync(ct);
            _logger.LogInformation("Catálogos refrescados con éxito: {Mensaje}", mensaje);

            return new ResultadoSyncCatalogosDto
            {
                Exitoso = true,
                Mensaje = mensaje
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al refrescar catálogos desde Punto_de_Venta.");
            return new ResultadoSyncCatalogosDto
            {
                Exitoso = false,
                Mensaje = $"Error al refrescar catálogos: {ex.Message}"
            };
        }
    }

    public async Task<int> ActualizarEstatusEntregasAsync(CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Verificando recepción de mercancía en Punto_de_Venta...");
            int actualizadas = await _syncRepo.ActualizarEstatusEntregaMercanciaSpAsync(ct);
            if (actualizadas > 0)
            {
                _logger.LogInformation("Se actualizaron {Count} facturas con mercancía entregada.", actualizadas);
            }
            return actualizadas;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al verificar estatus de entregas de mercancía.");
            return 0;
        }
    }

    public async Task<EstadoSincronizacionResumenDto> ObtenerEstadoSincronizacionAsync(CancellationToken ct = default)
    {
        return await _syncRepo.ObtenerResumenEstadoSyncAsync(ct);
    }
}
