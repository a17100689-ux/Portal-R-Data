-- ============================================================================
-- SCRIPT DE ALTA DE USUARIOS DE PRUEBA EN PortalProveedores_DB
-- BASE DE DATOS OFICIAL: PortalProveedores_DB
-- ============================================================================

USE [PortalProveedores_DB];
GO

-- 1. Asegurar Catálogo de Proveedor Demo
IF NOT EXISTS (SELECT 1 FROM dbo.Cat_Proveedores WHERE RFC = 'AAA010101AAA')
BEGIN
    INSERT INTO dbo.Cat_Proveedores (
        CodigoProveedor, RFC, RazonSocial, CondicionesPago, 
        RequiereValidarCompra, OrdenCompraObligatoria, EsProveedorNacional, Activo, CreatedAt, UpdatedAt
    )
    VALUES (
        'PRV-DEMO01', 'AAA010101AAA', 'PROVEEDOR DEMO SA DE CV', '0,15,30',
        1, 1, 1, 1, SYSUTCDATETIME(), SYSUTCDATETIME()
    );
    PRINT 'Proveedor DEMO creado en Cat_Proveedores.';
END
GO

-- 2. Alta / Actualización de Usuario Administrador (dbo.Usuarios_Administradores)
-- Credenciales: Identificador = admin@portalrdata.com | Password = Admin@Portal2026!
IF NOT EXISTS (SELECT 1 FROM dbo.Usuarios_Administradores WHERE Email = 'admin@portalrdata.com')
BEGIN
    INSERT INTO dbo.Usuarios_Administradores (
        CodigoEmpleado,
        NombreCompleto,
        Email,
        PasswordHash,
        Rol,
        Activo,
        CreatedAt,
        UpdatedAt
    )
    VALUES (
        'EMP-ADMIN01',
        'Administrador General del Sistema',
        'admin@portalrdata.com',
        'talOyam2904R6OPvrK1yJQXz22g9ms+VgWV4UsTjTmA=:dEfo+mrvsYJ+iTWRWckSlM7cFRdPlL5ujA87eEAKqPQ+wf4f9m6zLwjE3fDMHowM/0QBXUPmcQeK82wX777lqw==',
        'ADMIN',
        1,
        SYSUTCDATETIME(),
        SYSUTCDATETIME()
    );
    PRINT 'Usuario Administrador creado exitosamente en Usuarios_Administradores.';
END
ELSE
BEGIN
    UPDATE dbo.Usuarios_Administradores
    SET PasswordHash = 'talOyam2904R6OPvrK1yJQXz22g9ms+VgWV4UsTjTmA=:dEfo+mrvsYJ+iTWRWckSlM7cFRdPlL5ujA87eEAKqPQ+wf4f9m6zLwjE3fDMHowM/0QBXUPmcQeK82wX777lqw==',
        Rol = 'ADMIN',
        Activo = 1,
        UpdatedAt = SYSUTCDATETIME()
    WHERE Email = 'admin@portalrdata.com';
    PRINT 'Usuario Administrador actualizado exitosamente.';
END
GO

-- 3. Alta / Actualización de Usuario Proveedor Demo (dbo.Usuarios_Proveedor)
-- Credenciales: Identificador = proveedor@demo.com o AAA010101AAA | Password = Proveedor@Portal2026!
DECLARE @ProveedorIdDemo INT = (SELECT TOP (1) ProveedorId FROM dbo.Cat_Proveedores WHERE RFC = 'AAA010101AAA');

IF NOT EXISTS (SELECT 1 FROM dbo.Usuarios_Proveedor WHERE Email = 'proveedor@demo.com')
BEGIN
    INSERT INTO dbo.Usuarios_Proveedor (
        ProveedorId,
        RFC,
        Email,
        PasswordHash,
        IntentosFallidos,
        BloqueadoHasta,
        Activo,
        CreatedAt,
        UpdatedAt
    )
    VALUES (
        @ProveedorIdDemo,
        'AAA010101AAA',
        'proveedor@demo.com',
        'IduVkGEGb5B9f1Ndxmur30ZQQx1kQ1ZGCPQNF8hdBdw=:g9axiBZ38bSOGhOCT8/u5jXHaQKg+tAYoHK+jXB6oduJ43MVfr7mGuOvi+idlB+I1tVQRB+Iy5O/8sNJoLKj3Q==',
        0,
        NULL,
        1,
        SYSUTCDATETIME(),
        SYSUTCDATETIME()
    );
    PRINT 'Usuario Proveedor Demo creado exitosamente en Usuarios_Proveedor.';
END
ELSE
BEGIN
    UPDATE dbo.Usuarios_Proveedor
    SET PasswordHash = 'IduVkGEGb5B9f1Ndxmur30ZQQx1kQ1ZGCPQNF8hdBdw=:g9axiBZ38bSOGhOCT8/u5jXHaQKg+tAYoHK+jXB6oduJ43MVfr7mGuOvi+idlB+I1tVQRB+Iy5O/8sNJoLKj3Q==',
        ProveedorId = @ProveedorIdDemo,
        RFC = 'AAA010101AAA',
        IntentosFallidos = 0,
        BloqueadoHasta = NULL,
        Activo = 1,
        UpdatedAt = SYSUTCDATETIME()
    WHERE Email = 'proveedor@demo.com';
    PRINT 'Usuario Proveedor Demo actualizado exitosamente.';
END
GO
