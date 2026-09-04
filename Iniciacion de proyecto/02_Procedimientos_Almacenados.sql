-- ============================================================================
-- SCRIPT 02: PROCEDIMIENTOS ALMACENADOS PARA EL PORTAL WEB
-- PROYECTO: Portal de Facturación de Proveedores (Portal R-Data)
-- BASE DE DATOS: PortalProveedores_DB
-- ARQUITECTURA: DB_Optimizer_Pro
-- ============================================================================

USE [PortalProveedores_DB];
GO

-- ============================================================================
-- 1. PROCEDIMIENTOS DE AUTENTICACIÓN Y SEGURIDAD
-- ============================================================================

CREATE OR ALTER PROCEDURE dbo.sp_Portal_Usuario_ObtenerPorLogin
    @Identificador VARCHAR(120) -- Puede ser RFC o Email
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (1)
        U.UsuarioId,
        U.ProveedorId,
        U.RFC,
        U.Email,
        U.PasswordHash,
        U.IntentosFallidos,
        U.BloqueadoHasta,
        U.Activo AS UsuarioActivo,
        P.CodigoProveedor,
        P.RazonSocial,
        P.RequiereValidarCompra,
        P.OrdenCompraObligatoria,
        P.CondicionesPago,
        P.Activo AS ProveedorActivo
    FROM dbo.Usuarios_Proveedor U
    INNER JOIN dbo.Cat_Proveedores P ON U.ProveedorId = P.ProveedorId
    WHERE (U.Email = @Identificador OR U.RFC = @Identificador)
      AND U.Activo = 1
      AND P.Activo = 1;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Portal_Usuario_RegistrarIntentoFallido
    @UsuarioId INT,
    @MaxIntentos TINYINT = 5,
    @MinutosBloqueo INT = 15
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.Usuarios_Proveedor
    SET IntentosFallidos = IntentosFallidos + 1,
        BloqueadoHasta = CASE 
            WHEN (IntentosFallidos + 1) >= @MaxIntentos 
            THEN DATEADD(MINUTE, @MinutosBloqueo, SYSUTCDATETIME())
            ELSE NULL 
        END,
        UpdatedAt = SYSUTCDATETIME()
    WHERE UsuarioId = @UsuarioId;

    SELECT IntentosFallidos, BloqueadoHasta
    FROM dbo.Usuarios_Proveedor
    WHERE UsuarioId = @UsuarioId;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Portal_Usuario_RegistrarLoginExitoso
    @UsuarioId INT,
    @DireccionIP VARCHAR(45)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.Usuarios_Proveedor
    SET IntentosFallidos = 0,
        BloqueadoHasta = NULL,
        UltimoAcceso = SYSUTCDATETIME(),
        UpdatedAt = SYSUTCDATETIME()
    WHERE UsuarioId = @UsuarioId;

    INSERT INTO dbo.Bitacora_Accesos (UsuarioId, RFC, DireccionIP, Exitoso, Detalle, FechaAcceso)
    SELECT @UsuarioId, RFC, @DireccionIP, 1, 'Inicio de sesión exitoso', SYSUTCDATETIME()
    FROM dbo.Usuarios_Proveedor
    WHERE UsuarioId = @UsuarioId;
END
GO

-- ============================================================================
-- 2. PROCEDIMIENTOS DE VALIDACIÓN PREVIA DE FACTURAS (OPTIMIZACIÓN SARGABLE)
-- ============================================================================

