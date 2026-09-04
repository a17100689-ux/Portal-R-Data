using PortalProveedores.Core.Entities;

namespace PortalProveedores.Application.DTOs;

public class ResultadoValidacionArchivo
{
    public bool EsValido { get; set; }
    public string? MensajeError { get; set; }
    public string? TipoDetectado { get; set; }
}

public class CargaFacturaDto
{
    public int ProveedorId { get; set; }
    public Stream XmlStream { get; set; } = Stream.Null;
    public string XmlNombreOriginal { get; set; } = string.Empty;
    public long XmlTamano { get; set; }

    public Stream PdfStream { get; set; } = Stream.Null;
    public string PdfNombreOriginal { get; set; } = string.Empty;
    public long PdfTamano { get; set; }

    public byte NumeroCortoSucursal { get; set; } = 1;
    public string? OrdenCompra { get; set; }
    public bool EsMesaDeControl { get; set; } = false;
    public string? CreatedByIp { get; set; }
    public string? Observaciones { get; set; }
}

public class FacturaConceptoXml
{
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

public class FacturaParsedXml
{
    public string UUID { get; set; } = string.Empty;
    public string? Serie { get; set; }
    public string? Folio { get; set; }
    public string RFCEmisor { get; set; } = string.Empty;
    public string NombreEmisor { get; set; } = string.Empty;
    public string RFCReceptor { get; set; } = string.Empty;
    public string NombreReceptor { get; set; } = string.Empty;
    public DateTime FechaEmision { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Descuento { get; set; }
    public decimal ImpuestosTrasladados { get; set; }
    public decimal ImpuestosRetenidos { get; set; }
    public decimal Total { get; set; }
    public string Moneda { get; set; } = "MXN";
    public decimal TipoCambio { get; set; } = 1.0000m;
    public string CodigoUsoCFDI { get; set; } = "G03";
    public string CodigoCFDIMetodoPago { get; set; } = "PPD";
    public string CodigoCFDIFormaPago { get; set; } = "99";
    public string RegimenFiscalEmisor { get; set; } = "601";
    public string RegimenFiscalReceptor { get; set; } = "601";
    public List<FacturaConceptoXml> Conceptos { get; set; } = new();
}

public class ValidacionFacturaPreviaDto
{
    public bool EsValido { get; set; }
    public bool ExisteUUID { get; set; }
    public bool ExisteFolio { get; set; }
    public string MensajeError { get; set; } = string.Empty;
}

public class ResultadoCargaFactura
{
    public bool Exitoso { get; set; }
    public string Mensaje { get; set; } = string.Empty;
    public long? FacturaId { get; set; }
    public string? UUID { get; set; }
}

public class CargaFacturaResultDto
{
    public long FacturaId { get; set; }
    public string UUID { get; set; } = string.Empty;
    public string? Serie { get; set; }
    public string? Folio { get; set; }
    public string RFCEmisor { get; set; } = string.Empty;
    public string RFCReceptor { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public string Moneda { get; set; } = "MXN";
    public DateTime FechaEmision { get; set; }
    public DateTime FechaCarga { get; set; }
    public EstadoFactura Estatus { get; set; }
}

public class FacturaFiltroDto
{
    private int _pagina = 1;
    private int _registrosPorPagina = 20;

    public int Pagina
    {
        get => _pagina;
        set => _pagina = value < 1 ? 1 : value;
    }

    public int RegistrosPorPagina
    {
        get => _registrosPorPagina;
        set => _registrosPorPagina = value switch
        {
            < 1 => 20,
            > 100 => 100,
            _ => value
        };
    }

