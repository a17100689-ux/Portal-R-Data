-- ============================================================================
-- SCRIPT 03: PROCEDIMIENTOS DE SINCRONIZACIÓN Y PUENTE CON BD CENTRAL
-- PROYECTO: Portal de Facturación de Proveedores (Portal R-Data)
-- BASE DE DATOS: PortalProveedores_DB (Conexión / Puente con Punto_de_Venta)
-- ARQUITECTURA: DB_Optimizer_Pro
-- ============================================================================

USE [PortalProveedores_DB];
GO

-- ============================================================================
-- 1. GESTIÓN DE LA COLA OUTBOX (DESPACHO CONCURRENTE SEGURO)
-- ============================================================================

CREATE OR ALTER PROCEDURE dbo.sp_Sync_ObtenerLotePendiente
    @TamanoLote INT = 10
AS
BEGIN
    SET NOCOUNT ON;

    -- Concurrencia pesimista sin bloqueo de tablas completas:
    -- UPDLOCK bloquea las filas seleccionadas, READPAST salta filas ya tomadas por otro proceso/worker.
    ;WITH SiguienteLote AS (
        SELECT TOP (@TamanoLote)
            Q.SyncId,
            Q.FacturaId,
            Q.TipoOperacion,
            Q.EstadoSync,
            Q.Intentos
        FROM dbo.Sync_Transacciones_Cola Q WITH (UPDLOCK, READPAST)
        WHERE Q.EstadoSync = 'PENDIENTE'
          AND Q.Intentos < Q.MaxIntentos
        ORDER BY Q.SyncId ASC
    )
    UPDATE SiguienteLote
    SET EstadoSync = 'EN_PROCESO';

    -- Retorna los datos completos de las facturas que componen el lote
    SELECT 
        Q.SyncId,
        Q.FacturaId,
        Q.TipoOperacion,
        Q.Intentos,
        F.CodigoProveedor,
        F.UUID,
        F.Serie,
        F.FolioFactura,
        F.FechaFactura,
        F.Subtotal,
        F.Descuento,
        F.IvaTrasladado,
        F.IvaRetenido,
        F.CostoTotal,
        F.Moneda,
        F.TipoCambio,
        F.CodigoUsoCFDI,
        F.CodigoCFDIMetodoPago,
        F.CodigoCFDIFormaPago,
        F.RegimenFiscalEmisor,
        F.RegimenFiscalReceptor,
        F.NumeroCortoSucursal,
        F.OrdenCompra,
        F.EsMesaDeControl
    FROM dbo.Sync_Transacciones_Cola Q WITH (NOLOCK)
    INNER JOIN dbo.Facturas_Cabecera F WITH (NOLOCK) ON Q.FacturaId = F.FacturaId
    WHERE Q.EstadoSync = 'EN_PROCESO';
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Sync_ConfirmarSincronizacion
    @SyncId BIGINT,
    @FacturaId BIGINT,
    @NumeroDocumentoCentral DECIMAL(9,0)
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRANSACTION;

    UPDATE dbo.Sync_Transacciones_Cola
    SET EstadoSync = 'COMPLETADO',
        ProcesadoAt = SYSUTCDATETIME(),
        UltimoError = NULL
    WHERE SyncId = @SyncId;

    UPDATE dbo.Facturas_Cabecera
    SET EstatusSincronizacion = 2, -- Sincronizada OK
        NumeroDocumentoCentral = @NumeroDocumentoCentral,
        FechaSincronizacion = SYSUTCDATETIME(),
        MensajeErrorSincronizacion = NULL,
        UpdatedAt = SYSUTCDATETIME()
    WHERE FacturaId = @FacturaId;

    COMMIT TRANSACTION;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Sync_RegistrarFallo
    @SyncId BIGINT,
    @FacturaId BIGINT,
    @MensajeError VARCHAR(1000)
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRANSACTION;

    UPDATE dbo.Sync_Transacciones_Cola
    SET Intentos = Intentos + 1,
        EstadoSync = CASE WHEN (Intentos + 1) >= MaxIntentos THEN 'FALLIDO' ELSE 'PENDIENTE' END,
        UltimoError = @MensajeError
    WHERE SyncId = @SyncId;

    UPDATE dbo.Facturas_Cabecera
    SET EstatusSincronizacion = 3, -- Error de Sincronización
        MensajeErrorSincronizacion = @MensajeError,
        UpdatedAt = SYSUTCDATETIME()
    WHERE FacturaId = @FacturaId;

    COMMIT TRANSACTION;