CREATE OR ALTER PROCEDURE dbo.sp_Portal_ValidarFacturaPrevia
    @CodigoProveedor VARCHAR(15),
    @FolioFactura VARCHAR(40),
    @UUID VARCHAR(36)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ExisteFolio BIT = 0;
    DECLARE @ExisteUUID BIT = 0;
    DECLARE @Mensaje VARCHAR(200) = '';

    -- Búsqueda sargable directa por UUID (Index Seek en IX_Facturas_UUID)
    IF EXISTS (SELECT 1 FROM dbo.Facturas_Cabecera WITH (NOLOCK) WHERE UUID = @UUID)
    BEGIN
        SET @ExisteUUID = 1;
        SET @Mensaje = 'El UUID fiscal de la factura ya se encuentra registrado previamente en el portal.';
    END
    -- Búsqueda sargable directa por Proveedor y Folio (Index Seek en IX_Facturas_Duplicados)
    ELSE IF EXISTS (SELECT 1 FROM dbo.Facturas_Cabecera WITH (NOLOCK) 
                    WHERE CodigoProveedor = @CodigoProveedor 
                      AND FolioFactura = @FolioFactura 
                      AND EstatusValidacion <> 3) -- Excluye rechazadas si se permite reintento
    BEGIN
        SET @ExisteFolio = 1;
        SET @Mensaje = 'El folio de factura ya fue registrado para este proveedor.';
    END

    SELECT 
        CASE WHEN @ExisteUUID = 1 OR @ExisteFolio = 1 THEN 0 ELSE 1 END AS EsValido,
        @ExisteUUID AS ExisteUUID,
        @ExisteFolio AS ExisteFolio,
        @Mensaje AS MensajeError;
END
GO

-- ============================================================================
-- 3. REGISTRO TRANSACCIONAL ATÓMICO DE FACTURA COMPLETA
-- ============================================================================

