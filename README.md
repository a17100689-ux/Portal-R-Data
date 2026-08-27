# Portal de Facturación de Proveedores (Portal R-Data)

Portal web desarrollado en **ASP.NET Core / C#** para la recepción, validación estricta y gestión de facturas electrónicas de proveedores (archivos XML de CFDI y representación impresa en PDF).

---

## 🏛 Arquitectura de la Solución

La solución sigue el patrón de arquitectura por capas (Clean / N-Tier Architecture):

```
Portal R-Data/
├── .agents/
│   └── rules/
│       └── portal-security.md            # Reglas obligatorias de seguridad del proyecto
├── .gitignore                            # Plantilla de exclusiones Git para .NET
├── GEMINI.md                             # Contexto y directrices para desarrollo
├── mcp_config.json                       # Configuración y directivas de seguridad
├── README.md                             # Documentación general
├── PortalProveedores.sln                 # Solución principal de Visual Studio / .NET
├── scripts/
│   ├── Database_Schema_Procedures.sql    # Tablas, índices, Stored Procedures y usuario con menor privilegio
│   └── IIS_Security_Hardening.ps1        # Script de aprovisionamiento seguro de IIS / TLS
├── src/
│   ├── PortalProveedores.Core/           # Entidades de dominio (Factura, Proveedor, Usuario, Auditoria)
│   ├── PortalProveedores.Application/    # Casos de uso, DTOs, validadores de Magic Numbers y parser CFDI
│   ├── PortalProveedores.Infrastructure/ # Repositorios con Stored Procedures, almacenamiento de archivos seguro
│   └── PortalProveedores.Web/            # Capa de presentación ASP.NET Core MVC con middleware de seguridad
└── tests/
    └── PortalProveedores.UnitTests/      # Pruebas unitarias de validación y seguridad
```

---

## 🔒 Medidas de Seguridad Implementadas

### 1. Seguridad a Nivel de Aplicación (C# / .NET)
* **Validación de Magic Numbers**: Validación de firmas de bytes reales (`%PDF-` para PDF y encabezados válidos para XML CFDI) en `FileSecurityValidator`.
* **Límite de Tamaño de Archivo**: Límite estricto de **5 MB** tanto a nivel de middleware como en los endpoints (`[RequestSizeLimit(5 * 1024 * 1024)]`).
* **Almacenamiento Seguro**: Los archivos se almacenan fuera de `wwwroot` (`C:\AppStorage\...`), renombrados con GUIDs únicos y con sanitización de rutas para mitigar Directory / Path Traversal.
* **Protección contra SQL Injection**: Consultas y transacciones implementadas exclusivamente a través de **Stored Procedures** y parámetros tipados en `SqlFacturaRepository` y `SqlUsuarioRepository`.
* **Autenticación y Sesiones**: Cookies configuradas con `HttpOnly = true`, `SecurePolicy = CookieSecurePolicy.Always`, `SameSite = SameSiteMode.Strict`, tokens Antiforgery y hashing de contraseñas robusto con PBKDF2/SHA512.
* **Manejo Global de Excepciones**: Middleware que intercepta excepciones no controladas, genera un identificador de ticket de soporte y responde con vistas de error genéricas sin exponer stack traces ni infraestructura.

### 2. Seguridad a Nivel de Servidor (IIS & OS)
* **`web.config` Endurecido**:
  * Remoción de cabeceras reveladoras: `Server`, `X-Powered-By`, `X-AspNet-Version`, `X-AspNetMvc-Version`.
  * Deshabilitación de `Directory Browsing`.
  * Deshabilitación de `WebDAV` y verbos HTTP no permitidos (`TRACE`, `TRACK`, `DEBUG`).
  * Límite de carga en IIS configurado a 5 MB (`maxAllowedContentLength="5242880"`).
* **Script PowerShell de Aprovisionamiento (`scripts/IIS_Security_Hardening.ps1`)**:
  * Application Pool aislado bajo identidad `ApplicationPoolIdentity`.
  * Permisos NTFS de solo lectura en el directorio de la aplicación y modificación exclusiva en la carpeta de almacenamiento de facturas.
  * Habilitación de TLS 1.2 / TLS 1.3 y deshabilitación de SSLv3, TLS 1.0 y TLS 1.1 en el registro de Windows Server.

---

## 🚀 Puesta en Marcha Local

### Prerrequisitos
* [.NET 8.0 SDK / .NET 10.0 SDK](https://dotnet.microsoft.com/download)
* SQL Server 2019+ o Azure SQL

### Pasos de Ejecución
1. **Crear Base de Datos y Stored Procedures**:
   Ejecuta el script [`scripts/Database_Schema_Procedures.sql`](./scripts/Database_Schema_Procedures.sql) en SQL Server Management Studio (SSMS).

2. **Configurar Cadena de Conexión**:
   Ajusta la conexión en `src/PortalProveedores.Web/appsettings.json`:
   ```json
   "ConnectionStrings": {
     "DefaultConnection": "Server=localhost;Database=PortalProveedoresDB;Trusted_Connection=True;TrustServerCertificate=True;"
   }
   ```

3. **Ejecutar Pruebas Unitarias**:
   ```powershell
   dotnet test
   ```

4. **Compilar y Levantar el Portal**:
   ```powershell
   dotnet run --project src/PortalProveedores.Web
   ```
