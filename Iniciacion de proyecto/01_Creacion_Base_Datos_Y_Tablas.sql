-- ============================================================================
-- SCRIPT 01: CREACIÓN DE BASE DE DATOS Y TABLAS OPTIMIZADAS
-- PROYECTO: Portal de Facturación de Proveedores (Portal R-Data)
-- BASE DE DATOS: PortalProveedores_DB
-- ARQUITECTURA: DB_Optimizer_Pro
-- ============================================================================

USE master;
GO

-- 1. CREACIÓN DE LA BASE DE DATOS
-- Usamos SQL_Latin1_General_CP1_CI_AS para concordar exactamente con la base central del ERP (Punto_de_Venta)
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'PortalProveedores_DB')
BEGIN
    CREATE DATABASE [PortalProveedores_DB]
    COLLATE SQL_Latin1_General_CP1_CI_AS;
END
GO

USE [PortalProveedores_DB];
GO

-- Optimización de concurrencia: Habilitar READ COMMITTED SNAPSHOT para eliminar bloqueos entre lectores y escritores
IF (SELECT is_read_committed_snapshot_on FROM sys.databases WHERE name = N'PortalProveedores_DB') = 0
BEGIN
    ALTER DATABASE [PortalProveedores_DB] SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;
END
GO

-- ============================================================================
-- 2. TIPOS DEFINIDOS POR EL USUARIO (TABLE-VALUED PARAMETERS - TVPs)
-- ============================================================================

-- Parámetro estructurado para inserción atómica del detalle de partidas
IF TYPE_ID(N'dbo.typePortal_FacturaDetalle') IS NULL
BEGIN
    CREATE TYPE dbo.typePortal_FacturaDetalle AS TABLE (
        Renglon SMALLINT NOT NULL,
        CodigoArticulo VARCHAR(20) NOT NULL,
        ClaveProdServSat VARCHAR(15) NOT NULL,
        Descripcion VARCHAR(255) NOT NULL,
        Unidad VARCHAR(50) NOT NULL,
        Cantidad DECIMAL(12,4) NOT NULL,
        PrecioUnitarioSinDescuento DECIMAL(18,4) NOT NULL,
        Descuento VARCHAR(15) NOT NULL,
        PrecioUnitarioConDescuento DECIMAL(18,4) NOT NULL,
        Importe DECIMAL(18,2) NOT NULL,
        PorcentajeRetencion DECIMAL(9,4) NOT NULL,
        MontoRetencion DECIMAL(18,2) NOT NULL,
        CantidadReal DECIMAL(12,4) NOT NULL
    );
END
GO

-- Parámetro estructurado para metadatos de archivos subidos (XML, PDF)
IF TYPE_ID(N'dbo.typePortal_FacturaArchivo') IS NULL
BEGIN
    CREATE TYPE dbo.typePortal_FacturaArchivo AS TABLE (
        TipoArchivo VARCHAR(4) NOT NULL, -- 'XML' o 'PDF'
        NombreOriginal VARCHAR(200) NOT NULL,
        NombreAlmacenamiento VARCHAR(100) NOT NULL, -- GUID generado
        RutaFisicaSegura VARCHAR(300) NOT NULL,     -- Ruta fuera de wwwroot
        HashSha256 CHAR(64) NOT NULL,              -- Hash de integridad
        TamanoBytes INT NOT NULL,
        XmlContenido VARCHAR(MAX) NULL              -- Texto plano del XML validado
    );
END
GO

-- Parámetro estructurado para localizaciones de bodega (si aplica)
IF TYPE_ID(N'dbo.typePortal_FacturaLocalizacion') IS NULL
BEGIN
    CREATE TYPE dbo.typePortal_FacturaLocalizacion AS TABLE (
        CodigoArticulo VARCHAR(20) NOT NULL,
        Cantidad DECIMAL(12,4) NOT NULL,
        Localizacion VARCHAR(20) NOT NULL,
        DOT VARCHAR(4) NULL
    );
END
GO

-- ============================================================================
-- 3. TABLAS DE CONFIGURACIÓN Y CATÁLOGOS LOCALES (RÉPLICA / CACHE DESDE CENTRAL)
-- ============================================================================