    public EstadoFactura? Estatus { get; set; }
    public byte? EstatusValidacion => Estatus.HasValue ? (byte)Estatus.Value : null;
    public DateTime? FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }
    public string? FolioFactura { get; set; }
    public string? TerminoBusqueda { get => FolioFactura; set => FolioFactura = value; }
}

public class FacturaResumenDto
{
    public long FacturaId { get; set; }
    public int Id { get => (int)FacturaId; set => FacturaId = value; }
    public int ProveedorId { get; set; }
    public string? CodigoProveedor { get; set; }
    public string? RazonSocialProveedor { get; set; }
    public string UUID { get; set; } = string.Empty;
    public string? Serie { get; set; }
    public string FolioFactura { get; set; } = string.Empty;
    public string? Folio { get => FolioFactura; set => FolioFactura = value ?? string.Empty; }
    public string RFCEmisor { get; set; } = string.Empty;
    public string RFCReceptor { get; set; } = string.Empty;
    public DateTime FechaFactura { get; set; }
    public DateTime FechaEmision { get => FechaFactura; set => FechaFactura = value; }
    public DateTime FechaRecepcion { get; set; }
    public DateTime FechaCarga { get => FechaRecepcion; set => FechaRecepcion = value; }
    public decimal Subtotal { get; set; }
    public decimal Descuento { get; set; }
    public decimal IvaTrasladado { get; set; }
    public decimal CostoTotal { get; set; }
    public decimal Total { get => CostoTotal; set => CostoTotal = value; }
    public string Moneda { get; set; } = "MXN";
    public byte NumeroCortoSucursal { get; set; }
    public string? NombreSucursal { get; set; }
    public string? OrdenCompra { get; set; }
    public bool EsMesaDeControl { get; set; }
    public byte EstatusValidacion { get; set; } = 1;
    public EstadoFactura Estatus 
    { 
        get => EstatusValidacion switch { 1 => EstadoFactura.Pendiente, 2 => EstadoFactura.Validada, 3 => EstadoFactura.Rechazada, _ => EstadoFactura.Pendiente };
        set => EstatusValidacion = (byte)value;
    }
    public string EstatusNombre => Estatus.ToString();
    public byte EstatusSincronizacion { get; set; }
    public decimal? NumeroDocumentoCentral { get; set; }
    public string? MotivoRechazo { get; set; }
}

public class FacturaDetalleDto
{
    public long FacturaId { get; set; }
    public int Id { get => (int)FacturaId; set => FacturaId = value; }
    public int ProveedorId { get; set; }
    public string CodigoProveedor { get; set; } = string.Empty;
    public string? RazonSocialProveedor { get; set; }
    public string UUID { get; set; } = string.Empty;
    public string? Serie { get; set; }
    public string FolioFactura { get; set; } = string.Empty;
    public string? Folio { get => FolioFactura; set => FolioFactura = value ?? string.Empty; }
    public string RFCEmisor { get; set; } = string.Empty;
    public string RFCReceptor { get; set; } = string.Empty;
    public DateTime FechaFactura { get; set; }
    public DateTime FechaEmision { get => FechaFactura; set => FechaFactura = value; }
    public DateTime FechaRecepcion { get; set; }
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
    public decimal TipoCambio { get; set; } = 1.0m;
    public string CodigoUsoCFDI { get; set; } = "G03";
    public string CodigoCFDIMetodoPago { get; set; } = "PPD";
    public string CodigoCFDIFormaPago { get; set; } = "99";
    public string RegimenFiscalEmisor { get; set; } = "601";
    public string RegimenFiscalReceptor { get; set; } = "601";
    public byte NumeroCortoSucursal { get; set; }
    public string? NombreSucursal { get; set; }
    public string? OrdenCompra { get; set; }
    public bool EsMesaDeControl { get; set; }
    public byte EstatusValidacion { get; set; }
    public EstadoFactura Estatus 
    { 
        get => EstatusValidacion switch { 1 => EstadoFactura.Pendiente, 2 => EstadoFactura.Validada, 3 => EstadoFactura.Rechazada, _ => EstadoFactura.Pendiente };
        set => EstatusValidacion = (byte)value;
    }
    public string EstatusNombre => Estatus.ToString();
    public byte EstatusSincronizacion { get; set; }
    public decimal? NumeroDocumentoCentral { get; set; }
    public DateTime? FechaSincronizacion { get; set; }
    public string? MotivoRechazo { get; set; }
    public string? Observaciones { get; set; }

    public string ArchivoXmlNombreOriginal { get; set; } = string.Empty;
    public string ArchivoPdfNombreOriginal { get; set; } = string.Empty;

    public List<FacturaDetalleItem> Partidas { get; set; } = new();
    public List<FacturaArchivoItem> Archivos { get; set; } = new();
}

public class ActualizarEstatusFacturaDto
{
    public EstadoFactura NuevoEstatus { get; set; }
    public string? MotivoRechazo { get; set; }
}

public class LoginDto
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class ResultadoLogin
{
    public bool Exitoso { get; set; }
    public string Mensaje { get; set; } = string.Empty;
    public int? UsuarioId { get; set; }
    public string? Username { get; set; }
    public string? Email { get; set; }
    public string? RazonSocial { get; set; }
    public string? Rol { get; set; }
    public int? ProveedorId { get; set; }
    public string? CodigoProveedor { get; set; }
    public string? RFC { get; set; }
    public bool EsAdmin { get; set; }
}

public class CrearProveedorDto
{
    public string CodigoProveedor { get; set; } = string.Empty;
    public string RFC { get; set; } = string.Empty;
    public string RazonSocial { get; set; } = string.Empty;
    public string? RegimenFiscal { get; set; }
    public string? CodigoPostal { get; set; }
    public string? CondicionesPago { get; set; } = "30";
    public string? Telefono { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool RequiereValidarCompra { get; set; } = true;
    public bool OrdenCompraObligatoria { get; set; } = true;
    public bool EsProveedorNacional { get; set; } = true;
    public bool Activo { get; set; } = true;
}

public class ResultadoCrearProveedorDto
{
    public bool Exitoso { get; set; }
    public string Mensaje { get; set; } = string.Empty;
    public int? ProveedorId { get; set; }
    public int? UsuarioId { get; set; }
    public string? CodigoProveedor { get; set; }
    public string? RFC { get; set; }
}
