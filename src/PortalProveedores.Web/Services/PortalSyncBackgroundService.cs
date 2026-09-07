using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using PortalProveedores.Application.Interfaces;

namespace PortalProveedores.Web.Services;

/// <summary>
/// Servicio en segundo plano para sincronización asíncrona continua entre PortalProveedores_DB y Punto_de_Venta.
/// Ejecuta:
/// 1. Despacho de facturas pendientes en cola outbox (cada 30s).
/// 2. Actualización de estatus de entrega física de mercancía (cada 5 min).
/// 3. Sincronización periódica de catálogos centrales (cada hora y al iniciar).
/// </summary>
public class PortalSyncBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PortalSyncBackgroundService> _logger;

    private DateTime _ultimaEjecucionEntregas = DateTime.MinValue;
    private DateTime _ultimaEjecucionCatalogos = DateTime.MinValue;

    public PortalSyncBackgroundService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<PortalSyncBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        bool habilitado = _configuration.GetValue<bool>("SyncOptions:Enabled", true);
        if (!habilitado)
        {
            _logger.LogInformation("PortalSyncBackgroundService está deshabilitado en configuración (SyncOptions:Enabled = false).");
            return;
        }

        int intervaloFacturasSegundos = _configuration.GetValue<int>("SyncOptions:IntervaloFacturasSegundos", 30);
        int intervaloEntregasMinutos = _configuration.GetValue<int>("SyncOptions:IntervaloEntregasMinutos", 5);
        int intervaloCatalogosHoras = _configuration.GetValue<int>("SyncOptions:IntervaloCatalogosHoras", 1);

        _logger.LogInformation(
            "Iniciando PortalSyncBackgroundService. Intervalo facturas: {Secs}s, Entregas: {Min}m, Catálogos: {Horas}h.",
            intervaloFacturasSegundos, intervaloEntregasMinutos, intervaloCatalogosHoras);

        // Pequeño retardo inicial para permitir que el servidor web complete el bootstrap
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        // Refresco inicial de catálogos al arrancar el servicio
        try
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var syncService = scope.ServiceProvider.GetRequiredService<ISyncService>();
                _logger.LogInformation("Ejecutando refresco inicial de catálogos en arranque...");
                await syncService.RefrescarCatalogosAsync(stoppingToken);
                _ultimaEjecucionCatalogos = DateTime.UtcNow;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error durante el refresco inicial de catálogos en arranque.");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var syncService = scope.ServiceProvider.GetRequiredService<ISyncService>();

                // 1. Procesar facturas en cola outbox hacia ERP central
                var resultadoFacturas = await syncService.ProcesarColaFacturasAsync(tamanoLote: 10, stoppingToken);
                if (resultadoFacturas.Exitosos > 0 || resultadoFacturas.Fallidos > 0)
                {
                    _logger.LogInformation("Ciclo de sincronización: {Exitosos} facturas procesadas OK, {Fallidos} fallidas.",
                        resultadoFacturas.Exitosos, resultadoFacturas.Fallidos);
                }

                // 2. Monitoreo de recepción física de mercancía (cada N minutos)
                if (DateTime.UtcNow - _ultimaEjecucionEntregas >= TimeSpan.FromMinutes(intervaloEntregasMinutos))
                {
                    int entregasActualizadas = await syncService.ActualizarEstatusEntregasAsync(stoppingToken);
                    if (entregasActualizadas > 0)
                    {
                        _logger.LogInformation("Actualizado estatus de entrega para {Count} facturas en el portal.", entregasActualizadas);
                    }
                    _ultimaEjecucionEntregas = DateTime.UtcNow;
                }

                // 3. Refresco periódico de catálogos (cada N horas)
                if (DateTime.UtcNow - _ultimaEjecucionCatalogos >= TimeSpan.FromHours(intervaloCatalogosHoras))
                {
                    _logger.LogInformation("Ejecutando refresco periódico de catálogos...");
                    await syncService.RefrescarCatalogosAsync(stoppingToken);
                    _ultimaEjecucionCatalogos = DateTime.UtcNow;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error no controlado en el ciclo de sincronización en segundo plano.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(intervaloFacturasSegundos), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("PortalSyncBackgroundService detenido correctamente.");
    }
}
