using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortalProveedores.Application.Interfaces;

namespace PortalProveedores.Web.Controllers;

[Authorize(Roles = "Administrador,ADMIN,COMPRAS,MESA_CONTROL")]
public class AdminSyncController : Controller
{
    private readonly ISyncService _syncService;
    private readonly ILogger<AdminSyncController> _logger;

    public AdminSyncController(ISyncService syncService, ILogger<AdminSyncController> logger)
    {
        _syncService = syncService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var resumen = await _syncService.ObtenerEstadoSincronizacionAsync(ct);
        return View(resumen);
    }

    [HttpGet]
    public async Task<IActionResult> EstadoJson(CancellationToken ct)
    {
        var resumen = await _syncService.ObtenerEstadoSincronizacionAsync(ct);
        return Json(resumen);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProcesarCola(CancellationToken ct)
    {
        var resultado = await _syncService.ProcesarColaFacturasAsync(tamanoLote: 25, ct);
        if (resultado.Fallidos > 0)
        {
            TempData["MensajeError"] = $"Sincronización parcial: {resultado.Exitosos} procesadas correctamente, {resultado.Fallidos} con error.";
        }
        else if (resultado.Exitosos > 0)
        {
            TempData["MensajeExito"] = $"Se procesaron exitosamente {resultado.Exitosos} facturas hacia la base central.";
        }
        else
        {
            TempData["MensajeInfo"] = "No hay facturas pendientes en la cola de sincronización.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RefrescarCatalogos(CancellationToken ct)
    {
        var resultado = await _syncService.RefrescarCatalogosAsync(ct);
        if (resultado.Exitoso)
        {
            TempData["MensajeExito"] = resultado.Mensaje;
        }
        else
        {
            TempData["MensajeError"] = resultado.Mensaje;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerificarEntregas(CancellationToken ct)
    {
        int actualizadas = await _syncService.ActualizarEstatusEntregasAsync(ct);
        if (actualizadas > 0)
        {
            TempData["MensajeExito"] = $"Se actualizaron {actualizadas} facturas con entrega confirmada en tienda/almacén.";
        }
        else
        {
            TempData["MensajeInfo"] = "No se encontraron nuevas entregas de mercancía para actualizar.";
        }

        return RedirectToAction(nameof(Index));
    }
}
