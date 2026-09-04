namespace PortalProveedores.Core.Entities;

public class Factura
{
    public long FacturaId { get; set; }
    public int Id { get => (int)FacturaId; set => FacturaId = value; }

    public int ProveedorId { get; set; }
    public string CodigoProveedor { get; set; } = string.Empty;
    public Proveedor? Proveedor { get; set; }

    public string UUID { get; set; } = string.Empty;
    public string? Serie { get; set; }
    public string FolioFactura { get; set; } = string.Empty;
    public string? Folio { get => FolioFactura; set => FolioFactura = value ?? string.Empty; }

    public string RFCEmisor { get; set; } = string.Empty;
    public string RFCReceptor { get; set; } = string.Empty;

    public DateTime FechaFactura { get; set; }
    public DateTime FechaEmision { get => FechaFactura; set => FechaFactura = value; }

    public DateTime FechaRecepcion { get; set; } = DateTime.UtcNow;
    public DateTime FechaCarga { get => FechaRecepcion; set => FechaRecepcion = value; }

    public decimal Subtotal { get; set; }
    public decimal Descuento { get; set; }
    public decimal IvaTrasladado { get; set; }
    public decimal ImpuestosTrasladados { get => IvaTrasladado; set => IvaTrasladado = value; }
    public decimal IvaRetenido { get; set; }
    public decimal ImpuestosRetenidos { get => IvaRetenido; set => IvaRetenido = value; }
    public decimal CostoTotal { get; set; }
    public decimal Total { get => CostoTotal; set => CostoTotal = value; }

    public string Moneda { get; set; } = "MXN";
    public decimal TipoCambio { get; set; } = 1.0000m;

    public string CodigoUsoCFDI { get; set; } = "G03";
    public string CodigoCFDIMetodoPago { get; set; } = "PPD";
    public string CodigoCFDIFormaPago { get; set; } = "99";
    public string RegimenFiscalEmisor { get; set; } = "601";
    public string RegimenFiscalReceptor { get; set; } = "601";
    public byte NumeroCortoSucursal { get; set; } = 1;
    public string? NombreSucursal { get; set; }
    public string? OrdenCompra { get; set; }
    public bool EsMesaDeControl { get; set; }

    // Estatus de proceso
    public byte EstatusValidacion { get; set; } = 1; // 1: Recibida/Pendiente, 2: Validada OK, 3: Rechazada
    public EstadoFactura Estatus 
    { 
        get => EstatusValidacion switch
        {
            1 => EstadoFactura.Pendiente,
            2 => EstadoFactura.Validada,
            3 => EstadoFactura.Rechazada,
            4 => EstadoFactura.EnRevision,
            5 => EstadoFactura.AprobadaParaPago,
            6 => EstadoFactura.Pagada,
            _ => (EstadoFactura)EstatusValidacion
        };
        set => EstatusValidacion = (byte)value;
    }

    public string? MotivoRechazo { get; set; }
    public byte EstatusSincronizacion { get; set; } // 0: Sin procesar, 1: En Cola, 2: Sincronizada, 3: Error
    public decimal? NumeroDocumentoCentral { get; set; }
    public DateTime? FechaSincronizacion { get; set; }
    public string? EstatusMercancia { get; set; } = "PENDIENTE_ENTREGA";
    public string? Observaciones { get; set; }

    // Archivos
    public string ArchivoXmlNombreInterno { get; set; } = string.Empty;
    public string ArchivoXmlNombreOriginal { get; set; } = string.Empty;
    public string ArchivoPdfNombreInterno { get; set; } = string.Empty;
    public string ArchivoPdfNombreOriginal { get; set; } = string.Empty;

    public List<FacturaDetalleItem> Detalle { get; set; } = new();
    public List<FacturaArchivoItem> Archivos { get; set; } = new();
}

public class FacturaDetalleItem
{
    public long FacturaDetalleId { get; set; }
    public long FacturaId { get; set; }
    public short Renglon { get; set; }
    public string CodigoArticulo { get; set; } = string.Empty;
    public string ClaveProdServSat { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string Unidad { get; set; } = "PZA";
    public decimal Cantidad { get; set; }
    public decimal PrecioUnitarioSinDescuento { get; set; }
    public string Descuento { get; set; } = "0";
    public decimal PrecioUnitarioConDescuento { get; set; }
    public decimal Importe { get; set; }
    public decimal PorcentajeRetencion { get; set; }
    public decimal MontoRetencion { get; set; }
    public decimal CantidadReal { get; set; }
}

public class FacturaArchivoItem
{
    public long ArchivoId { get; set; }
    public long FacturaId { get; set; }
    public string TipoArchivo { get; set; } = "XML"; // 'XML' o 'PDF'
    public string NombreOriginal { get; set; } = string.Empty;
    public string NombreAlmacenamiento { get; set; } = string.Empty;
    public string RutaFisicaSegura { get; set; } = string.Empty;
    public string HashSha256 { get; set; } = string.Empty;
    public int TamanoBytes { get; set; }
    public string? XmlContenido { get; set; }
}
