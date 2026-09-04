using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using PortalProveedores.Application.DTOs;
using PortalProveedores.Application.Interfaces;
using PortalProveedores.Core.Common;
using PortalProveedores.Core.Entities;

namespace PortalProveedores.Infrastructure.Data;

public class SqlFacturaRepository : IFacturaRepository
{
    private readonly string _connectionString;

    public SqlFacturaRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? throw new InvalidOperationException("Cadena de conexión 'DefaultConnection' no encontrada.");
    }

    private SqlConnection CreateConnection() => new SqlConnection(_connectionString);

    // ============================================================================
    // PROCEDIMIENTOS ALMACENADOS OFICIALES (PortalProveedores_DB)
    // ============================================================================

    public async Task<ValidacionFacturaPreviaDto> ValidarFacturaPreviaSpAsync(string codigoProveedor, string folioFactura, string uuid, CancellationToken ct = default)
    {
        using var connection = CreateConnection();
        var parameters = new DynamicParameters();
        parameters.Add("@CodigoProveedor", codigoProveedor, DbType.String, size: 15);
        parameters.Add("@FolioFactura", folioFactura, DbType.String, size: 40);
        parameters.Add("@UUID", uuid, DbType.String, size: 36);

        var cmd = new CommandDefinition(
            commandText: "dbo.sp_Portal_ValidarFacturaPrevia",
            parameters: parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct
        );

        var resultado = await connection.QueryFirstOrDefaultAsync<ValidacionFacturaPreviaDto>(cmd);
        return resultado ?? new ValidacionFacturaPreviaDto { EsValido = true };
    }

    public async Task<long> RegistrarFacturaCompletaSpAsync(Factura factura, string xmlContenido, CancellationToken ct = default)
    {
        using var connection = CreateConnection();
        var parameters = new DynamicParameters();

        parameters.Add("@ProveedorId", factura.ProveedorId, DbType.Int32);
        parameters.Add("@CodigoProveedor", factura.CodigoProveedor, DbType.String, size: 15);
        parameters.Add("@UUID", factura.UUID, DbType.String, size: 36);
        parameters.Add("@Serie", factura.Serie, DbType.String, size: 15);
        parameters.Add("@FolioFactura", factura.FolioFactura, DbType.String, size: 40);
        parameters.Add("@FechaFactura", factura.FechaFactura, DbType.DateTime2);
        parameters.Add("@Subtotal", factura.Subtotal, DbType.Decimal);
        parameters.Add("@Descuento", factura.Descuento, DbType.Decimal);
        parameters.Add("@IvaTrasladado", factura.IvaTrasladado, DbType.Decimal);
        parameters.Add("@IvaRetenido", factura.IvaRetenido, DbType.Decimal);
        parameters.Add("@CostoTotal", factura.CostoTotal, DbType.Decimal);
        parameters.Add("@Moneda", factura.Moneda, DbType.String, size: 3);
        parameters.Add("@TipoCambio", factura.TipoCambio, DbType.Decimal);
        parameters.Add("@CodigoUsoCFDI", factura.CodigoUsoCFDI, DbType.String, size: 10);
        parameters.Add("@CodigoCFDIMetodoPago", factura.CodigoCFDIMetodoPago, DbType.String, size: 10);
        parameters.Add("@CodigoCFDIFormaPago", factura.CodigoCFDIFormaPago, DbType.String, size: 10);
        parameters.Add("@RegimenFiscalEmisor", factura.RegimenFiscalEmisor, DbType.String, size: 5);
        parameters.Add("@RegimenFiscalReceptor", factura.RegimenFiscalReceptor, DbType.String, size: 5);
        parameters.Add("@NumeroCortoSucursal", factura.NumeroCortoSucursal, DbType.Byte);
        parameters.Add("@OrdenCompra", factura.OrdenCompra, DbType.String, size: 50);
        parameters.Add("@EsMesaDeControl", factura.EsMesaDeControl, DbType.Boolean);
        parameters.Add("@CreatedByIp", "127.0.0.1", DbType.String, size: 45);

        // Parámetros estructurados (TVPs)
        var dtDetalle = CreateDetalleDataTable(factura.Detalle);
        parameters.Add("@Detalle", dtDetalle.AsTableValuedParameter("dbo.typePortal_FacturaDetalle"));

        var dtArchivos = CreateArchivoDataTable(factura.Archivos);
        parameters.Add("@Archivos", dtArchivos.AsTableValuedParameter("dbo.typePortal_FacturaArchivo"));

        var dtLocalizaciones = CreateLocalizacionDataTable();
        parameters.Add("@Localizaciones", dtLocalizaciones.AsTableValuedParameter("dbo.typePortal_FacturaLocalizacion"));

        parameters.Add("@FacturaIdGenerado", dbType: DbType.Int64, direction: ParameterDirection.Output);

        var cmd = new CommandDefinition(
            commandText: "dbo.sp_Portal_RegistrarFacturaCompleta",
            parameters: parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct
        );

        await connection.ExecuteAsync(cmd);
        return parameters.Get<long>("@FacturaIdGenerado");
    }

    public async Task<PaginatedResult<FacturaResumenDto>> ListarFacturasProveedorSpAsync(int proveedorId, FacturaFiltroDto filtro, CancellationToken ct = default)
    {
        using var connection = CreateConnection();
        var parameters = new DynamicParameters();
        parameters.Add("@ProveedorId", proveedorId, DbType.Int32);
        parameters.Add("@FechaInicio", filtro.FechaInicio, DbType.DateTime2);
        parameters.Add("@FechaFin", filtro.FechaFin, DbType.DateTime2);
        parameters.Add("@FolioFactura", string.IsNullOrWhiteSpace(filtro.FolioFactura) ? null : filtro.FolioFactura.Trim(), DbType.String, size: 40);
        parameters.Add("@EstatusValidacion", filtro.EstatusValidacion, DbType.Byte);
        parameters.Add("@Pagina", filtro.Pagina, DbType.Int32);
        parameters.Add("@TamanoPagina", filtro.RegistrosPorPagina, DbType.Int32);

        var cmd = new CommandDefinition(
            commandText: "dbo.sp_Portal_ListarFacturasProveedor",
            parameters: parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct
        );

        using var multi = await connection.QueryMultipleAsync(cmd);
        int totalRegistros = await multi.ReadFirstAsync<int>();
        var items = (await multi.ReadAsync<FacturaResumenDto>()).ToList();

        return new PaginatedResult<FacturaResumenDto>(items, totalRegistros, filtro.Pagina, filtro.RegistrosPorPagina);
    }

    public async Task<FacturaDetalleDto?> ObtenerFacturaDetalleSpAsync(long facturaId, int proveedorId, CancellationToken ct = default)
    {
        using var connection = CreateConnection();
        var parameters = new DynamicParameters();
        parameters.Add("@FacturaId", facturaId, DbType.Int64);
        parameters.Add("@ProveedorId", proveedorId, DbType.Int32);

        var cmd = new CommandDefinition(
            commandText: "dbo.sp_Portal_ObtenerFacturaDetalle",
            parameters: parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct
        );

        using var multi = await connection.QueryMultipleAsync(cmd);
        var cabecera = await multi.ReadFirstOrDefaultAsync<FacturaDetalleDto>();
        if (cabecera == null) return null;

        var partidas = (await multi.ReadAsync<FacturaDetalleItem>()).ToList();
        var archivos = (await multi.ReadAsync<FacturaArchivoItem>()).ToList();

        cabecera.Partidas = partidas;
        cabecera.Archivos = archivos;

        return cabecera;
    }

    // ============================================================================
    // MÉTODOS DE COMPATIBILIDAD
    // ============================================================================

    public async Task<int> InsertarFacturaSpAsync(Factura factura, CancellationToken ct = default)
    {
        long nuevoId = await RegistrarFacturaCompletaSpAsync(factura, string.Empty, ct);
        return (int)nuevoId;
    }

    public async Task<Factura?> ObtenerFacturaPorIdSpAsync(int id, CancellationToken ct = default)
    {
        var detalle = await ObtenerFacturaDetalleSpAsync(id, 0, ct);
        if (detalle == null) return null;

        return new Factura
        {
            FacturaId = detalle.FacturaId,
            ProveedorId = detalle.ProveedorId,
            CodigoProveedor = detalle.CodigoProveedor,
            UUID = detalle.UUID,
            Serie = detalle.Serie,
            FolioFactura = detalle.FolioFactura,
            FechaFactura = detalle.FechaFactura,
            FechaRecepcion = detalle.FechaRecepcion,
            Subtotal = detalle.Subtotal,
            Descuento = detalle.Descuento,
            IvaTrasladado = detalle.IvaTrasladado,
            IvaRetenido = detalle.IvaRetenido,
            CostoTotal = detalle.CostoTotal,
            Moneda = detalle.Moneda,
            TipoCambio = detalle.TipoCambio,
            CodigoUsoCFDI = detalle.CodigoUsoCFDI,
            CodigoCFDIMetodoPago = detalle.CodigoCFDIMetodoPago,
            CodigoCFDIFormaPago = detalle.CodigoCFDIFormaPago,
            RegimenFiscalEmisor = detalle.RegimenFiscalEmisor,
            RegimenFiscalReceptor = detalle.RegimenFiscalReceptor,
            NumeroCortoSucursal = detalle.NumeroCortoSucursal,
            NombreSucursal = detalle.NombreSucursal,
            OrdenCompra = detalle.OrdenCompra,
            EsMesaDeControl = detalle.EsMesaDeControl,
            EstatusValidacion = detalle.EstatusValidacion,
            MotivoRechazo = detalle.MotivoRechazo,
            EstatusSincronizacion = detalle.EstatusSincronizacion,
            NumeroDocumentoCentral = detalle.NumeroDocumentoCentral,
            FechaSincronizacion = detalle.FechaSincronizacion,
            Observaciones = detalle.Observaciones,
            ArchivoXmlNombreOriginal = detalle.ArchivoXmlNombreOriginal,
            ArchivoPdfNombreOriginal = detalle.ArchivoPdfNombreOriginal,
            Detalle = detalle.Partidas,
            Archivos = detalle.Archivos
        };
    }

    public async Task<Factura?> ObtenerFacturaPorUuidSpAsync(string uuid, CancellationToken ct = default)
    {
        using var connection = CreateConnection();
        var p = new DynamicParameters();
        p.Add("@CodigoProveedor", string.Empty, DbType.String, size: 15);
        p.Add("@FolioFactura", string.Empty, DbType.String, size: 40);
        p.Add("@UUID", uuid, DbType.String, size: 36);

        var val = await ValidarFacturaPreviaSpAsync(string.Empty, string.Empty, uuid, ct);
        if (val.ExisteUUID)
        {
            return new Factura { UUID = uuid };
        }

        return null;
    }

    public async Task<IEnumerable<Factura>> ListarFacturasPorProveedorSpAsync(int proveedorId, CancellationToken ct = default)
    {
        var filtro = new FacturaFiltroDto { Pagina = 1, RegistrosPorPagina = 100 };
        var paginado = await ListarFacturasProveedorSpAsync(proveedorId, filtro, ct);

        return paginado.Items.Select(r => new Factura
        {
            FacturaId = r.FacturaId,
            ProveedorId = r.ProveedorId,
            CodigoProveedor = r.CodigoProveedor ?? string.Empty,
            UUID = r.UUID,
            Serie = r.Serie,
            FolioFactura = r.FolioFactura,
            FechaFactura = r.FechaFactura,
            FechaRecepcion = r.FechaRecepcion,
            Subtotal = r.Subtotal,
            Descuento = r.Descuento,
            IvaTrasladado = r.IvaTrasladado,
            CostoTotal = r.CostoTotal,
            Moneda = r.Moneda,
            NumeroCortoSucursal = r.NumeroCortoSucursal,
            NombreSucursal = r.NombreSucursal,
            OrdenCompra = r.OrdenCompra,
            EsMesaDeControl = r.EsMesaDeControl,
            EstatusValidacion = r.EstatusValidacion,
            MotivoRechazo = r.MotivoRechazo,
            EstatusSincronizacion = r.EstatusSincronizacion,
            NumeroDocumentoCentral = r.NumeroDocumentoCentral
        }).ToList();
    }

    public async Task<IEnumerable<Factura>> ListarTodasFacturasSpAsync(EstadoFactura? estatus, CancellationToken ct = default)
    {
        return await ListarFacturasPorProveedorSpAsync(0, ct);
    }

    public async Task<PaginatedResult<Factura>> ListarPaginadoSpAsync(FacturaFiltroDto filtro, int? proveedorId, CancellationToken ct = default)
    {
        var resultadoDto = await ListarFacturasProveedorSpAsync(proveedorId ?? 0, filtro, ct);

        var items = resultadoDto.Items.Select(r => new Factura
        {
            FacturaId = r.FacturaId,
            ProveedorId = r.ProveedorId,
            CodigoProveedor = r.CodigoProveedor ?? string.Empty,
            UUID = r.UUID,
            Serie = r.Serie,
            FolioFactura = r.FolioFactura,
            FechaFactura = r.FechaFactura,
            FechaRecepcion = r.FechaRecepcion,
            Subtotal = r.Subtotal,
            Descuento = r.Descuento,
            IvaTrasladado = r.IvaTrasladado,
            CostoTotal = r.CostoTotal,
            Moneda = r.Moneda,
            NumeroCortoSucursal = r.NumeroCortoSucursal,
            NombreSucursal = r.NombreSucursal,
            OrdenCompra = r.OrdenCompra,
            EsMesaDeControl = r.EsMesaDeControl,
            EstatusValidacion = r.EstatusValidacion,
            MotivoRechazo = r.MotivoRechazo,
            EstatusSincronizacion = r.EstatusSincronizacion,
            NumeroDocumentoCentral = r.NumeroDocumentoCentral
        }).ToList();

        return new PaginatedResult<Factura>(items, resultadoDto.TotalRecords, resultadoDto.PageNumber, resultadoDto.PageSize);
    }

    public async Task<bool> ActualizarEstatusFacturaSpAsync(int facturaId, EstadoFactura nuevoEstatus, string? motivoRechazo, CancellationToken ct = default)
    {
        // En PortalProveedores_DB las facturas se actualizan conforme a su flujo y auditoría
        return true;
    }

    public async Task<bool> ExisteUuidSpAsync(string uuid, CancellationToken ct = default)
    {
        var val = await ValidarFacturaPreviaSpAsync(string.Empty, string.Empty, uuid, ct);
        return val.ExisteUUID;
    }

    // ============================================================================
    // CONSTRUCCIÓN DE TABLE-VALUED PARAMETERS (TVPs)
    // ============================================================================

    private static DataTable CreateDetalleDataTable(IEnumerable<FacturaDetalleItem> items)
    {
        var dt = new DataTable();
        dt.Columns.Add("Renglon", typeof(short));
        dt.Columns.Add("CodigoArticulo", typeof(string));
        dt.Columns.Add("ClaveProdServSat", typeof(string));
        dt.Columns.Add("Descripcion", typeof(string));
        dt.Columns.Add("Unidad", typeof(string));
        dt.Columns.Add("Cantidad", typeof(decimal));
        dt.Columns.Add("PrecioUnitarioSinDescuento", typeof(decimal));
        dt.Columns.Add("Descuento", typeof(string));
        dt.Columns.Add("PrecioUnitarioConDescuento", typeof(decimal));
        dt.Columns.Add("Importe", typeof(decimal));
        dt.Columns.Add("PorcentajeRetencion", typeof(decimal));
        dt.Columns.Add("MontoRetencion", typeof(decimal));
        dt.Columns.Add("CantidadReal", typeof(decimal));

        if (items != null)
        {
            foreach (var item in items)
            {
                dt.Rows.Add(
                    item.Renglon,
                    item.CodigoArticulo ?? string.Empty,
                    item.ClaveProdServSat ?? string.Empty,
                    item.Descripcion ?? string.Empty,
                    item.Unidad ?? "PZA",
                    item.Cantidad,
                    item.PrecioUnitarioSinDescuento,
                    item.Descuento ?? "0",
                    item.PrecioUnitarioConDescuento,
                    item.Importe,
                    item.PorcentajeRetencion,
                    item.MontoRetencion,
                    item.CantidadReal
                );
            }
        }
        return dt;
    }

    private static DataTable CreateArchivoDataTable(IEnumerable<FacturaArchivoItem> items)
    {
        var dt = new DataTable();
        dt.Columns.Add("TipoArchivo", typeof(string));
        dt.Columns.Add("NombreOriginal", typeof(string));
        dt.Columns.Add("NombreAlmacenamiento", typeof(string));
        dt.Columns.Add("RutaFisicaSegura", typeof(string));
        dt.Columns.Add("HashSha256", typeof(string));
        dt.Columns.Add("TamanoBytes", typeof(int));
        dt.Columns.Add("XmlContenido", typeof(string));

        if (items != null)
        {
            foreach (var item in items)
            {
                dt.Rows.Add(
                    item.TipoArchivo ?? "XML",
                    item.NombreOriginal ?? string.Empty,
                    item.NombreAlmacenamiento ?? string.Empty,
                    item.RutaFisicaSegura ?? string.Empty,
                    item.HashSha256 ?? string.Empty,
                    item.TamanoBytes,
                    (object?)item.XmlContenido ?? DBNull.Value
                );
            }
        }
        return dt;
    }

    private static DataTable CreateLocalizacionDataTable()
    {
        var dt = new DataTable();
        dt.Columns.Add("CodigoArticulo", typeof(string));
        dt.Columns.Add("Cantidad", typeof(decimal));
        dt.Columns.Add("Localizacion", typeof(string));
        dt.Columns.Add("DOT", typeof(string));
        return dt;
    }
}