END
GO

-- ============================================================================
-- 2. PROCEDIMIENTO ATÓMICO DE INSERCIÓN EN ERP CENTRAL (Punto_de_Venta)
-- ============================================================================

CREATE OR ALTER PROCEDURE dbo.sp_Sync_EjecutarInsertCentral_EnMismaInstancia
    @FacturaId BIGINT,
    @NumeroDocumentoAsignado DECIMAL(9,0) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @CodigoProveedor VARCHAR(15);
    DECLARE @UUID VARCHAR(36);
    DECLARE @FolioFactura VARCHAR(40);
    DECLARE @FechaFactura DATETIME2(0);
    DECLARE @Subtotal DECIMAL(18,2);
    DECLARE @Descuento DECIMAL(18,2);
    DECLARE @IvaTrasladado DECIMAL(18,2);
    DECLARE @CostoTotal DECIMAL(18,2);
    DECLARE @CodigoUsoCFDI VARCHAR(10);
    DECLARE @CodigoCFDIMetodoPago VARCHAR(10);
    DECLARE @CodigoCFDIFormaPago VARCHAR(10);
    DECLARE @RegimenEmisor VARCHAR(5);
    DECLARE @RegimenReceptor VARCHAR(5);
    DECLARE @NumeroCortoSucursal TINYINT;
    DECLARE @OrdenCompra VARCHAR(50);
    DECLARE @EsMesaDeControl BIT;
    DECLARE @CodigoRegion TINYINT;
    DECLARE @XmlContenido VARCHAR(MAX);
    DECLARE @NombrePdf VARCHAR(200);
    DECLARE @RutaPdf VARCHAR(300);

    -- 1. Obtener datos de la factura en Portal
    SELECT 
        @CodigoProveedor = F.CodigoProveedor,
        @UUID = F.UUID,
        @FolioFactura = F.FolioFactura,
        @FechaFactura = F.FechaFactura,
        @Subtotal = F.Subtotal,
        @Descuento = F.Descuento,
        @IvaTrasladado = F.IvaTrasladado,
        @CostoTotal = F.CostoTotal,
        @CodigoUsoCFDI = F.CodigoUsoCFDI,
        @CodigoCFDIMetodoPago = F.CodigoCFDIMetodoPago,
        @CodigoCFDIFormaPago = F.CodigoCFDIFormaPago,
        @RegimenEmisor = F.RegimenFiscalEmisor,
        @RegimenReceptor = F.RegimenFiscalReceptor,
        @NumeroCortoSucursal = F.NumeroCortoSucursal,
        @OrdenCompra = ISNULL(F.OrdenCompra, ''),
        @EsMesaDeControl = F.EsMesaDeControl
    FROM dbo.Facturas_Cabecera F
    WHERE F.FacturaId = @FacturaId;

    -- Obtener archivos
    SELECT @XmlContenido = XmlContenido 
    FROM dbo.Facturas_Archivos 
    WHERE FacturaId = @FacturaId AND TipoArchivo = 'XML';

    SELECT @NombrePdf = NombreAlmacenamiento, @RutaPdf = RutaFisicaSegura 
    FROM dbo.Facturas_Archivos 
    WHERE FacturaId = @FacturaId AND TipoArchivo = 'PDF';

    -- Obtener región de la sucursal desde Punto_de_Venta mapeando con Datos_De_Sistema_Central
    SELECT TOP (1) 
        @CodigoRegion = DSC.Codigo_De_Region
    FROM Punto_de_Venta.dbo.Sucursales S WITH (NOLOCK)
    INNER JOIN Punto_de_Venta.dbo.Datos_De_Sistema_Central DSC WITH (NOLOCK) 
        ON UPPER(LTRIM(RTRIM(S.Region))) = UPPER(LTRIM(RTRIM(DSC.Region)))
    WHERE S.Numero_Corto_De_Sucursal = @NumeroCortoSucursal;

    IF @CodigoRegion IS NULL
        SET @CodigoRegion = 1; -- Default Occidente

    BEGIN TRY
        BEGIN TRANSACTION;

        -- 2. Incrementar y asignar consecutivo atómico en Datos_De_Sistema_Central
        UPDATE Punto_de_Venta.dbo.Datos_De_Sistema_Central WITH (UPDLOCK, ROWLOCK)
        SET Numero_De_Documento = Numero_De_Documento + 1,
            @NumeroDocumentoAsignado = Numero_De_Documento + 1
        WHERE Codigo_De_Region = @CodigoRegion;

        -- 3. Inserción en Compras_Y_Devoluciones o Compras_Y_Devoluciones_Mesa_De_Control
        IF @EsMesaDeControl = 1
        BEGIN
            INSERT INTO Punto_de_Venta.dbo.Compras_Y_Devoluciones_Mesa_De_Control (
                Numero_De_Documento, Folio_De_Factura, Fecha_De_Factura, Fecha_Y_Hora_Del_Documento,
                Cantidad_Total_De_Articulos, Fecha_De_Recepcion, Descuento_Aplicado_A_La_Factura,
                Empleado_Registro, Empleado_Que_Incurre_En_Gasto, Empleado_Firmado_En_El_Sistema,
                Codigo_De_Proveedor, Costo_Total, Iva_Aplicado_Al_Documento, Estatus_De_Compra,
                Estatus_De_Documento, Numero_Corto_De_Sucursal, Condiciones_De_Pago, Forma_De_Pago,
                UUID, Codigo_UsoCFDI, Codigo_CFDI_Metodo_De_Pago, Codigo_CFDI_Forma_De_Pago,
                Observaciones, Estatus_De_Replicacion, Fecha_Y_Hora_De_Ultima_Actualizacion,
                Regimen_Fiscal_Del_Receptor, Regimen_Fiscal_Del_Emisor, VentasRemotas_Orden_De_Compra
            )
            VALUES (
                @NumeroDocumentoAsignado, @FolioFactura, CAST(@FechaFactura AS DATE), GETDATE(),
                (SELECT SUM(Cantidad) FROM dbo.Facturas_Detalle WHERE FacturaId = @FacturaId),
                GETDATE(), CAST(@Descuento AS VARCHAR(15)), 'PORTAL', '0', 'PORTAL',
                @CodigoProveedor, @CostoTotal, 16, 1, 1, @NumeroCortoSucursal, 1, 1,
                @UUID, @CodigoUsoCFDI, @CodigoCFDIMetodoPago, @CodigoCFDIFormaPago,
                'Factura ingresada vía Portal Web de Proveedores', 0, GETDATE(),
                @RegimenReceptor, @RegimenEmisor, @OrdenCompra
            );

            -- Detalle Mesa de Control (Sin columna computada Precio_Extendido_Con_Descuento)
            INSERT INTO Punto_de_Venta.dbo.Compras_Y_Devoluciones_Mesa_De_Control_Detalle (
                Numero_De_Documento, Renglon, Codigo_De_Articulo, Descripcion, Unidad,
                Cantidad, Precio_Unitario_Sin_Descuento, Descuento, Incremento,
                Clave_De_Producto_O_Servicio, Precio_Unitario_Con_Descuento,
                Porcentaje_Retencion, Monto_Retencion, Cantidad_Real
            )
            SELECT 
                @NumeroDocumentoAsignado, Renglon, CodigoArticulo, Descripcion, Unidad,
                Cantidad, PrecioUnitarioSinDescuento, Descuento, 0,
                ClaveProdServSat, PrecioUnitarioConDescuento,
                PorcentajeRetencion, MontoRetencion, CantidadReal
            FROM dbo.Facturas_Detalle
            WHERE FacturaId = @FacturaId;
        END
        ELSE
        BEGIN
            INSERT INTO Punto_de_Venta.dbo.Compras_Y_Devoluciones (
                Numero_De_Documento, Folio_De_Factura, Fecha_De_Factura, Fecha_Y_Hora_Del_Documento,
                Cantidad_Total_De_Articulos, Fecha_De_Recepcion, Descuento_Aplicado_A_La_Factura,
                Empleado_Registro, Empleado_Que_Incurre_En_Gasto, Empleado_Firmado_En_El_Sistema,
                Codigo_De_Proveedor, Costo_Total, Iva_Aplicado_Al_Documento, Estatus_De_Compra,
                Estatus_De_Documento, Numero_Corto_De_Sucursal, Condiciones_De_Pago, Forma_De_Pago,
                UUID, Codigo_UsoCFDI, Codigo_CFDI_Metodo_De_Pago, Codigo_CFDI_Forma_De_Pago,
                Observaciones, Estatus_De_Replicacion, Fecha_Y_Hora_De_Ultima_Actualizacion,
                Regimen_Fiscal_Del_Receptor, Regimen_Fiscal_Del_Emisor, VentasRemotas_Orden_De_Compra
            )
            VALUES (
                @NumeroDocumentoAsignado, @FolioFactura, CAST(@FechaFactura AS DATE), GETDATE(),
                (SELECT SUM(Cantidad) FROM dbo.Facturas_Detalle WHERE FacturaId = @FacturaId),
                GETDATE(), CAST(@Descuento AS VARCHAR(15)), 'PORTAL', '0', 'PORTAL',
                @CodigoProveedor, @CostoTotal, 16, 1, 1, @NumeroCortoSucursal, 1, 1,
                @UUID, @CodigoUsoCFDI, @CodigoCFDIMetodoPago, @CodigoCFDIFormaPago,
                'Factura ingresada vía Portal Web de Proveedores', 0, GETDATE(),
                @RegimenReceptor, @RegimenEmisor, @OrdenCompra
            );

            -- Detalle Compra Normal (Sin columna computada Precio_Extendido_Con_Descuento)
            INSERT INTO Punto_de_Venta.dbo.Compras_Y_Devoluciones_Detalle (
                Numero_De_Documento, Renglon, Codigo_De_Articulo, Descripcion, Unidad,
                Cantidad, Precio_Unitario_Sin_Descuento, Descuento, Incremento,
                Clave_De_Producto_O_Servicio, Precio_Unitario_Con_Descuento,
                Porcentaje_Retencion, Monto_Retencion, Cantidad_Real
            )
            SELECT 
                @NumeroDocumentoAsignado, Renglon, CodigoArticulo, Descripcion, Unidad,
                Cantidad, PrecioUnitarioSinDescuento, Descuento, 0,
                ClaveProdServSat, PrecioUnitarioConDescuento,
                PorcentajeRetencion, MontoRetencion, CantidadReal
            FROM dbo.Facturas_Detalle
            WHERE FacturaId = @FacturaId;
        END

        -- 4. Inserción en Repositorio_De_Compras_Xml
        INSERT INTO Punto_de_Venta.dbo.Repositorio_De_Compras_Xml (
            Numero_De_Documento, UUID, XML, Tipo_De_Documento, Fecha,
            Numero_Corto_De_Sucursal, Estatus_De_Replicacion,
            Fecha_Y_Hora_De_Ultima_Actualizacion, Ruta_Servidor, Nombre_Archivo_PDF
        )
        VALUES (
            @NumeroDocumentoAsignado, @UUID, ISNULL(@XmlContenido, ''), 1, GETDATE(),
            @NumeroCortoSucursal, 0, GETDATE(), ISNULL(@RutaPdf, ''), ISNULL(@NombrePdf, '')
        );

        -- 5. Inserción en Localizador si aplica
        IF EXISTS (SELECT 1 FROM dbo.Facturas_Localizaciones WHERE FacturaId = @FacturaId)
        BEGIN
            INSERT INTO Punto_de_Venta.dbo.Localizador_Localizaciones_Detalle (
                Numero_De_Documento, Folio_Del_Documento, Codigo_De_Articulo, Descripcion,
                Cantidad, Localizacion, Fecha, DOT, Codigo_De_Empleado, Numero_Corto_De_Sucursal,
                Tipo_Documento, Fecha_Y_Hora_De_Ultima_Actualizacion
            )
            SELECT 
                @NumeroDocumentoAsignado, @FolioFactura, L.CodigoArticulo, D.Descripcion,
                L.Cantidad, L.Localizacion, CAST(GETDATE() AS DATE), ISNULL(L.DOT, '0000'),
                'PORTAL', @NumeroCortoSucursal, 1, GETDATE()
            FROM dbo.Facturas_Localizaciones L
            INNER JOIN dbo.Facturas_Detalle D ON L.FacturaId = D.FacturaId AND L.CodigoArticulo = D.CodigoArticulo
            WHERE L.FacturaId = @FacturaId;
        END

        COMMIT TRANSACTION;

    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;

        DECLARE @Err NVARCHAR(4000) = ERROR_MESSAGE();
        RAISERROR(@Err, 16, 1);
    END CATCH
