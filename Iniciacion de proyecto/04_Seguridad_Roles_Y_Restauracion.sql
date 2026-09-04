-- ============================================================================
-- SCRIPT 04: CONFIGURACIÓN DE SEGURIDAD, ROLES, MENOR PRIVILEGIO Y RESTAURACIÓN
-- PROYECTO: Portal de Facturación de Proveedores (Portal R-Data)
-- BASE DE DATOS: PortalProveedores_DB
-- ARQUITECTURA: DB_Optimizer_Pro
-- ============================================================================

USE master;
GO

-- ============================================================================
-- 1. CREACIÓN DE LOGINS A NIVEL SERVIDOR (CAMBIAR CONTRASEÑAS EN PRODUCCIÓN)
-- ============================================================================

-- Login para la aplicación web ASP.NET Core MVC
IF NOT EXISTS (SELECT name FROM sys.server_principals WHERE name = N'PortalAppLogin')
BEGIN
    CREATE LOGIN [PortalAppLogin] 
    WITH PASSWORD = N'P0rt@l_Pr0v33d0r3s_2026!Sec#Web',
         DEFAULT_DATABASE = [PortalProveedores_DB],
         CHECK_EXPIRATION = OFF,
         CHECK_POLICY = ON;
END
GO

-- Login para el Worker de Sincronización (Background Service)
IF NOT EXISTS (SELECT name FROM sys.server_principals WHERE name = N'PortalSyncWorkerLogin')
BEGIN
    CREATE LOGIN [PortalSyncWorkerLogin] 
    WITH PASSWORD = N'P0rt@l_SyncW0rk3r_2026!Sec#Int',
         DEFAULT_DATABASE = [PortalProveedores_DB],
         CHECK_EXPIRATION = OFF,
         CHECK_POLICY = ON;
END
GO

USE [PortalProveedores_DB];
GO

-- ============================================================================
-- 2. CREACIÓN DE USUARIOS DE BASE DE DATOS Y ROLES DE APLICACIÓN
-- ============================================================================

IF NOT EXISTS (SELECT name FROM sys.database_principals WHERE name = N'PortalAppUser')
BEGIN
    CREATE USER [PortalAppUser] FOR LOGIN [PortalAppLogin];
END
GO

IF NOT EXISTS (SELECT name FROM sys.database_principals WHERE name = N'PortalSyncWorkerUser')
BEGIN
    CREATE USER [PortalSyncWorkerUser] FOR LOGIN [PortalSyncWorkerLogin];
END
GO

-- Creación de Rol de Aplicación Web (Least Privilege)
IF NOT EXISTS (SELECT name FROM sys.database_principals WHERE name = N'Rol_PortalWeb_App' AND type = 'R')
BEGIN
    CREATE ROLE [Rol_PortalWeb_App];
END
GO

-- Creación de Rol para Worker de Sincronización
IF NOT EXISTS (SELECT name FROM sys.database_principals WHERE name = N'Rol_PortalSync_Worker' AND type = 'R')
BEGIN
    CREATE ROLE [Rol_PortalSync_Worker];
END
GO

-- Asignar usuarios a sus respectivos roles
ALTER ROLE [Rol_PortalWeb_App] ADD MEMBER [PortalAppUser];
ALTER ROLE [Rol_PortalSync_Worker] ADD MEMBER [PortalSyncWorkerUser];
GO

-- ============================================================================
-- 3. ASIGNACIÓN ESTRICTA DE PERMISOS (PRINCIPIO DE MENOR PRIVILEGIO)
-- ============================================================================

-- DENY DIRECT TABLE ACCESS: La aplicación Web JAMÁS tiene acceso directo SELECT/INSERT/UPDATE a tablas
DENY SELECT, INSERT, UPDATE, DELETE ON SCHEMA::dbo TO [Rol_PortalWeb_App];

-- GRANT EXECUTE ÚNICAMENTE a los procedimientos del portal web
GRANT EXECUTE ON dbo.sp_Portal_Usuario_ObtenerPorLogin TO [Rol_PortalWeb_App];
GRANT EXECUTE ON dbo.sp_Portal_Usuario_RegistrarIntentoFallido TO [Rol_PortalWeb_App];
GRANT EXECUTE ON dbo.sp_Portal_Usuario_RegistrarLoginExitoso TO [Rol_PortalWeb_App];
GRANT EXECUTE ON dbo.sp_Portal_ValidarFacturaPrevia TO [Rol_PortalWeb_App];
GRANT EXECUTE ON dbo.sp_Portal_RegistrarFacturaCompleta TO [Rol_PortalWeb_App];
GRANT EXECUTE ON dbo.sp_Portal_ListarFacturasProveedor TO [Rol_PortalWeb_App];
GRANT EXECUTE ON dbo.sp_Portal_ObtenerFacturaDetalle TO [Rol_PortalWeb_App];
GRANT EXECUTE ON dbo.sp_Portal_Auditoria_Registrar TO [Rol_PortalWeb_App];
GRANT EXECUTE ON dbo.sp_Portal_ObtenerConfiguracionEmpresa TO [Rol_PortalWeb_App];
GRANT EXECUTE ON dbo.sp_Portal_CompararOrdenCompra TO [Rol_PortalWeb_App];
GRANT EXECUTE ON dbo.sp_Portal_Admin_Login TO [Rol_PortalWeb_App];
GRANT EXECUTE ON dbo.sp_Portal_Admin_CrearUsuarioProveedor TO [Rol_PortalWeb_App];
GRANT EXECUTE ON dbo.sp_Portal_Admin_ResetearPasswordProveedor TO [Rol_PortalWeb_App];
GRANT EXECUTE ON dbo.sp_Portal_Admin_CambiarEstatusUsuario TO [Rol_PortalWeb_App];