CREATE OR ALTER PROCEDURE dbo.sp_Portal_RegistrarFacturaCompleta
    -- Cabecera
    @ProveedorId INT,
    @CodigoProveedor VARCHAR(15),
    @UUID VARCHAR(36),
    @Serie VARCHAR(15) = NULL,
    @FolioFactura VARCHAR(40),
    @FechaFactura DATETIME2(0),
    @Subtotal DECIMAL(18,2),
    @Descuento DECIMAL(18,2) = 0,
    @IvaTrasladado DECIMAL(18,2),
    @IvaRetenido DECIMAL(18,2) = 0,
    @CostoTotal DECIMAL(18,2),
    @Moneda VARCHAR(3) = 'MXN',
    @TipoCambio DECIMAL(12,4) = 1.0000,
    @CodigoUsoCFDI VARCHAR(10),
    @CodigoCFDIMetodoPago VARCHAR(10),
    @CodigoCFDIFormaPago VARCHAR(10),
    @RegimenFiscalEmisor VARCHAR(5),
    @RegimenFiscalReceptor VARCHAR(5),
    @NumeroCortoSucursal TINYINT,
    @OrdenCompra VARCHAR(50) = NULL,
    @EsMesaDeControl BIT = 0,
    @CreatedByIp VARCHAR(45),
    
    -- Parámetros Estructurados (TVPs)
    @Detalle dbo.typePortal_FacturaDetalle READONLY,
    @Archivos dbo.typePortal_FacturaArchivo READONLY,
    @Localizaciones dbo.typePortal_FacturaLocalizacion READONLY,
    
    -- Parámetros de salida
    @FacturaIdGenerado BIGINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        -- 1. Inserción de Cabecera
        INSERT INTO dbo.Facturas_Cabecera (
            ProveedorId, CodigoProveedor, UUID, Serie, FolioFactura,
            FechaFactura, FechaRecepcion, Subtotal, Descuento, IvaTrasladado,
            IvaRetenido, CostoTotal, Moneda, TipoCambio, CodigoUsoCFDI,
            CodigoCFDIMetodoPago, CodigoCFDIFormaPago, RegimenFiscalEmisor,
            RegimenFiscalReceptor, NumeroCortoSucursal, OrdenCompra, EsMesaDeControl,
            EstatusValidacion, EstatusSincronizacion, CreatedAt, UpdatedAt, CreatedByIp
        )
        VALUES (
            @ProveedorId, @CodigoProveedor, @UUID, @Serie, @FolioFactura,
            @FechaFactura, SYSUTCDATETIME(), @Subtotal, @Descuento, @IvaTrasladado,
            @IvaRetenido, @CostoTotal, @Moneda, @TipoCambio, @CodigoUsoCFDI,
            @CodigoCFDIMetodoPago, @CodigoCFDIFormaPago, @RegimenFiscalEmisor,
            @RegimenFiscalReceptor, @NumeroCortoSucursal, @OrdenCompra, @EsMesaDeControl,
            2, -- Validada OK (pasa validación previa)
            1, -- EstatusSincronizacion = 1 (En cola para réplica central)
            SYSUTCDATETIME(), SYSUTCDATETIME(), @CreatedByIp
        );

        SET @FacturaIdGenerado = SCOPE_IDENTITY();

        -- 2. Inserción de Partidas / Detalle
        INSERT INTO dbo.Facturas_Detalle (
            FacturaId, Renglon, CodigoArticulo, ClaveProdServSat, Descripcion,
            Unidad, Cantidad, PrecioUnitarioSinDescuento, Descuento,
            PrecioUnitarioConDescuento, Importe, PorcentajeRetencion,
            MontoRetencion, CantidadReal, CreatedAt
        )
        SELECT 
            @FacturaIdGenerado, Renglon, CodigoArticulo, ClaveProdServSat, Descripcion,
            Unidad, Cantidad, PrecioUnitarioSinDescuento, Descuento,
            PrecioUnitarioConDescuento, Importe, PorcentajeRetencion,
            MontoRetencion, CantidadReal, SYSUTCDATETIME()
        FROM @Detalle;

        -- 3. Inserción de Metadatos de Archivos (XML y PDF)
        INSERT INTO dbo.Facturas_Archivos (
            FacturaId, TipoArchivo, NombreOriginal, NombreAlmacenamiento,
            RutaFisicaSegura, HashSha256, TamanoBytes, XmlContenido, CreatedAt
        )
        SELECT 
            @FacturaIdGenerado, TipoArchivo, NombreOriginal, NombreAlmacenamiento,
            RutaFisicaSegura, HashSha256, TamanoBytes, XmlContenido, SYSUTCDATETIME()
        FROM @Archivos;

        -- 4. Inserción de Localizaciones (si aplica)
        IF EXISTS (SELECT 1 FROM @Localizaciones)
        BEGIN
            INSERT INTO dbo.Facturas_Localizaciones (
                FacturaId, CodigoArticulo, Cantidad, Localizacion, DOT, CreatedAt
            )
            SELECT 
                @FacturaIdGenerado, CodigoArticulo, Cantidad, Localizacion, DOT, SYSUTCDATETIME()
            FROM @Localizaciones;
        END

        -- 5. Encolamiento en Outbox / Cola de Sincronización hacia ERP Central
        INSERT INTO dbo.Sync_Transacciones_Cola (
            FacturaId, TipoOperacion, EstadoSync, Intentos, CreatedAt
        )
        VALUES (
            @FacturaIdGenerado,
            CASE WHEN @EsMesaDeControl = 1 THEN 'COMPRA_MESA_CONTROL' ELSE 'COMPRA_NORMAL' END,
            'PENDIENTE',
            0,
            SYSUTCDATETIME()
        );

        COMMIT TRANSACTION;

        SELECT @FacturaIdGenerado AS FacturaId, 'Factura registrada y encolada exitosamente' AS Mensaje;

    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;

        DECLARE @ErrorMsg NVARCHAR(4000) = ERROR_MESSAGE();
        DECLARE @ErrorSev INT = ERROR_SEVERITY();
        DECLARE @ErrorState INT = ERROR_STATE();

        RAISERROR(@ErrorMsg, @ErrorSev, @ErrorState);
    END CATCH
END
GO

-- ============================================================================
-- 4. CONSULTAS DE FACTURAS PARA EL PROVEEDOR (PAGINACIÓN EFICIENTE)
-- ============================================================================