-- Configuración y parámetros fiscales / de negocio de la empresa receptora (Radial Llantas)
IF OBJECT_ID(N'dbo.Configuracion_Empresa_Reglas', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Configuracion_Empresa_Reglas (
        ConfiguracionId INT IDENTITY(1,1) NOT NULL,
        RfcEmpresaReceptora VARCHAR(15) NOT NULL, -- 'RLA8103252S2'
        RazonSocialReceptora VARCHAR(200) NOT NULL,
        RegimenFiscalReceptor VARCHAR(5) NOT NULL,  -- '601'
        CodigoPostalReceptor VARCHAR(10) NOT NULL, -- '44920'
        DiasToleranciaFactura INT NOT NULL CONSTRAINT DF_Config_DiasTolerancia DEFAULT(3),
        ToleranciaPrecioOC DECIMAL(5,2) NOT NULL CONSTRAINT DF_Config_ToleranciaPrecio DEFAULT(0.99),
        LimiteTamanoArchivoMB INT NOT NULL CONSTRAINT DF_Config_LimiteTamano DEFAULT(5),
        VersionCfdiPermitida VARCHAR(5) NOT NULL CONSTRAINT DF_Config_VersionCfdi DEFAULT('4.0'),
        Activo BIT NOT NULL CONSTRAINT DF_Config_Activo DEFAULT(1),
        UpdatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_Config_UpdatedAt DEFAULT(SYSUTCDATETIME()),
        CONSTRAINT PK_Configuracion_Empresa PRIMARY KEY CLUSTERED (ConfiguracionId)
    );

    INSERT INTO dbo.Configuracion_Empresa_Reglas (
        RfcEmpresaReceptora, RazonSocialReceptora, RegimenFiscalReceptor, 
        CodigoPostalReceptor, DiasToleranciaFactura, ToleranciaPrecioOC, LimiteTamanoArchivoMB, VersionCfdiPermitida
    )
    VALUES (
        'RLA8103252S2', 'RADIAL LLANTAS', '601', '44920', 3, 0.99, 5, '4.0'
    );
END
GO

-- Catálogo de Proveedores habilitados para el portal
IF OBJECT_ID(N'dbo.Cat_Proveedores', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Cat_Proveedores (
        ProveedorId INT IDENTITY(1,1) NOT NULL,
        CodigoProveedor VARCHAR(15) NOT NULL,
        RFC VARCHAR(15) NOT NULL,
        RazonSocial VARCHAR(200) NOT NULL,
        CondicionesPago VARCHAR(50) NULL,
        RequiereValidarCompra BIT NOT NULL CONSTRAINT DF_Proveedores_RequiereValidar DEFAULT(1),
        OrdenCompraObligatoria BIT NOT NULL CONSTRAINT DF_Proveedores_OCObligatoria DEFAULT(1),
        EsProveedorNacional BIT NOT NULL CONSTRAINT DF_Proveedores_EsNacional DEFAULT(1),
        Activo BIT NOT NULL CONSTRAINT DF_Proveedores_Activo DEFAULT(1),
        CreatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_Proveedores_CreatedAt DEFAULT(SYSUTCDATETIME()),
        UpdatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_Proveedores_UpdatedAt DEFAULT(SYSUTCDATETIME()),
        CONSTRAINT PK_Cat_Proveedores PRIMARY KEY CLUSTERED (ProveedorId),
        CONSTRAINT UQ_Cat_Proveedores_Codigo UNIQUE NONCLUSTERED (CodigoProveedor)
    );

    CREATE NONCLUSTERED INDEX IX_Cat_Proveedores_RFC 
    ON dbo.Cat_Proveedores (RFC) 
    INCLUDE (CodigoProveedor, RazonSocial, Activo);
END
GO

-- Catálogo de Sucursales válidas para compras
IF OBJECT_ID(N'dbo.Cat_Sucursales', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Cat_Sucursales (
        SucursalId INT IDENTITY(1,1) NOT NULL,
        NumeroCortoSucursal TINYINT NOT NULL,
        CodigoSucursal VARCHAR(10) NOT NULL,
        Descripcion VARCHAR(100) NOT NULL,
        Region VARCHAR(20) NOT NULL,
        AplicaLocalizador BIT NOT NULL CONSTRAINT DF_Sucursales_Localizador DEFAULT(0),
        Activo BIT NOT NULL CONSTRAINT DF_Sucursales_Activo DEFAULT(1),
        CreatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_Sucursales_CreatedAt DEFAULT(SYSUTCDATETIME()),
        CONSTRAINT PK_Cat_Sucursales PRIMARY KEY CLUSTERED (SucursalId),
        CONSTRAINT UQ_Cat_Sucursales_NumeroCorto UNIQUE NONCLUSTERED (NumeroCortoSucursal)
    );
END
GO

-- Catálogo y reglas de Artículos validados para compras
IF OBJECT_ID(N'dbo.Cat_Articulos_Reglas', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Cat_Articulos_Reglas (
        ArticuloId INT IDENTITY(1,1) NOT NULL,
        CodigoArticulo VARCHAR(20) NOT NULL,
        Descripcion VARCHAR(255) NOT NULL,
        CodigoLinea VARCHAR(10) NOT NULL,
        IdProductosyServicios VARCHAR(15) NOT NULL, -- Clave SAT
        FactorConversion DECIMAL(12,4) NOT NULL CONSTRAINT DF_Articulos_Factor DEFAULT(1.0),
        EsPlomo BIT NOT NULL CONSTRAINT DF_Articulos_EsPlomo DEFAULT(0),
        PermitidoCFDI_G03 BIT NOT NULL CONSTRAINT DF_Articulos_G03 DEFAULT(0),
        AplicaLocalizador BIT NOT NULL CONSTRAINT DF_Articulos_Localizador DEFAULT(0),
        Activo BIT NOT NULL CONSTRAINT DF_Articulos_Activo DEFAULT(1),
        CreatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_Articulos_CreatedAt DEFAULT(SYSUTCDATETIME()),
        CONSTRAINT PK_Cat_Articulos_Reglas PRIMARY KEY CLUSTERED (ArticuloId),
        CONSTRAINT UQ_Cat_Articulos_Codigo UNIQUE NONCLUSTERED (CodigoArticulo)
    );

    CREATE NONCLUSTERED INDEX IX_Cat_Articulos_G03 
    ON dbo.Cat_Articulos_Reglas (CodigoArticulo) 
    INCLUDE (PermitidoCFDI_G03, IdProductosyServicios);
END
GO

-- Órdenes de Compra abiertas asignadas al proveedor
IF OBJECT_ID(N'dbo.Cat_Ordenes_Compra', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Cat_Ordenes_Compra (
        OrdenCompraId BIGINT IDENTITY(1,1) NOT NULL,
        FolioOrden VARCHAR(50) NOT NULL,
        NumeroDocumentoCentral DECIMAL(9,0) NOT NULL,
        CodigoProveedor VARCHAR(15) NOT NULL,
        NumeroCortoSucursal TINYINT NOT NULL,
        EstatusCompra TINYINT NOT NULL, -- Estatus 3, 4, 5, 6, 9
        CondicionesPago TINYINT NULL,
        Observaciones VARCHAR(300) NULL,
        FechaCreacion DATETIME2(0) NOT NULL,
        Activo BIT NOT NULL CONSTRAINT DF_OC_Activo DEFAULT(1),
        CreatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_OC_CreatedAt DEFAULT(SYSUTCDATETIME()),
        CONSTRAINT PK_Cat_Ordenes_Compra PRIMARY KEY CLUSTERED (OrdenCompraId),
        CONSTRAINT UQ_Cat_Ordenes_Compra_Folio UNIQUE NONCLUSTERED (FolioOrden)
    );

    CREATE NONCLUSTERED INDEX IX_Cat_Ordenes_Compra_Proveedor 
    ON dbo.Cat_Ordenes_Compra (CodigoProveedor, Activo) 
    INCLUDE (FolioOrden, NumeroCortoSucursal, EstatusCompra);
END
GO

-- Partidas de las Órdenes de Compra
IF OBJECT_ID(N'dbo.Cat_Ordenes_Compra_Detalle', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Cat_Ordenes_Compra_Detalle (
        OrdenCompraDetalleId BIGINT IDENTITY(1,1) NOT NULL,
        OrdenCompraId BIGINT NOT NULL,
        CodigoArticulo VARCHAR(20) NOT NULL,
        CantidadPedida DECIMAL(12,4) NOT NULL,
        CantidadRecibidaPrevia DECIMAL(12,4) NOT NULL CONSTRAINT DF_OCDetalle_CantPrevia DEFAULT(0),
        CostoReposicion DECIMAL(18,4) NOT NULL,
        CreatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_OCDetalle_CreatedAt DEFAULT(SYSUTCDATETIME()),
        CONSTRAINT PK_Cat_Ordenes_Compra_Detalle PRIMARY KEY CLUSTERED (OrdenCompraDetalleId),
        CONSTRAINT FK_OCDetalle_OrdenCompra FOREIGN KEY (OrdenCompraId) REFERENCES dbo.Cat_Ordenes_Compra(OrdenCompraId) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX IX_Cat_OCDetalle_Articulo 
    ON dbo.Cat_Ordenes_Compra_Detalle (OrdenCompraId, CodigoArticulo) 
    INCLUDE (CantidadPedida, CantidadRecibidaPrevia, CostoReposicion);
END
GO

-- ============================================================================
-- 4. TABLAS DE SEGURIDAD Y ACCESOS DEL PORTAL
-- ============================================================================

-- Usuarios Internos Administradores (Alta y Gestión de Proveedores)
IF OBJECT_ID(N'dbo.Usuarios_Administradores', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Usuarios_Administradores (
        AdminId INT IDENTITY(1,1) NOT NULL,
        CodigoEmpleado VARCHAR(15) NOT NULL,
        NombreCompleto VARCHAR(150) NOT NULL,
        Email VARCHAR(120) NOT NULL,
        PasswordHash VARCHAR(255) NOT NULL,
        Rol VARCHAR(30) NOT NULL CONSTRAINT DF_Admin_Rol DEFAULT('ADMIN'), -- 'ADMIN', 'COMPRAS', 'MESA_CONTROL'
        Activo BIT NOT NULL CONSTRAINT DF_Admin_Activo DEFAULT(1),
        UltimoAcceso DATETIME2(3) NULL,
        CreatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_Admin_CreatedAt DEFAULT(SYSUTCDATETIME()),
        UpdatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_Admin_UpdatedAt DEFAULT(SYSUTCDATETIME()),
        CONSTRAINT PK_Usuarios_Administradores PRIMARY KEY CLUSTERED (AdminId),
        CONSTRAINT UQ_Admin_CodigoEmpleado UNIQUE NONCLUSTERED (CodigoEmpleado),
        CONSTRAINT UQ_Admin_Email UNIQUE NONCLUSTERED (Email)
    );
END
GO

-- Usuarios y Credenciales de Proveedores
IF OBJECT_ID(N'dbo.Usuarios_Proveedor', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Usuarios_Proveedor (
        UsuarioId INT IDENTITY(1,1) NOT NULL,
        ProveedorId INT NOT NULL,
        RFC VARCHAR(15) NOT NULL,
        Email VARCHAR(120) NOT NULL,
        PasswordHash VARCHAR(255) NOT NULL, -- Hash seguro con BCrypt o Argon2
        IntentosFallidos TINYINT NOT NULL CONSTRAINT DF_Usuarios_Intentos DEFAULT(0),
        BloqueadoHasta DATETIME2(3) NULL,
        Activo BIT NOT NULL CONSTRAINT DF_Usuarios_Activo DEFAULT(1),
        UltimoAcceso DATETIME2(3) NULL,
        CreatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_Usuarios_CreatedAt DEFAULT(SYSUTCDATETIME()),
        UpdatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_Usuarios_UpdatedAt DEFAULT(SYSUTCDATETIME()),
        CONSTRAINT PK_Usuarios_Proveedor PRIMARY KEY CLUSTERED (UsuarioId),
        CONSTRAINT UQ_Usuarios_Proveedor_Email UNIQUE NONCLUSTERED (Email),
        CONSTRAINT FK_Usuarios_Proveedor FOREIGN KEY (ProveedorId) REFERENCES dbo.Cat_Proveedores(ProveedorId)
    );

    CREATE NONCLUSTERED INDEX IX_Usuarios_Proveedor_RFC 
    ON dbo.Usuarios_Proveedor (RFC, Activo);
END
GO

-- Bitácora de accesos y seguridad
IF OBJECT_ID(N'dbo.Bitacora_Accesos', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Bitacora_Accesos (
        AccesoId BIGINT IDENTITY(1,1) NOT NULL,
        UsuarioId INT NULL,
        RFC VARCHAR(15) NOT NULL,
        DireccionIP VARCHAR(45) NOT NULL,
        Exitoso BIT NOT NULL,
        Detalle VARCHAR(255) NULL,
        FechaAcceso DATETIME2(3) NOT NULL CONSTRAINT DF_Bitacora_FechaAcceso DEFAULT(SYSUTCDATETIME()),
        CONSTRAINT PK_Bitacora_Accesos PRIMARY KEY CLUSTERED (AccesoId)
    );

    CREATE NONCLUSTERED INDEX IX_Bitacora_RFC_Fecha 
    ON dbo.Bitacora_Accesos (RFC, FechaAcceso DESC);
END
GO

-- ============================================================================
-- 5. TABLAS TRANSACCIONALES DE FACTURACIÓN (ESCRITURA Y CONSULTA)
-- ============================================================================

-- Cabecera de facturas ingresadas por el proveedor
IF OBJECT_ID(N'dbo.Facturas_Cabecera', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Facturas_Cabecera (
        FacturaId BIGINT IDENTITY(1,1) NOT NULL,
        ProveedorId INT NOT NULL,
        CodigoProveedor VARCHAR(15) NOT NULL,
        UUID VARCHAR(36) NOT NULL,
        Serie VARCHAR(15) NULL,
        FolioFactura VARCHAR(40) NOT NULL,
        FechaFactura DATETIME2(0) NOT NULL,
        FechaRecepcion DATETIME2(3) NOT NULL CONSTRAINT DF_Facturas_FechaRecepcion DEFAULT(SYSUTCDATETIME()),
        Subtotal DECIMAL(18,2) NOT NULL,
        Descuento DECIMAL(18,2) NOT NULL CONSTRAINT DF_Facturas_Descuento DEFAULT(0),
        IvaTrasladado DECIMAL(18,2) NOT NULL,
        IvaRetenido DECIMAL(18,2) NOT NULL CONSTRAINT DF_Facturas_IvaRetenido DEFAULT(0),
        CostoTotal DECIMAL(18,2) NOT NULL,
        Moneda VARCHAR(3) NOT NULL CONSTRAINT DF_Facturas_Moneda DEFAULT('MXN'),
        TipoCambio DECIMAL(12,4) NOT NULL CONSTRAINT DF_Facturas_TipoCambio DEFAULT(1.0000),
        CodigoUsoCFDI VARCHAR(10) NOT NULL,
        CodigoCFDIMetodoPago VARCHAR(10) NOT NULL,
        CodigoCFDIFormaPago VARCHAR(10) NOT NULL,
        RegimenFiscalEmisor VARCHAR(5) NOT NULL,
        RegimenFiscalReceptor VARCHAR(5) NOT NULL,
        NumeroCortoSucursal TINYINT NOT NULL,
        OrdenCompra VARCHAR(50) NULL,
        EsMesaDeControl BIT NOT NULL CONSTRAINT DF_Facturas_MesaControl DEFAULT(0),
        
        -- Estatus de proceso y validación interna
        EstatusValidacion TINYINT NOT NULL CONSTRAINT DF_Facturas_EstatusValidacion DEFAULT(1), 
        -- 1: Recibida, 2: Validada OK, 3: Rechazada por Regla de Negocio
        MotivoRechazo VARCHAR(500) NULL,
        
        -- Sincronización con el ERP Central (Punto_de_Venta)
        EstatusSincronizacion TINYINT NOT NULL CONSTRAINT DF_Facturas_EstatusSync DEFAULT(0),
        -- 0: Pendiente de Cola, 1: En Cola, 2: Sincronizada a Central, 3: Error
        NumeroDocumentoCentral DECIMAL(9,0) NULL,
        FechaSincronizacion DATETIME2(3) NULL,
        MensajeErrorSincronizacion VARCHAR(500) NULL,

        -- Validación de Entrega Física de Mercancía en Sucursal/Almacén
        EstatusMercancia VARCHAR(30) NOT NULL CONSTRAINT DF_Facturas_EstMercancia DEFAULT('PENDIENTE_ENTREGA'),
        -- 'PENDIENTE_ENTREGA', 'ENTREGADA_SUCURSAL', 'ENTREGA_PARCIAL', 'CANCELADA'
        FechaEntregaMercancia DATETIME2(0) NULL,
        
        -- Campos de auditoría
        CreatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_Facturas_CreatedAt DEFAULT(SYSUTCDATETIME()),
        UpdatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_Facturas_UpdatedAt DEFAULT(SYSUTCDATETIME()),
        CreatedByIp VARCHAR(45) NOT NULL CONSTRAINT DF_Facturas_CreatedByIp DEFAULT('127.0.0.1'),
        
        CONSTRAINT PK_Facturas_Cabecera PRIMARY KEY CLUSTERED (FacturaId),
        CONSTRAINT UQ_Facturas_UUID UNIQUE NONCLUSTERED (UUID),
        CONSTRAINT FK_Facturas_Proveedor FOREIGN KEY (ProveedorId) REFERENCES dbo.Cat_Proveedores(ProveedorId)
    );

    -- Índice covering para la bandeja principal del proveedor
    CREATE NONCLUSTERED INDEX IX_Facturas_Proveedor_Fecha 
    ON dbo.Facturas_Cabecera (ProveedorId, FechaFactura DESC)
    INCLUDE (FolioFactura, CostoTotal, EstatusValidacion, EstatusSincronizacion, NumeroDocumentoCentral);

    -- Índice sargable para validación instantánea de duplicados por proveedor y folio
    CREATE NONCLUSTERED INDEX IX_Facturas_Duplicados 
    ON dbo.Facturas_Cabecera (CodigoProveedor, FolioFactura)
    INCLUDE (UUID, FechaFactura, EstatusValidacion);
END
GO

-- Detalle de partidas y conceptos de la factura
IF OBJECT_ID(N'dbo.Facturas_Detalle', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Facturas_Detalle (
        FacturaDetalleId BIGINT IDENTITY(1,1) NOT NULL,
        FacturaId BIGINT NOT NULL,
        Renglon SMALLINT NOT NULL,
        CodigoArticulo VARCHAR(20) NOT NULL,
        ClaveProdServSat VARCHAR(15) NOT NULL,
        Descripcion VARCHAR(255) NOT NULL,
        Unidad VARCHAR(50) NOT NULL,
        Cantidad DECIMAL(12,4) NOT NULL,
        PrecioUnitarioSinDescuento DECIMAL(18,4) NOT NULL,
        Descuento VARCHAR(15) NOT NULL CONSTRAINT DF_FacturasDetalle_Descuento DEFAULT('0'),
        PrecioUnitarioConDescuento DECIMAL(18,4) NOT NULL,
        Importe DECIMAL(18,2) NOT NULL,
        PorcentajeRetencion DECIMAL(9,4) NOT NULL CONSTRAINT DF_FacturasDetalle_PorcRet DEFAULT(0),
        MontoRetencion DECIMAL(18,2) NOT NULL CONSTRAINT DF_FacturasDetalle_MontoRet DEFAULT(0),
        CantidadReal DECIMAL(12,4) NOT NULL CONSTRAINT DF_FacturasDetalle_CantReal DEFAULT(0),
        CreatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_FacturasDetalle_CreatedAt DEFAULT(SYSUTCDATETIME()),
        CONSTRAINT PK_Facturas_Detalle PRIMARY KEY CLUSTERED (FacturaDetalleId),
        CONSTRAINT UQ_Facturas_Renglon UNIQUE NONCLUSTERED (FacturaId, Renglon),
        CONSTRAINT FK_FacturasDetalle_Factura FOREIGN KEY (FacturaId) REFERENCES dbo.Facturas_Cabecera(FacturaId) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX IX_FacturasDetalle_FacturaId 
    ON dbo.Facturas_Detalle (FacturaId) 
    INCLUDE (CodigoArticulo, Cantidad, PrecioUnitarioConDescuento, Importe);
END
GO

-- Metadatos de Archivos XML y PDF almacenados en disco seguro
IF OBJECT_ID(N'dbo.Facturas_Archivos', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Facturas_Archivos (
        ArchivoId BIGINT IDENTITY(1,1) NOT NULL,
        FacturaId BIGINT NOT NULL,
        TipoArchivo VARCHAR(4) NOT NULL, -- 'XML' o 'PDF'
        NombreOriginal VARCHAR(200) NOT NULL,
        NombreAlmacenamiento VARCHAR(100) NOT NULL, -- GUID asignado
        RutaFisicaSegura VARCHAR(300) NOT NULL,     -- Fuera de wwwroot
        HashSha256 CHAR(64) NOT NULL,              -- Hash para verificación de integridad
        TamanoBytes INT NOT NULL,
        XmlContenido VARCHAR(MAX) NULL,             -- Contenido XML cacheado
        CreatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_Archivos_CreatedAt DEFAULT(SYSUTCDATETIME()),
        CONSTRAINT PK_Facturas_Archivos PRIMARY KEY CLUSTERED (ArchivoId),
        CONSTRAINT UQ_FacturasArchivos_Hash UNIQUE NONCLUSTERED (HashSha256),
        CONSTRAINT FK_FacturasArchivos_Factura FOREIGN KEY (FacturaId) REFERENCES dbo.Facturas_Cabecera(FacturaId) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX IX_FacturasArchivos_FacturaId 
    ON dbo.Facturas_Archivos (FacturaId) 
    INCLUDE (TipoArchivo, NombreAlmacenamiento, RutaFisicaSegura);
END
GO

-- Detalle de Localizaciones de Bodega (si la sucursal maneja módulo localizador)
IF OBJECT_ID(N'dbo.Facturas_Localizaciones', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Facturas_Localizaciones (
        LocalizacionId BIGINT IDENTITY(1,1) NOT NULL,
        FacturaId BIGINT NOT NULL,
        CodigoArticulo VARCHAR(20) NOT NULL,
        Cantidad DECIMAL(12,4) NOT NULL,
        Localizacion VARCHAR(20) NOT NULL,
        DOT VARCHAR(4) NULL,
        CreatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_Localizaciones_CreatedAt DEFAULT(SYSUTCDATETIME()),
        CONSTRAINT PK_Facturas_Localizaciones PRIMARY KEY CLUSTERED (LocalizacionId),
        CONSTRAINT FK_FacturasLocalizaciones_Factura FOREIGN KEY (FacturaId) REFERENCES dbo.Facturas_Cabecera(FacturaId) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX IX_FacturasLocalizaciones_FacturaId 
    ON dbo.Facturas_Localizaciones (FacturaId);
END
GO

-- ============================================================================
-- 6. COLA DE SINCRONIZACIÓN Y OUTBOX (SINCRONIZACIÓN ASÍNCRONA A CENTRAL)
-- ============================================================================

IF OBJECT_ID(N'dbo.Sync_Transacciones_Cola', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Sync_Transacciones_Cola (
        SyncId BIGINT IDENTITY(1,1) NOT NULL,
        FacturaId BIGINT NOT NULL,
        TipoOperacion VARCHAR(40) NOT NULL, -- 'COMPRA_NORMAL', 'COMPRA_MESA_CONTROL'
        EstadoSync VARCHAR(20) NOT NULL CONSTRAINT DF_Sync_Estado DEFAULT('PENDIENTE'),
        -- Estados: 'PENDIENTE', 'EN_PROCESO', 'COMPLETADO', 'FALLIDO'
        Intentos TINYINT NOT NULL CONSTRAINT DF_Sync_Intentos DEFAULT(0),
        MaxIntentos TINYINT NOT NULL CONSTRAINT DF_Sync_MaxIntentos DEFAULT(5),
        UltimoError VARCHAR(1000) NULL,
        CreatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_Sync_CreatedAt DEFAULT(SYSUTCDATETIME()),
        ProcesadoAt DATETIME2(3) NULL,
        CONSTRAINT PK_Sync_Transacciones_Cola PRIMARY KEY CLUSTERED (SyncId),
        CONSTRAINT FK_Sync_Factura FOREIGN KEY (FacturaId) REFERENCES dbo.Facturas_Cabecera(FacturaId)
    );

    -- Índice covering para despacho ultra-rápido de la cola
    CREATE NONCLUSTERED INDEX IX_Sync_Cola_Estado 
    ON dbo.Sync_Transacciones_Cola (EstadoSync, Intentos)
    INCLUDE (FacturaId, TipoOperacion, CreatedAt);
END
GO

-- ============================================================================
-- 7. AUDITORÍA GENERAL DE ACTIVIDADES
-- ============================================================================

IF OBJECT_ID(N'dbo.Auditoria_Eventos', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Auditoria_Eventos (
        EventoId BIGINT IDENTITY(1,1) NOT NULL,
        UsuarioId INT NULL,
        Modulo VARCHAR(50) NOT NULL,
        Accion VARCHAR(50) NOT NULL,
        Detalle VARCHAR(500) NOT NULL,
        DireccionIP VARCHAR(45) NOT NULL,
        CreatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_Auditoria_CreatedAt DEFAULT(SYSUTCDATETIME()),
        CONSTRAINT PK_Auditoria_Eventos PRIMARY KEY CLUSTERED (EventoId)
    );

    CREATE NONCLUSTERED INDEX IX_Auditoria_Fecha 
    ON dbo.Auditoria_Eventos (CreatedAt DESC)
    INCLUDE (UsuarioId, Modulo, Accion);
END
GO
