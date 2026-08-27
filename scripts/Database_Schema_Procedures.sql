-- ============================================================================
-- BASE DE DATOS: PortalProveedoresDB
-- ESQUEMA Y PROCEDIMIENTOS ALMACENADOS CON PRINCIPIO DE MENOR PRIVILEGIO
-- ============================================================================

-- 1. TABLAS
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Proveedores')
BEGIN
    CREATE TABLE Proveedores (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        RFC VARCHAR(15) NOT NULL UNIQUE,
        RazonSocial NVARCHAR(250) NOT NULL,
        EmailContacto NVARCHAR(150) NOT NULL,
        Telefono VARCHAR(20) NULL,
        Activo BIT NOT NULL DEFAULT 1,
        FechaRegistro DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Usuarios')
BEGIN
    CREATE TABLE Usuarios (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Username NVARCHAR(100) NOT NULL UNIQUE,
        Email NVARCHAR(150) NOT NULL,
        PasswordHash NVARCHAR(256) NOT NULL,
        Salt NVARCHAR(128) NOT NULL,
        Rol INT NOT NULL DEFAULT 1, -- 1: Proveedor, 2: Revisor, 3: Admin
        ProveedorId INT NULL FOREIGN KEY REFERENCES Proveedores(Id),
        Activo BIT NOT NULL DEFAULT 1,
        IntentosFallidosLogin INT NOT NULL DEFAULT 0,
        BloqueadoHasta DATETIME2 NULL,
        FechaCreacion DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Facturas')
BEGIN
    CREATE TABLE Facturas (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        ProveedorId INT NOT NULL FOREIGN KEY REFERENCES Proveedores(Id),
        UUID VARCHAR(50) NOT NULL UNIQUE,
        Serie VARCHAR(20) NULL,
        Folio VARCHAR(50) NULL,
        RFCEmisor VARCHAR(15) NOT NULL,
        RFCReceptor VARCHAR(15) NOT NULL,
        FechaEmision DATETIME2 NOT NULL,
        FechaCarga DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        Subtotal DECIMAL(18,4) NOT NULL,
        ImpuestosTrasladados DECIMAL(18,4) NOT NULL DEFAULT 0,
        ImpuestosRetenidos DECIMAL(18,4) NOT NULL DEFAULT 0,
        Total DECIMAL(18,4) NOT NULL,
        Moneda VARCHAR(10) NOT NULL DEFAULT 'MXN',
        EstatusId INT NOT NULL DEFAULT 1, -- 1: Pendiente, 2: Validada, 3: Rechazada, etc.
        ArchivoXmlNombreInterno NVARCHAR(250) NOT NULL,
        ArchivoXmlNombreOriginal NVARCHAR(250) NOT NULL,
        ArchivoPdfNombreInterno NVARCHAR(250) NOT NULL,
        ArchivoPdfNombreOriginal NVARCHAR(250) NOT NULL,
        MotivoRechazo NVARCHAR(500) NULL,
        Observaciones NVARCHAR(500) NULL
    );
    CREATE INDEX IX_Facturas_ProveedorId ON Facturas(ProveedorId);
    CREATE INDEX IX_Facturas_FechaCarga ON Facturas(FechaCarga);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Auditoria')
BEGIN
    CREATE TABLE Auditoria (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        UsuarioId INT NULL FOREIGN KEY REFERENCES Usuarios(Id),
        Accion NVARCHAR(50) NOT NULL,
        Detalle NVARCHAR(500) NOT NULL,
        DireccionIP VARCHAR(50) NOT NULL,
        FechaRegistro DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
    CREATE INDEX IX_Auditoria_Usuario_Fecha ON Auditoria(UsuarioId, FechaRegistro);
END
GO

-- ============================================================================
-- 2. PROCEDIMIENTOS ALMACENADOS (Stored Procedures)
-- ============================================================================

CREATE OR ALTER PROCEDURE sp_Factura_Insertar
    @ProveedorId INT,
    @UUID VARCHAR(50),
    @Serie VARCHAR(20) = NULL,
    @Folio VARCHAR(50) = NULL,
    @RFCEmisor VARCHAR(15),
    @RFCReceptor VARCHAR(15),
    @FechaEmision DATETIME2,
    @Subtotal DECIMAL(18,4),
    @ImpuestosTrasladados DECIMAL(18,4),
    @ImpuestosRetenidos DECIMAL(18,4),
    @Total DECIMAL(18,4),
    @Moneda VARCHAR(10),
    @EstatusId INT,
    @ArchivoXmlNombreInterno NVARCHAR(250),
    @ArchivoXmlNombreOriginal NVARCHAR(250),
    @ArchivoPdfNombreInterno NVARCHAR(250),
    @ArchivoPdfNombreOriginal NVARCHAR(250),
    @Observaciones NVARCHAR(500) = NULL,
    @NuevoId INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO Facturas (
        ProveedorId, UUID, Serie, Folio, RFCEmisor, RFCReceptor,
        FechaEmision, Subtotal, ImpuestosTrasladados, ImpuestosRetenidos,
        Total, Moneda, EstatusId, ArchivoXmlNombreInterno, ArchivoXmlNombreOriginal,
        ArchivoPdfNombreInterno, ArchivoPdfNombreOriginal, Observaciones, FechaCarga
    )
    VALUES (
        @ProveedorId, @UUID, @Serie, @Folio, @RFCEmisor, @RFCReceptor,
        @FechaEmision, @Subtotal, @ImpuestosTrasladados, @ImpuestosRetenidos,
        @Total, @Moneda, @EstatusId, @ArchivoXmlNombreInterno, @ArchivoXmlNombreOriginal,
        @ArchivoPdfNombreInterno, @ArchivoPdfNombreOriginal, @Observaciones, SYSUTCDATETIME()
    );

    SET @NuevoId = SCOPE_IDENTITY();
END
GO

CREATE OR ALTER PROCEDURE sp_Factura_ObtenerPorId
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM Facturas WHERE Id = @Id;
END
GO

CREATE OR ALTER PROCEDURE sp_Factura_ObtenerPorUuid
    @UUID VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM Facturas WHERE UUID = @UUID;
END
GO

CREATE OR ALTER PROCEDURE sp_Factura_ListarPorProveedor
    @ProveedorId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM Facturas 
    WHERE ProveedorId = @ProveedorId 
    ORDER BY FechaCarga DESC;
END
GO

CREATE OR ALTER PROCEDURE sp_Factura_ListarTodas
    @EstatusId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM Facturas 
    WHERE (@EstatusId IS NULL OR EstatusId = @EstatusId)
    ORDER BY FechaCarga DESC;
END
GO

CREATE OR ALTER PROCEDURE sp_Factura_ActualizarEstatus
    @FacturaId INT,
    @NuevoEstatusId INT,
    @MotivoRechazo NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE Facturas
    SET EstatusId = @NuevoEstatusId,
        MotivoRechazo = @MotivoRechazo
    WHERE Id = @FacturaId;
END
GO

CREATE OR ALTER PROCEDURE sp_Factura_ExisteUuid
    @UUID VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT COUNT(1) FROM Facturas WHERE UUID = @UUID;
END
GO

CREATE OR ALTER PROCEDURE sp_Usuario_ObtenerPorUsername
    @Username NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM Usuarios WHERE Username = @Username;
END
GO

CREATE OR ALTER PROCEDURE sp_Usuario_ObtenerPorId
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM Usuarios WHERE Id = @Id;
END
GO

CREATE OR ALTER PROCEDURE sp_Usuario_RegistrarIntentoFallido
    @UsuarioId INT,
    @MaxIntentos INT = 5,
    @MinutosBloqueo INT = 15
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE Usuarios
    SET IntentosFallidosLogin = IntentosFallidosLogin + 1,
        BloqueadoHasta = CASE 
            WHEN (IntentosFallidosLogin + 1) >= @MaxIntentos 
            THEN DATEADD(MINUTE, @MinutosBloqueo, SYSUTCDATETIME())
            ELSE NULL 
        END
    WHERE Id = @UsuarioId;
END
GO

CREATE OR ALTER PROCEDURE sp_Usuario_ResetearIntentosFallidos
    @UsuarioId INT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE Usuarios
    SET IntentosFallidosLogin = 0,
        BloqueadoHasta = NULL
    WHERE Id = @UsuarioId;
END
GO

CREATE OR ALTER PROCEDURE sp_Auditoria_Registrar
    @UsuarioId INT = NULL,
    @Accion NVARCHAR(50),
    @Detalle NVARCHAR(500),
    @DireccionIP VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO Auditoria (UsuarioId, Accion, Detalle, DireccionIP, FechaRegistro)
    VALUES (@UsuarioId, @Accion, @Detalle, @DireccionIP, SYSUTCDATETIME());
END
GO

-- ============================================================================
-- 3. ROL DE PRIVILEGIOS MÍNIMOS PARA LA APLICACIÓN (Least Privilege)
-- ============================================================================
/*
CREATE LOGIN PortalAppUser WITH PASSWORD = 'YourStrongSecretPassword_Here!123';
CREATE USER PortalAppUser FOR LOGIN PortalAppUser;

-- Solo conceder ejecución de los Stored Procedures específicos (sin acceso directo DDL/DML a tablas)
GRANT EXECUTE ON sp_Factura_Insertar TO PortalAppUser;
GRANT EXECUTE ON sp_Factura_ObtenerPorId TO PortalAppUser;
GRANT EXECUTE ON sp_Factura_ObtenerPorUuid TO PortalAppUser;
GRANT EXECUTE ON sp_Factura_ListarPorProveedor TO PortalAppUser;
GRANT EXECUTE ON sp_Factura_ListarTodas TO PortalAppUser;
GRANT EXECUTE ON sp_Factura_ActualizarEstatus TO PortalAppUser;
GRANT EXECUTE ON sp_Factura_ExisteUuid TO PortalAppUser;
GRANT EXECUTE ON sp_Usuario_ObtenerPorUsername TO PortalAppUser;
GRANT EXECUTE ON sp_Usuario_ObtenerPorId TO PortalAppUser;
GRANT EXECUTE ON sp_Usuario_RegistrarIntentoFallido TO PortalAppUser;
GRANT EXECUTE ON sp_Usuario_ResetearIntentosFallidos TO PortalAppUser;
GRANT EXECUTE ON sp_Auditoria_Registrar TO PortalAppUser;
*/