-- Permisos de ejecución de tipos TVP para la aplicación web
GRANT EXECUTE ON TYPE::dbo.typePortal_FacturaDetalle TO [Rol_PortalWeb_App];
GRANT EXECUTE ON TYPE::dbo.typePortal_FacturaArchivo TO [Rol_PortalWeb_App];
GRANT EXECUTE ON TYPE::dbo.typePortal_FacturaLocalizacion TO [Rol_PortalWeb_App];

-- GRANT EXECUTE al Worker de Sincronización
GRANT EXECUTE ON dbo.sp_Sync_ObtenerLotePendiente TO [Rol_PortalSync_Worker];
GRANT EXECUTE ON dbo.sp_Sync_ConfirmarSincronizacion TO [Rol_PortalSync_Worker];
GRANT EXECUTE ON dbo.sp_Sync_RegistrarFallo TO [Rol_PortalSync_Worker];
GRANT EXECUTE ON dbo.sp_Sync_RefrescarCatalogosDesdeCentral TO [Rol_PortalSync_Worker];
GRANT EXECUTE ON dbo.sp_Sync_EjecutarInsertCentral_EnMismaInstancia TO [Rol_PortalSync_Worker];
GRANT EXECUTE ON dbo.sp_Sync_ActualizarEstatusEntregaMercancia TO [Rol_PortalSync_Worker];
GO

-- ============================================================================
-- 4. PROCEDIMIENTO Y SCRIPT DE RESPALDO (BACKUP SEGURO CON CHECKSUM)
-- ============================================================================
-- Para generar un respaldo completo manual o programado:
-- BACKUP DATABASE [PortalProveedores_DB]
-- TO DISK = N'C:\AppStorage\Backups\PortalProveedores_DB_Full.bak'
-- WITH FORMAT, INIT,
--      NAME = N'PortalProveedores_DB - Full Backup',
--      SKIP, NOREWIND, NOUNLOAD,
--      COMPRESSION,
--      CHECKSUM,
--      STATS = 10;
-- GO

-- ============================================================================
-- 5. PROTOCOLO DE RESTAURACIÓN Y REPARACIÓN DE HUÉRFANOS (DISASTER RECOVERY)
-- Ejecutar estas instrucciones al restaurar la base en un servidor destino:
--
-- PASO 1: RESTAURAR LA BASE DE DATOS EN EL NUEVO SERVIDOR
-- USE master;
-- ALTER DATABASE [PortalProveedores_DB] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
-- RESTORE DATABASE [PortalProveedores_DB]
-- FROM DISK = N'C:\AppStorage\Backups\PortalProveedores_DB_Full.bak'
-- WITH REPLACE, RECOVERY, STATS = 10;
-- ALTER DATABASE [PortalProveedores_DB] SET MULTI_USER;
--
-- PASO 2: VINCULACIÓN DE USUARIOS HUÉRFANOS (Fix Orphaned Users)
-- USE [PortalProveedores_DB];
-- IF EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'PortalAppUser')
-- BEGIN
--     ALTER USER [PortalAppUser] WITH LOGIN = [PortalAppLogin];
-- END;
-- IF EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'PortalSyncWorkerUser')
-- BEGIN
--     ALTER USER [PortalSyncWorkerUser] WITH LOGIN = [PortalSyncWorkerLogin];
-- END;
--
-- PASO 3: VALIDAR QUE NO QUEDEN HUÉRFANOS
-- SELECT dp.name AS UsuarioHuerfano
-- FROM sys.database_principals dp
-- LEFT JOIN sys.server_principals sp ON dp.sid = sp.sid
-- WHERE dp.type IN ('S', 'U', 'G') 
--   AND dp.authentication_type = 1 
--   AND sp.sid IS NULL
--   AND dp.name NOT IN ('guest', 'INFORMATION_SCHEMA', 'sys');
GO