END
GO

-- ============================================================================
-- 3. REFRESH Y SINCRONIZACIÓN DE CATÁLOGOS CENTRAL -> PORTAL
-- ============================================================================

CREATE OR ALTER PROCEDURE dbo.sp_Sync_RefrescarCatalogosDesdeCentral
AS
BEGIN
    SET NOCOUNT ON;

    -- 1. Sincronizar Proveedores (incluyendo CP, Telefono, Email)
    MERGE dbo.Cat_Proveedores AS Target
    USING (
        SELECT 
            V.Codigo_De_Proveedor COLLATE DATABASE_DEFAULT AS CodigoProveedor,
            V.RFC COLLATE DATABASE_DEFAULT AS RFC,
            V.Nombre_De_Proveedor COLLATE DATABASE_DEFAULT AS RazonSocial,
            V.CondicionesPago COLLATE DATABASE_DEFAULT AS CondicionesPago,
            CASE WHEN V.[Requiere validar compra] COLLATE DATABASE_DEFAULT = 'Si' THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END AS RequiereValidarCompra,
            V.Orden_Compra_Obligatoria AS OrdenCompraObligatoria,
            V.Es_Proveedor_Nacional AS EsProveedorNacional,
            NULLIF(LTRIM(RTRIM(P.CP)), '') COLLATE DATABASE_DEFAULT AS CodigoPostal,
            NULLIF(LTRIM(RTRIM(P.Telefono)), '') COLLATE DATABASE_DEFAULT AS Telefono,
            COALESCE(NULLIF(LTRIM(RTRIM(P.Email_Portal)), ''), NULLIF(LTRIM(RTRIM(P.Email)), '')) COLLATE DATABASE_DEFAULT AS EmailContacto
        FROM Punto_de_Venta.dbo.vtaProveedores_ProveedoresCompras V
        LEFT JOIN Punto_de_Venta.dbo.Proveedores P 
            ON V.Codigo_De_Proveedor = P.Codigo_De_Proveedor COLLATE DATABASE_DEFAULT
    ) AS Source
    ON (Target.CodigoProveedor = Source.CodigoProveedor COLLATE DATABASE_DEFAULT)
    WHEN MATCHED THEN
        UPDATE SET 
            Target.RFC = Source.RFC,
            Target.RazonSocial = Source.RazonSocial,
            Target.CondicionesPago = Source.CondicionesPago,
            Target.RequiereValidarCompra = Source.RequiereValidarCompra,
            Target.OrdenCompraObligatoria = Source.OrdenCompraObligatoria,
            Target.EsProveedorNacional = Source.EsProveedorNacional,
            Target.CodigoPostal = COALESCE(Source.CodigoPostal, Target.CodigoPostal),
            Target.Telefono = COALESCE(Source.Telefono, Target.Telefono),
            Target.EmailContacto = COALESCE(Source.EmailContacto, Target.EmailContacto),
            Target.Activo = 1,
            Target.UpdatedAt = SYSUTCDATETIME()
    WHEN NOT MATCHED BY TARGET THEN
        INSERT (
            CodigoProveedor, RFC, RazonSocial, CondicionesPago, 
            RequiereValidarCompra, OrdenCompraObligatoria, EsProveedorNacional, 
            CodigoPostal, Telefono, EmailContacto, Activo, CreatedAt, UpdatedAt
        )
        VALUES (
            Source.CodigoProveedor, Source.RFC, Source.RazonSocial, Source.CondicionesPago, 
            Source.RequiereValidarCompra, Source.OrdenCompraObligatoria, Source.EsProveedorNacional, 
            Source.CodigoPostal, Source.Telefono, Source.EmailContacto, 1, SYSUTCDATETIME(), SYSUTCDATETIME()
        );

    -- 2. Sincronizar Sucursales
    MERGE dbo.Cat_Sucursales AS Target
    USING (
        SELECT 
            Numero_Corto_De_Sucursal AS NumeroCortoSucursal,
            Codigo_De_Sucursal AS CodigoSucursal,
            Descripcion,
            Region
        FROM Punto_de_Venta.dbo.vtaSucursales_PortalProveedores
    ) AS Source
    ON (Target.NumeroCortoSucursal = Source.NumeroCortoSucursal)
    WHEN MATCHED THEN
        UPDATE SET 
            Target.Descripcion = Source.Descripcion,
            Target.Region = Source.Region,
            Target.Activo = 1
    WHEN NOT MATCHED BY TARGET THEN
        INSERT (NumeroCortoSucursal, CodigoSucursal, Descripcion, Region, Activo, CreatedAt)
        VALUES (Source.NumeroCortoSucursal, Source.CodigoSucursal, Source.Descripcion, Source.Region, 1, SYSUTCDATETIME());

    -- 3. Sincronizar Artículos y Reglas
    MERGE dbo.Cat_Articulos_Reglas AS Target
    USING (
        SELECT 
            A.Codigo_De_Articulo AS CodigoArticulo,
            A.Descripcion,
            A.Codigo_De_Linea AS CodigoLinea,
            ISNULL(A.IdProductosyServicios, '') AS IdProductosyServicios,
            ISNULL(CAST(Art.Factor_De_Conversion AS DECIMAL(12,4)), 1.0) AS FactorConversion,
            CASE WHEN A.Codigo_De_Linea = '04049' THEN 1 ELSE 0 END AS EsPlomo,
            CASE WHEN A.Codigo_De_Linea IN ('04028', '03043', '04049', '03032', '04048') THEN 1 ELSE 0 END AS PermitidoCFDI_G03,
            CASE WHEN L.Codigo_De_Articulo IS NOT NULL THEN 1 ELSE 0 END AS AplicaLocalizador
        FROM Punto_de_Venta.dbo.vtaCompras_ArticulosParaComprasInventariadas A
        INNER JOIN Punto_de_Venta.dbo.Articulos Art 
            ON A.Codigo_De_Articulo = Art.Codigo_De_Articulo COLLATE DATABASE_DEFAULT
        LEFT JOIN Punto_de_Venta.dbo.vtaLocalizador_ArticulosQueAplicanParaLocalizador L 
            ON A.Codigo_De_Articulo = L.Codigo_De_Articulo COLLATE DATABASE_DEFAULT
    ) AS Source
    ON (Target.CodigoArticulo = Source.CodigoArticulo COLLATE DATABASE_DEFAULT)
    WHEN MATCHED THEN
        UPDATE SET 
            Target.Descripcion = Source.Descripcion,
            Target.CodigoLinea = Source.CodigoLinea,
            Target.IdProductosyServicios = Source.IdProductosyServicios,
            Target.FactorConversion = Source.FactorConversion,
            Target.EsPlomo = Source.EsPlomo,
            Target.PermitidoCFDI_G03 = Source.PermitidoCFDI_G03,
            Target.AplicaLocalizador = Source.AplicaLocalizador,
            Target.Activo = 1
    WHEN NOT MATCHED BY TARGET THEN
        INSERT (CodigoArticulo, Descripcion, CodigoLinea, IdProductosyServicios, FactorConversion, EsPlomo, PermitidoCFDI_G03, AplicaLocalizador, Activo, CreatedAt)
        VALUES (Source.CodigoArticulo, Source.Descripcion, Source.CodigoLinea, Source.IdProductosyServicios, Source.FactorConversion, Source.EsPlomo, Source.PermitidoCFDI_G03, Source.AplicaLocalizador, 1, SYSUTCDATETIME());

    SELECT 'Catálogos sincronizados exitosamente con la base central' AS Resultado;
