# Reglas de Seguridad Obligatorias - Portal de Facturación de Proveedores

Este documento establece las políticas de seguridad estrictas que deben ser respetadas e implementadas en todo el código y configuración del portal.

---

## 1. Seguridad a Nivel de Aplicación (C# / .NET)

### Validación Estricta de Archivos
- **Validación de Magic Numbers**: NUNCA confiar únicamente en la extensión del archivo o la cabecera `Content-Type` enviada por el cliente.
  - Para **PDF**: Verificar que los primeros bytes correspondan a `%PDF-` (`0x25, 0x50, 0x44, 0x46, 0x2D`).
  - Para **XML (CFDI)**: Verificar que comience con la cabecera XML y contenga una estructura válida de comprobante fiscal (`cfdi:Comprobante`).
- **Límite de Tamaño**:
  - Configurar un límite máximo de **5 MB** por archivo para evitar ataques DoS por saturación de disco o memoria.
  - Aplicar `[RequestSizeLimit(5 * 1024 * 1024)]` en los controladores que reciban cargas de archivos.
- **Almacenamiento Seguro**:
  - Guardar los archivos fuera del directorio raíz público (`wwwroot`).
  - Renombrar los archivos en el servidor usando GUIDs aleatorios para prevenir sobreescritura accidental o ataques de Path Traversal.
  - Desactivar permisos de ejecución en el directorio de almacenamiento.

### Protección de Base de Datos
- **Consultas Parametrizadas y Stored Procedures**:
  - Toda consulta o transacción con la base de datos debe realizarse exclusivamente a través de **Stored Procedures** o comandos fuertemente parametrizados.
  - Prohibida cualquier concatenación de cadenas en sentencias SQL.
- **Principio de Privilegios Mínimos**:
  - El usuario de base de datos de la aplicación debe tener únicamente permisos `EXECUTE` en los Stored Procedures requeridos y permisos de lectura/escritura limitados a las tablas del portal. Prohibidos permisos de `sysadmin` o `db_owner`.

### Autenticación y Gestión de Sesiones
- **Hashing de Contraseñas**: Utilizar algoritmos criptográficos robustos como Argon2, BCrypt o PBKDF2 con salt aleatorio.
- **Cookies Seguras**:
  - `HttpOnly = true` (previene acceso a cookies desde JavaScript / ataques XSS).
  - `Secure = true` / `CookieSecurePolicy.Always` (obliga transmisión exclusiva por HTTPS).
  - `SameSite = SameSiteMode.Strict` o `Lax` para mitigación de CSRF.
  - Protección Antiforgery (`@Html.AntiForgeryToken()` y `[ValidateAntiForgeryToken]`).

### Manejo de Excepciones y Datos Sensibles
- Captura global de excepciones mediante middleware.
- Prohibido exponer stack traces, nombres de tablas, cadenas de conexión o detalles internos al usuario final.
- Responder siempre con mensajes de error genéricos y amigables.

---

## 2. Seguridad a Nivel de Servidor (IIS y Sistema Operativo)

### Aislamiento de Application Pool
- Ejecutar el sitio bajo `ApplicationPoolIdentity` dedicada.
- Permisos NTFS: Solo lectura en los archivos de la aplicación (`C:\inetpub\PortalProveedores`), y permisos de lectura/escritura exclusivamente en la carpeta designada para almacenamiento de facturas (`C:\AppStorage\Facturas`).

### Reducción de la Superficie de Ataque en IIS
- Deshabilitar `Directory Browsing`.
- Remover o deshabilitar módulos innecesarios como WebDAV.
- Remover cabeceras de servidor que revelan tecnología: `Server`, `X-Powered-By`, `X-AspNet-Version`.

### Cifrado de Red y Protocolos
- Redirección forzada de HTTP (80) a HTTPS (443).
- Habilitar exclusivamente TLS 1.2 y TLS 1.3 a nivel de Windows Server. Deshabilitar SSLv3, TLS 1.0 y TLS 1.1.
- Habilitar HSTS (`Strict-Transport-Security`).

### Auditoría y Monitoreo
- Configurar logs en IIS y registro de eventos para intentos fallidos de autenticación o errores críticos.