CREATE OR ALTER PROCEDURE dbo.sp_Portal_ListarFacturasProveedor
    @ProveedorId INT,
    @FechaInicio DATETIME2(0) = NULL,
    @FechaFin DATETIME2(0) = NULL,
    @FolioFactura VARCHAR(40) = NULL,
    @EstatusValidacion TINYINT = NULL,
    @Pagina INT = 1,
    @TamanoPagina INT = 20
AS
BEGIN
    SET NOCOUNT ON;

    SET @Pagina = CASE WHEN @Pagina < 1 THEN 1 ELSE @Pagina END;
    SET @TamanoPagina = CASE WHEN @TamanoPagina < 1 THEN 20 ELSE @TamanoPagina END;
    DECLARE @Offset INT = (@Pagina - 1) * @TamanoPagina;

    -- Conteo total para paginación UI
    SELECT COUNT(1) AS TotalRegistros
    FROM dbo.Facturas_Cabecera WITH (NOLOCK)
    WHERE ProveedorId = @ProveedorId
      AND (@FechaInicio IS NULL OR FechaFactura >= @FechaInicio)
      AND (@FechaFin IS NULL OR FechaFactura <= @FechaFin)
      AND (@FolioFactura IS NULL OR FolioFactura LIKE @FolioFactura + '%')
      AND (@EstatusValidacion IS NULL OR EstatusValidacion = @EstatusValidacion);

    -- Registros paginados con Index Seek sobre IX_Facturas_Proveedor_Fecha
    SELECT 
        F.FacturaId,
        F.UUID,
        F.Serie,
        F.FolioFactura,
        F.FechaFactura,
        F.FechaRecepcion,
        F.Subtotal,
        F.Descuento,
        F.IvaTrasladado,
        F.CostoTotal,
        F.Moneda,
        F.NumeroCortoSucursal,
        S.Descripcion AS NombreSucursal,
        F.OrdenCompra,
        F.EsMesaDeControl,
        F.EstatusValidacion,
        F.EstatusSincronizacion,
        F.NumeroDocumentoCentral,
        F.MotivoRechazo
    FROM dbo.Facturas_Cabecera F WITH (NOLOCK)
    LEFT JOIN dbo.Cat_Sucursales S WITH (NOLOCK) ON F.NumeroCortoSucursal = S.NumeroCortoSucursal
    WHERE F.ProveedorId = @ProveedorId
      AND (@FechaInicio IS NULL OR F.FechaFactura >= @FechaInicio)
      AND (@FechaFin IS NULL OR F.FechaFactura <= @FechaFin)
      AND (@FolioFactura IS NULL OR F.FolioFactura LIKE @FolioFactura + '%')
      AND (@EstatusValidacion IS NULL OR F.EstatusValidacion = @EstatusValidacion)
    ORDER BY F.FechaFactura DESC, F.FacturaId DESC
    OFFSET @Offset ROWS
    FETCH NEXT @TamanoPagina ROWS ONLY;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Portal_ObtenerFacturaDetalle
    @FacturaId BIGINT,
    @ProveedorId INT -- Validación de pertenencia para evitar acceso indebido entre proveedores
