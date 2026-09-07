namespace PortalProveedores.Application.DTOs;

/// <summary>
/// Representa una transacción pendiente en la cola outbox dbo.Sync_Transacciones_Cola.
/// </summary>
public class SyncColaItemDto
{
    public long SyncId { get; set; }
    public long FacturaId { get; set; }
    public string TipoOperacion { get; set; } = "COMPRA_NORMAL";
    public byte Intentos { get; set; }
    public string CodigoProveedor { get; set; } = string.Empty;
    public string UUID { get; set; } = string.Empty;
    public string? Serie { get; set; }
    public string FolioFactura { get; set; } = string.Empty;
    public DateTime FechaFactura { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Descuento { get; set; }
    public decimal IvaTrasladado { get; set; }
    public decimal IvaRetenido { get; set; }
    public decimal CostoTotal { get; set; }
    public string Moneda { get; set; } = "MXN";
    public decimal TipoCambio { get; set; } = 1.0m;
    public string CodigoUsoCFDI { get; set; } = "G01";
    public string CodigoCFDIMetodoPago { get; set; } = "PPD";
    public string CodigoCFDIFormaPago { get; set; } = "99";
    public string RegimenFiscalEmisor { get; set; } = "601";
    public string RegimenFiscalReceptor { get; set; } = "601";
    public byte NumeroCortoSucursal { get; set; } = 1;
    public string? OrdenCompra { get; set; }
    public bool EsMesaDeControl { get; set; }
}

/// <summary>
/// Resultado del procesamiento de un lote de la cola de sincronización.
/// </summary>
public class ResultadoProcesarLoteSyncDto
{
    public int TotalLote { get; set; }
    public int Exitosos { get; set; }
    public int Fallidos { get; set; }
    public List<string> Detalles { get; set; } = new();
    public DateTime FechaEjecucion { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Resultado de la sincronización y actualización de catálogos desde la base central.
/// </summary>
public class ResultadoSyncCatalogosDto
{
    public bool Exitoso { get; set; }
    public string Mensaje { get; set; } = string.Empty;
    public DateTime FechaEjecucion { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Resumen del estado global de la sincronización para monitoreo y dashboard.
/// </summary>
public class EstadoSincronizacionResumenDto
{
    public int FacturasPendientes { get; set; }
    public int FacturasEnProceso { get; set; }
    public int FacturasCompletadas { get; set; }
    public int FacturasFallidas { get; set; }
    public DateTime? UltimaSincronizacionExitosa { get; set; }
    public DateTime? UltimoRefrescoCatalogos { get; set; }
    public DateTime? UltimaVerificacionEntregas { get; set; }
    public bool WorkerActivo { get; set; }
}
