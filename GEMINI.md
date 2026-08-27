# Contexto del Proyecto: Portal de Facturación de Proveedores (Portal R-Data)

Este repositorio contiene la solución en C# .NET para el portal web de proveedores.

## Directrices Clave de Desarrollo
1. **Seguridad de Archivos**: Validar obligatoriamente los Magic Numbers de archivos (PDF `%PDF-` y XML CFDI). Límite de 5 MB. Almacenar fuera de `wwwroot`.
2. **Seguridad en Datos**: Consultas a base de datos únicamente con Stored Procedures o parámetros fuertemente tipados. Sin concatenación SQL.
3. **Seguridad Web**: Cookies con `HttpOnly`, `Secure` y `SameSite`. Sin exposición de stack traces ni cabeceras informativas de servidor en IIS.
4. **Arquitectura**: Arquitectura por capas (Clean / N-Tier Architecture):
   - `PortalProveedores.Core`: Entidades de dominio.
   - `PortalProveedores.Application`: Casos de uso, validadores y contratos de servicios.
   - `PortalProveedores.Infrastructure`: Persistencia con Stored Procedures y almacenamiento en disco seguro.
   - `PortalProveedores.Web`: Capa de presentación ASP.NET Core MVC con middleware de seguridad.