AS
BEGIN
    SET NOCOUNT ON;

    -- 1. Cabecera
    SELECT TOP (1)
        F.FacturaId,
        F.ProveedorId,
        F.CodigoProveedor,
        F.UUID,
        F.Serie,
        F.FolioFactura,
        F.FechaFactura,
        F.FechaRecepcion,
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
        S.Descripcion AS NombreSucursal,
        F.OrdenCompra,
        F.EsMesaDeControl,
        F.EstatusValidacion,
        F.EstatusSincronizacion,
        F.NumeroDocumentoCentral,
        F.FechaSincronizacion,
        F.MotivoRechazo
    FROM dbo.Facturas_Cabecera F WITH (NOLOCK)
    LEFT JOIN dbo.Cat_Sucursales S WITH (NOLOCK) ON F.NumeroCortoSucursal = S.NumeroCortoSucursal
    WHERE F.FacturaId = @FacturaId
      AND F.ProveedorId = @ProveedorId;

    -- 2. Partidas
    SELECT 
        D.FacturaDetalleId,
        D.Renglon,
        D.CodigoArticulo,
        D.ClaveProdServSat,
        D.Descripcion,
        D.Unidad,
        D.Cantidad,
        D.PrecioUnitarioSinDescuento,
        D.Descuento,
        D.PrecioUnitarioConDescuento,
        D.Importe,
        D.PorcentajeRetencion,
        D.MontoRetencion
    FROM dbo.Facturas_Detalle D WITH (NOLOCK)
    INNER JOIN dbo.Facturas_Cabecera F WITH (NOLOCK) ON D.FacturaId = F.FacturaId
    WHERE D.FacturaId = @FacturaId
      AND F.ProveedorId = @ProveedorId
    ORDER BY D.Renglon ASC;

    -- 3. Archivos asociados
    SELECT 
        A.ArchivoId,
        A.TipoArchivo,
        A.NombreOriginal,
        A.TamanoBytes,
        A.CreatedAt AS FechaCarga
    FROM dbo.Facturas_Archivos A WITH (NOLOCK)
    INNER JOIN dbo.Facturas_Cabecera F WITH (NOLOCK) ON A.FacturaId = F.FacturaId
    WHERE A.FacturaId = @FacturaId
      AND F.ProveedorId = @ProveedorId;
END
GO

-- ============================================================================
-- 5. AUDITORÍA GENERAL
-- ============================================================================

CREATE OR ALTER PROCEDURE dbo.sp_Portal_Auditoria_Registrar
    @UsuarioId INT = NULL,
    @Modulo VARCHAR(50),
    @Accion VARCHAR(50),
    @Detalle VARCHAR(500),
    @DireccionIP VARCHAR(45)
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.Auditoria_Eventos (UsuarioId, Modulo, Accion, Detalle, DireccionIP, CreatedAt)
    VALUES (@UsuarioId, @Modulo, @Accion, @Detalle, @DireccionIP, SYSUTCDATETIME());
END
GO

-- ============================================================================
-- 6. CONFIGURACIÓN Y PARÁMETROS FISCALES
-- ============================================================================

CREATE OR ALTER PROCEDURE dbo.sp_Portal_ObtenerConfiguracionEmpresa
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (1)
        RfcEmpresaReceptora,
        RazonSocialReceptora,
        RegimenFiscalReceptor,
        CodigoPostalReceptor,
        DiasToleranciaFactura,
        ToleranciaPrecioOC,
        LimiteTamanoArchivoMB,
        VersionCfdiPermitida
    FROM dbo.Configuracion_Empresa_Reglas WITH (NOLOCK)
    WHERE Activo = 1;
END
GO

-- ============================================================================
-- 7. COMPARACIÓN Y VALIDACIÓN CONTRA ORDEN DE COMPRA (PUNTO 2)
-- ============================================================================

