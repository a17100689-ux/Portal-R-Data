-- ============================================================================
-- SCRIPT DE INSERCIÓN DE USUARIOS DE PRUEBA (Portal R-Data)
-- Criptografía: PBKDF2 con SHA-512 y 100,000 iteraciones (CryptoService)
-- ============================================================================

USE PortalProveedoresDB;
GO

-- 1. Asegurar la existencia del Proveedor de prueba
IF NOT EXISTS (SELECT 1 FROM Proveedores WHERE RFC = 'AAA010101AAA')
BEGIN
    INSERT INTO Proveedores (RFC, RazonSocial, EmailContacto, Telefono, Activo, FechaRegistro)
    VALUES ('AAA010101AAA', 'PROVEEDOR DEMO SA DE CV', 'contacto@proveedordemo.com', '555-123-4567', 1, SYSUTCDATETIME());
END
GO

DECLARE @ProveedorId INT = (SELECT TOP 1 Id FROM Proveedores WHERE RFC = 'AAA010101AAA');

-- 2. Crear / Actualizar Usuario Administrador
-- Contraseña en texto plano: Admin@Portal2026!
IF NOT EXISTS (SELECT 1 FROM Usuarios WHERE Username = 'admin')
BEGIN
    INSERT INTO Usuarios (
        Username,
        Email,
        PasswordHash,
        Salt,
        Rol,
        ProveedorId,
        Activo,
        IntentosFallidosLogin,
        BloqueadoHasta,
        FechaCreacion
    )
    VALUES (
        'admin',
        'admin@portalrdata.com',
        'dEfo+mrvsYJ+iTWRWckSlM7cFRdPlL5ujA87eEAKqPQ+wf4f9m6zLwjE3fDMHowM/0QBXUPmcQeK82wX777lqw==',
        'talOyam2904R6OPvrK1yJQXz22g9ms+VgWV4UsTjTmA=',
        3, -- Rol 3: Administrador
        NULL,
        1,
        0,
        NULL,
        SYSUTCDATETIME()
    );
    PRINT 'Usuario admin creado exitosamente.';
END
ELSE
BEGIN
    UPDATE Usuarios
    SET PasswordHash = 'dEfo+mrvsYJ+iTWRWckSlM7cFRdPlL5ujA87eEAKqPQ+wf4f9m6zLwjE3fDMHowM/0QBXUPmcQeK82wX777lqw==',
        Salt = 'talOyam2904R6OPvrK1yJQXz22g9ms+VgWV4UsTjTmA=',
        Rol = 3,
        Activo = 1,
        IntentosFallidosLogin = 0,
        BloqueadoHasta = NULL
    WHERE Username = 'admin';
    PRINT 'Usuario admin actualizado exitosamente.';
END
GO

-- 3. Crear / Actualizar Usuario Proveedor de Prueba
-- Contraseña en texto plano: Proveedor@Portal2026!
DECLARE @ProveedorIdDemo INT = (SELECT TOP 1 Id FROM Proveedores WHERE RFC = 'AAA010101AAA');

IF NOT EXISTS (SELECT 1 FROM Usuarios WHERE Username = 'proveedor_demo')
BEGIN
    INSERT INTO Usuarios (
        Username,
        Email,
        PasswordHash,
        Salt,
        Rol,
        ProveedorId,
        Activo,
        IntentosFallidosLogin,
        BloqueadoHasta,
        FechaCreacion
    )
    VALUES (
        'proveedor_demo',
        'proveedor@demo.com',
        'g9axiBZ38bSOGhOCT8/u5jXHaQKg+tAYoHK+jXB6oduJ43MVfr7mGuOvi+idlB+I1tVQRB+Iy5O/8sNJoLKj3Q==',
        'IduVkGEGb5B9f1Ndxmur30ZQQx1kQ1ZGCPQNF8hdBdw=',
        1, -- Rol 1: Proveedor
        @ProveedorIdDemo,
        1,
        0,
        NULL,
        SYSUTCDATETIME()
    );
    PRINT 'Usuario proveedor_demo creado exitosamente.';
END
ELSE
BEGIN
    UPDATE Usuarios
    SET PasswordHash = 'g9axiBZ38bSOGhOCT8/u5jXHaQKg+tAYoHK+jXB6oduJ43MVfr7mGuOvi+idlB+I1tVQRB+Iy5O/8sNJoLKj3Q==',
        Salt = 'IduVkGEGb5B9f1Ndxmur30ZQQx1kQ1ZGCPQNF8hdBdw=',
        Rol = 1,
        ProveedorId = @ProveedorIdDemo,
        Activo = 1,
        IntentosFallidosLogin = 0,
        BloqueadoHasta = NULL
    WHERE Username = 'proveedor_demo';
    PRINT 'Usuario proveedor_demo actualizado exitosamente.';
END
GO