END
GO

-- ============================================================================
-- 4. ACTUALIZACIÓN DE ESTATUS DE ENTREGA DE MERCANCÍA (PUNTO 4)
-- ============================================================================

CREATE OR ALTER PROCEDURE dbo.sp_Sync_ActualizarEstatusEntregaMercancia
AS
BEGIN
    SET NOCOUNT ON;

    -- Busca facturas en el portal que ya tengan asignado NumeroDocumentoCentral
    -- pero cuyo estatus de mercancía siga 'PENDIENTE_ENTREGA'.
    -- Cruza con Compras_Y_Devoluciones en la base central para verificar si ya fue recibida en tienda/almacén.
    
    UPDATE F
    SET F.EstatusMercancia = 'ENTREGADA_SUCURSAL',
        F.FechaEntregaMercancia = C.Fecha_De_Recepcion,
        F.UpdatedAt = SYSUTCDATETIME()
    FROM dbo.Facturas_Cabecera F
    INNER JOIN Punto_de_Venta.dbo.Compras_Y_Devoluciones C WITH (NOLOCK)
        ON F.NumeroDocumentoCentral = C.Numero_De_Documento
    WHERE F.EstatusMercancia = 'PENDIENTE_ENTREGA'
      AND F.NumeroDocumentoCentral IS NOT NULL
      AND C.Estatus_De_Compra = 1
      AND C.Fecha_De_Recepcion IS NOT NULL;

    -- También para Mesa de Control
    UPDATE F
    SET F.EstatusMercancia = 'ENTREGADA_SUCURSAL',
        F.FechaEntregaMercancia = C.Fecha_De_Recepcion,
        F.UpdatedAt = SYSUTCDATETIME()
    FROM dbo.Facturas_Cabecera F
    INNER JOIN Punto_de_Venta.dbo.Compras_Y_Devoluciones_Mesa_De_Control C WITH (NOLOCK)
        ON F.NumeroDocumentoCentral = C.Numero_De_Documento
    WHERE F.EstatusMercancia = 'PENDIENTE_ENTREGA'
      AND F.NumeroDocumentoCentral IS NOT NULL
      AND C.Estatus_De_Compra = 1
      AND C.Fecha_De_Recepcion IS NOT NULL;

    SELECT @@ROWCOUNT AS FacturasActualizadasConEntrega;
END
GO

-- ============================================================================
-- 5. RESUMEN Y MÉTRICAS DE LA COLA DE SINCRONIZACIÓN
-- ============================================================================

CREATE OR ALTER PROCEDURE dbo.sp_Sync_ObtenerResumenEstado
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        ISNULL(SUM(CASE WHEN EstadoSync = 'PENDIENTE' THEN 1 ELSE 0 END), 0) AS FacturasPendientes,
        ISNULL(SUM(CASE WHEN EstadoSync = 'EN_PROCESO' THEN 1 ELSE 0 END), 0) AS FacturasEnProceso,
        ISNULL(SUM(CASE WHEN EstadoSync = 'COMPLETADO' THEN 1 ELSE 0 END), 0) AS FacturasCompletadas,
        ISNULL(SUM(CASE WHEN EstadoSync = 'FALLIDO' THEN 1 ELSE 0 END), 0) AS FacturasFallidas,
        MAX(CASE WHEN EstadoSync = 'COMPLETADO' THEN ProcesadoAt ELSE NULL END) AS UltimaSincronizacionExitosa
    FROM dbo.Sync_Transacciones_Cola WITH (NOLOCK);
END
GO