CREATE OR ALTER PROCEDURE dbo.sp_Portal_CompararOrdenCompra
    @FolioOrden VARCHAR(50),
    @CodigoProveedor VARCHAR(15),
    @Detalle dbo.typePortal_FacturaDetalle READONLY
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ToleranciaCosto DECIMAL(5,2) = 0.99;
    SELECT TOP (1) @ToleranciaCosto = ToleranciaPrecioOC 
    FROM dbo.Configuracion_Empresa_Reglas 
    WHERE Activo = 1;

    DECLARE @OrdenCompraId BIGINT;
    DECLARE @EstatusCompra TINYINT;
    DECLARE @EsValido BIT = 1;
    DECLARE @MensajeError VARCHAR(500) = '';

    -- 1. Validar existencia y vigencia de la Orden de Compra para este proveedor
    SELECT TOP (1)
        @OrdenCompraId = OrdenCompraId,
        @EstatusCompra = EstatusCompra
    FROM dbo.Cat_Ordenes_Compra WITH (NOLOCK)
    WHERE FolioOrden = @FolioOrden
      AND CodigoProveedor = @CodigoProveedor
      AND Activo = 1;

    IF @OrdenCompraId IS NULL
    BEGIN
        SELECT 
            0 AS EsValido,
            'La Orden de Compra especificada no existe, no pertenece a su razón social o no está activa.' AS MensajeError;

        SELECT 
            CAST('' AS VARCHAR(20)) AS CodigoArticulo,
            CAST(0 AS DECIMAL(12,4)) AS CantidadPedida,
            CAST(0 AS DECIMAL(12,4)) AS CantidadFacturada,
            CAST(0 AS DECIMAL(12,4)) AS CantidadRecibidaPrevia,
            CAST(0 AS DECIMAL(18,4)) AS CostoOrden,
            CAST(0 AS DECIMAL(18,4)) AS CostoFactura,
            CAST(0 AS DECIMAL(18,4)) AS DiferenciaCosto,
            CAST('ORDEN_NO_VALIDA' AS VARCHAR(50)) AS ErrorTipo
        WHERE 1 = 0;
        RETURN;
    END

    -- 2. Tabla temporal para consolidar partidas de la factura enviada
    CREATE TABLE #FacturaAgrupada (
        CodigoArticulo VARCHAR(20) NOT NULL,
        CantidadFacturada DECIMAL(12,4) NOT NULL,
        PrecioUnitario DECIMAL(18,4) NOT NULL,
        PRIMARY KEY CLUSTERED (CodigoArticulo, PrecioUnitario)
    );

    INSERT INTO #FacturaAgrupada (CodigoArticulo, CantidadFacturada, PrecioUnitario)
    SELECT 
        CodigoArticulo,
        SUM(Cantidad),
        PrecioUnitarioConDescuento
    FROM @Detalle
    GROUP BY CodigoArticulo, PrecioUnitarioConDescuento;

    -- 3. Tabla temporal para consolidar las partidas de la Orden de Compra
    CREATE TABLE #OrdenAgrupada (
        CodigoArticulo VARCHAR(20) NOT NULL,
        CantidadPedida DECIMAL(12,4) NOT NULL,
        CantidadRecibidaPrevia DECIMAL(12,4) NOT NULL,
        CostoReposicion DECIMAL(18,4) NOT NULL,
        PRIMARY KEY CLUSTERED (CodigoArticulo, CostoReposicion)
    );

    INSERT INTO #OrdenAgrupada (CodigoArticulo, CantidadPedida, CantidadRecibidaPrevia, CostoReposicion)
    SELECT 
        CodigoArticulo,
        SUM(CantidadPedida),
        SUM(CantidadRecibidaPrevia),
        CostoReposicion
    FROM dbo.Cat_Ordenes_Compra_Detalle WITH (NOLOCK)
    WHERE OrdenCompraId = @OrdenCompraId
    GROUP BY CodigoArticulo, CostoReposicion;

    -- 4. Detectar discrepancias
    CREATE TABLE #Discrepancias (
        CodigoArticulo VARCHAR(20) NOT NULL,
        CantidadPedida DECIMAL(12,4) NOT NULL,
        CantidadFacturada DECIMAL(12,4) NOT NULL,
        CantidadRecibidaPrevia DECIMAL(12,4) NOT NULL,
        CostoOrden DECIMAL(18,4) NOT NULL,
        CostoFactura DECIMAL(18,4) NOT NULL,
        DiferenciaCosto DECIMAL(18,4) NOT NULL,
        ErrorTipo VARCHAR(50) NOT NULL
    );

    -- 4.1 Artículos que NO existen en la Orden de Compra
    INSERT INTO #Discrepancias (
        CodigoArticulo, CantidadPedida, CantidadFacturada, CantidadRecibidaPrevia,
        CostoOrden, CostoFactura, DiferenciaCosto, ErrorTipo
    )
    SELECT 
        F.CodigoArticulo,
        0 AS CantidadPedida,
        F.CantidadFacturada,
        0 AS CantidadRecibidaPrevia,
        0 AS CostoOrden,
        F.PrecioUnitario AS CostoFactura,
        0 AS DiferenciaCosto,
        'ARTICULO_NO_EN_OC' AS ErrorTipo
    FROM #FacturaAgrupada F
    WHERE NOT EXISTS (
        SELECT 1 
        FROM #OrdenAgrupada O 
        WHERE O.CodigoArticulo = F.CodigoArticulo
    );

    -- 4.2 Artículos con costo unitario que supera la tolerancia permitida (+/- $0.99)
    INSERT INTO #Discrepancias (
        CodigoArticulo, CantidadPedida, CantidadFacturada, CantidadRecibidaPrevia,
        CostoOrden, CostoFactura, DiferenciaCosto, ErrorTipo
    )
    SELECT 
        F.CodigoArticulo,
        O.CantidadPedida,
        F.CantidadFacturada,
        O.CantidadRecibidaPrevia,
        O.CostoReposicion AS CostoOrden,
        F.PrecioUnitario AS CostoFactura,
        (F.PrecioUnitario - O.CostoReposicion) AS DiferenciaCosto,
        'DIFERENCIA_COSTO_EXCEDIDA' AS ErrorTipo
    FROM #FacturaAgrupada F
    INNER JOIN #OrdenAgrupada O ON F.CodigoArticulo = O.CodigoArticulo
    WHERE ABS(F.PrecioUnitario - O.CostoReposicion) > @ToleranciaCosto;

    -- 4.3 Cantidad facturada que excede el saldo restante de la OC
    INSERT INTO #Discrepancias (
        CodigoArticulo, CantidadPedida, CantidadFacturada, CantidadRecibidaPrevia,
        CostoOrden, CostoFactura, DiferenciaCosto, ErrorTipo
    )
    SELECT 
        F.CodigoArticulo,
        O.CantidadPedida,
        F.CantidadFacturada,
        O.CantidadRecibidaPrevia,
        O.CostoReposicion AS CostoOrden,
        F.PrecioUnitario AS CostoFactura,
        0 AS DiferenciaCosto,
        'CANTIDAD_EXCEDIDA' AS ErrorTipo
    FROM #FacturaAgrupada F
    INNER JOIN #OrdenAgrupada O ON F.CodigoArticulo = O.CodigoArticulo
    WHERE (F.CantidadFacturada + O.CantidadRecibidaPrevia) > O.CantidadPedida;

    -- 5. Determinar resultado global
    IF EXISTS (SELECT 1 FROM #Discrepancias)
    BEGIN
        SET @EsValido = 0;
        SET @MensajeError = 'La factura presenta discrepancias de artículos, precios o cantidades respecto a la Orden de Compra.';
    END
    ELSE
    BEGIN
        SET @EsValido = 1;
        SET @MensajeError = 'Validación de Orden de Compra exitosa. Todas las partidas coinciden.';
    END

    -- Retorno 1: Resumen de Validación
    SELECT @EsValido AS EsValido, @MensajeError AS MensajeError;

    -- Retorno 2: Detalle de Discrepancias (si las hay)
    SELECT 
        CodigoArticulo,
        CantidadPedida,
        CantidadFacturada,
        CantidadRecibidaPrevia,
        CostoOrden,
        CostoFactura,
        DiferenciaCosto,
        ErrorTipo
    FROM #Discrepancias
    ORDER BY CodigoArticulo, ErrorTipo;

    DROP TABLE #FacturaAgrupada;
    DROP TABLE #OrdenAgrupada;
    DROP TABLE #Discrepancias;
END
GO

-- ============================================================================
-- 8. PROCEDIMIENTOS DE ADMINISTRACIÓN INTERNA (PUNTO 3)
-- ============================================================================

CREATE OR ALTER PROCEDURE dbo.sp_Portal_Admin_Login
    @Identificador VARCHAR(120) -- Email o CodigoEmpleado
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (1)
        AdminId,
        CodigoEmpleado,
        NombreCompleto,
        Email,
        PasswordHash,
        Rol,
        Activo
    FROM dbo.Usuarios_Administradores WITH (NOLOCK)
    WHERE (Email = @Identificador OR CodigoEmpleado = @Identificador)
      AND Activo = 1;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Portal_Admin_CrearUsuarioProveedor
    @AdminUsuarioId INT,
    @CodigoProveedor VARCHAR(15),
    @RFC VARCHAR(15),
    @Email VARCHAR(120),
    @PasswordHash VARCHAR(255),
    @DireccionIP VARCHAR(45),
    @NuevoUsuarioId INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ProveedorId INT;

    SELECT TOP (1) @ProveedorId = ProveedorId
    FROM dbo.Cat_Proveedores WITH (NOLOCK)
    WHERE CodigoProveedor = @CodigoProveedor;

    IF @ProveedorId IS NULL
    BEGIN
        RAISERROR('El código de proveedor no existe en el catálogo activo.', 16, 1);
        RETURN;
    END

    IF EXISTS (SELECT 1 FROM dbo.Usuarios_Proveedor WHERE Email = @Email)
    BEGIN
        RAISERROR('El correo electrónico ya está registrado para otro usuario.', 16, 1);
        RETURN;
    END

    INSERT INTO dbo.Usuarios_Proveedor (
        ProveedorId, RFC, Email, PasswordHash, IntentosFallidos,
        BloqueadoHasta, Activo, CreatedAt, UpdatedAt
    )
    VALUES (
        @ProveedorId, @RFC, @Email, @PasswordHash, 0,
        NULL, 1, SYSUTCDATETIME(), SYSUTCDATETIME()
    );

    SET @NuevoUsuarioId = SCOPE_IDENTITY();

    -- Registrar auditoría
    INSERT INTO dbo.Auditoria_Eventos (UsuarioId, Modulo, Accion, Detalle, DireccionIP, CreatedAt)
    VALUES (
        @AdminUsuarioId, 'ADMIN_PROVEEDORES', 'CREAR_USUARIO',
        CONCAT('Usuario creado para proveedor: ', @CodigoProveedor, ' (Email: ', @Email, ')'),
        @DireccionIP, SYSUTCDATETIME()
    );
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Portal_Admin_ResetearPasswordProveedor
    @AdminUsuarioId INT,
    @UsuarioId INT,
    @NuevoPasswordHash VARCHAR(255),
    @DireccionIP VARCHAR(45)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.Usuarios_Proveedor
    SET PasswordHash = @NuevoPasswordHash,
        IntentosFallidos = 0,
        BloqueadoHasta = NULL,
        UpdatedAt = SYSUTCDATETIME()
    WHERE UsuarioId = @UsuarioId;

    -- Registrar auditoría
    INSERT INTO dbo.Auditoria_Eventos (UsuarioId, Modulo, Accion, Detalle, DireccionIP, CreatedAt)
    VALUES (
        @AdminUsuarioId, 'ADMIN_PROVEEDORES', 'RESETEAR_PASSWORD',
        CONCAT('Contraseña restablecida por administrador para el usuario ID: ', @UsuarioId),
        @DireccionIP, SYSUTCDATETIME()
    );
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Portal_Admin_CambiarEstatusUsuario
    @AdminUsuarioId INT,
    @UsuarioId INT,
    @Activo BIT,
    @DireccionIP VARCHAR(45)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.Usuarios_Proveedor
    SET Activo = @Activo,
        UpdatedAt = SYSUTCDATETIME()
    WHERE UsuarioId = @UsuarioId;

    INSERT INTO dbo.Auditoria_Eventos (UsuarioId, Modulo, Accion, Detalle, DireccionIP, CreatedAt)
    VALUES (
        @AdminUsuarioId, 'ADMIN_PROVEEDORES', 'CAMBIO_ESTATUS',
        CONCAT('Estatus cambiado a ', CASE WHEN @Activo = 1 THEN 'ACTIVO' ELSE 'INACTIVO' END, ' para usuario ID: ', @UsuarioId),
        @DireccionIP, SYSUTCDATETIME()
    );
END
GO
