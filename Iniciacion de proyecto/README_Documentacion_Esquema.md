# Documentación Técnica de Base de Datos: PortalProveedores_DB

**Rol:** Arquitecto de Bases de Datos Senior y Especialista en Rendimiento (`DB_Optimizer_Pro`)  
**Fecha:** Septiembre 2026  
**Entorno de Pruebas:** Servidor `10.0.3.5` (Instancia SQL Server 2022 Developer, coexistiendo con `Punto_de_Venta`)  
**Entorno de Producción:** Diseñado para desacoplamiento físico a servidor independiente.  
**Estado:** Probado y desplegado exitosamente en el servidor de pruebas con sincronización inicial verificada (1,398 proveedores, 133 sucursales, 35,010 artículos y reglas).

---

## 1. Visión General de la Arquitectura

`PortalProveedores_DB` es la base de datos perimetral diseñada para dar servicio al portal web de proveedores. Su objetivo primordial es aislar los accesos externos respecto al ERP central (`Punto_de_Venta`), eliminando riesgos de filtración de información (*data leakage*), bloqueos de transacciones en tiendas físicas y exposición de costos y márgenes internos.

### Principios Clave de Rendimiento y Diseño:
1. **Concurrencia sin Bloqueos:** `READ_COMMITTED_SNAPSHOT ON` activado por defecto para evitar contención entre lectores y escritores.
2. **Cero Tablas HEAP:** Cada tabla cuenta con una Clave Primaria Agrupada (`PRIMARY KEY CLUSTERED`) monotónica (`INT` o `BIGINT IDENTITY`), garantizando B-Trees ordenados secuencialmente y eliminando *RID Lookups*.
3. **Índices Covering y Sargabilidad:** Consultas de búsqueda diseñadas con operadores `=`, `BETWEEN` o prefijos indexables, erradicando los `LIKE '%texto'` que causaban escaneos completos de 1.33 millones de registros en la base central.
4. **Patrón Transaccional Outbox (`Sync_Transacciones_Cola`):** Las facturas se guardan y validan de inmediato para el proveedor en el portal, mientras que la sincronización con el ERP central se procesa de forma asíncrona y resiliente.
5. **Collation Unificada:** `SQL_Latin1_General_CP1_CI_AS` idéntica a la base central `Punto_de_Venta` para garantizar compatibilidad binaria cero-errores en cruces de datos.

---

## 2. Inventario de Archivos en la Carpeta `Iniciacion de proyecto`

- **`01_Creacion_Base_Datos_Y_Tablas.sql`**: Script DDL idempotente completo para la creación de la base de datos, tipos TVP, tablas de catálogos locales, tablas transaccionales, auditoría y sus índices agrupados y no agrupados.
- **`02_Procedimientos_Almacenados.sql`**: Procedimientos del portal web (autenticación segura, validación sargable previa, registro atómico con TVPs, paginación eficiente de facturas).
- **`03_Procedimientos_Sincronizacion_Central.sql`**: Procedimientos para el puente de integración con `Punto_de_Venta` (despacho concurrente con `UPDLOCK, READPAST`, asignación de folios regionales en `Datos_De_Sistema_Central`, inserción en `Compras_Y_Devoluciones`, `Detalle` y `Repositorio_De_Compras_Xml`, y sincronización periódica de catálogos).
- **`04_Seguridad_Roles_Y_Restauracion.sql`**: Creación de logins, usuarios huérfanos, roles de menor privilegio (`Rol_PortalWeb_App`, `Rol_PortalSync_Worker`), permisos estrictos `GRANT EXECUTE` (cero permisos a tablas) y protocolo de respaldo/restauración de desastres.
- **`README_Documentacion_Esquema.md`**: Esta documentación técnica completa y arquitectura de decisión.

---

## 3. Documentación del Esquema de Tablas

### 3.1. Tablas de Catálogos Locales (Caché / Réplica desde Central)

#### `dbo.Cat_Proveedores`
Almacena los proveedores habilitados para interactuar en el portal web.
- **`ProveedorId` (INT, PK Clustered, Identity):** Identificador interno autonumérico.
- **`CodigoProveedor` (VARCHAR(15), Unique):** Código asignado en el ERP central.
- **`RFC` (VARCHAR(15)):** Registro Federal de Contribuyentes. Indexado en `IX_Cat_Proveedores_RFC`.
- **`RazonSocial` (VARCHAR(200)):** Nombre comercial o legal del proveedor.
- **`CondicionesPago` (VARCHAR(50)):** Lista de días de crédito permitidos (ej. `'0,5,15,30'`).
- **`RequiereValidarCompra` (BIT, Default 1):** Regla de negocio que exige validación en portal.
- **`OrdenCompraObligatoria` (BIT, Default 1):** Indica si requiere una OC asociada.
- **`EsProveedorNacional` (BIT, Default 1):** Distingue proveedores nacionales de extranjeros.
- **`Activo` (BIT, Default 1):** Bandera de estatus activo.
- **Auditoría:** `CreatedAt`, `UpdatedAt` (DATETIME2(3)).

#### `dbo.Cat_Sucursales`
Sucursales autorizadas para recibir compras de proveedores.
- **`SucursalId` (INT, PK Clustered, Identity):** Identificador único.
- **`NumeroCortoSucursal` (TINYINT, Unique):** Clave numérica de sucursal central.
- **`CodigoSucursal` (VARCHAR(10)):** Clave alfanumérica de sucursal.
- **`Descripcion` (VARCHAR(100)):** Nombre descriptivo de la sucursal.
- **`Region` (VARCHAR(20)):** Región operativa (`OCC`, `PEN`, `GOL`, `NOR`, `CEN`).
- **`AplicaLocalizador` (BIT, Default 0):** Determina si requiere captura de ubicaciones en bodega.
- **`Activo` (BIT, Default 1):** Estatus de la sucursal.

#### `dbo.Cat_Articulos_Reglas`
Caché de artículos con sus restricciones fiscales y operativas.
- **`ArticuloId` (INT, PK Clustered, Identity):** Identificador primario.
- **`CodigoArticulo` (VARCHAR(20), Unique):** SKU o código del artículo.
- **`Descripcion` (VARCHAR(255)):** Descripción técnica.
- **`CodigoLinea` (VARCHAR(10)):** Familia o línea de producto.
- **`IdProductosyServicios` (VARCHAR(15)):** Clave SAT requerida en CFDI 4.0.
- **`FactorConversion` (DECIMAL(12,4)):** Factor de empaque/conversión.
- **`EsPlomo` (BIT):** Indicador para artículos con manejo especial (`CodigoLinea = '04049'`).
- **`PermitidoCFDI_G03` (BIT):** Bandera precalculada que indica si el artículo está autorizado cuando el uso de CFDI es Gastos en General (líneas `04028, 03043, 04049, 03032, 04048`).
- **`AplicaLocalizador` (BIT):** Requiere captura de ubicación física en bodega.

#### `dbo.Cat_Ordenes_Compra` y `dbo.Cat_Ordenes_Compra_Detalle`
Almacenan las órdenes de compra abiertas emitidas al proveedor para su cotejo previo a la facturación. Permite cotejar de forma instantánea precios y cantidades autorizadas contra lo ingresado en el XML, evitando discrepancias antes de enviar al ERP central.

---

### 3.2. Tablas de Seguridad y Accesos

#### `dbo.Usuarios_Proveedor`
Cuentas de usuario de los proveedores para autenticarse en el portal.
- **`UsuarioId` (INT, PK Clustered, Identity):** Identificador de usuario.
- **`ProveedorId` (INT, FK a Cat_Proveedores):** Relación con el proveedor.
- **`RFC` (VARCHAR(15)):** RFC del proveedor.
- **`Email` (VARCHAR(120), Unique):** Correo electrónico del usuario.
- **`PasswordHash` (VARCHAR(255)):** Hash criptográfico robusto generado con **BCrypt** o **PBKDF2** con salt aleatorio (reemplazo estricto del vulnerable SHA-1).
- **`IntentosFallidos` (TINYINT, Default 0):** Contador de bloqueos por fuerza bruta.
- **`BloqueadoHasta` (DATETIME2(3), Nullable):** Marca de tiempo hasta la cual la cuenta queda temporalmente inhabilitada tras exceder el límite de intentos (5 intentos = 15 min de bloqueo).
- **`Activo` (BIT, Default 1):** Estatus del usuario.
- **`UltimoAcceso` (DATETIME2(3)):** Registro de último login exitoso.

#### `dbo.Bitacora_Accesos`
Auditoría de seguridad y telemetría de autenticación.
- Registra `RFC`, `DireccionIP`, resultado `Exitoso`, detalle y fecha UTC.

---

### 3.3. Tablas Transaccionales de Facturación

#### `dbo.Facturas_Cabecera`
Registro de comprobantes fiscales subidos por los proveedores.
- **`FacturaId` (BIGINT, PK Clustered, Identity):** Clave primaria monotónica.
- **`ProveedorId` (INT, FK):** Identificador del proveedor.
- **`CodigoProveedor` (VARCHAR(15)):** Código asignado.
- **`UUID` (VARCHAR(36), Unique):** Folio Fiscal SAT (formato 8-4-4-4-12).
- **`FolioFactura` (VARCHAR(40)):** Folio interno del proveedor.
- **`FechaFactura` (DATETIME2(0)):** Fecha de emisión según CFDI.
- **`FechaRecepcion` (DATETIME2(3)):** Fecha y hora exacta de carga en portal.
- **Campos Financieros:** `Subtotal`, `Descuento`, `IvaTrasladado`, `IvaRetenido`, `CostoTotal` (todos `DECIMAL(18,2)` exactos).
- **Campos Fiscales SAT:** `CodigoUsoCFDI`, `CodigoCFDIMetodoPago`, `CodigoCFDIFormaPago`, `RegimenFiscalEmisor`, `RegimenFiscalReceptor`.
- **Estatus de Proceso:**
  - `EstatusValidacion`: 1 (Recibida), 2 (Validada OK), 3 (Rechazada con motivo).
  - `EstatusSincronizacion`: 0 (Sin procesar), 1 (En Cola), 2 (Sincronizada Central), 3 (Error).
  - `NumeroDocumentoCentral` (DECIMAL(9,0)): Consecutivo final asignado en `Compras_Y_Devoluciones`.
- **Auditoría:** `CreatedAt`, `UpdatedAt`, `CreatedByIp`.

#### `dbo.Facturas_Detalle`
Partidas y conceptos contenidos en el comprobante fiscal.
- **`FacturaDetalleId` (BIGINT, PK Clustered, Identity):** Identificador de la partida.
- **`FacturaId` (BIGINT, FK):** Referencia a la cabecera.
- **`Renglon` (SMALLINT):** Posición o secuencia de partida.
- **`CodigoArticulo` (VARCHAR(20)):** Código interno validado.
- **`ClaveProdServSat` (VARCHAR(15)):** Clave SAT.
- **`Cantidad` (DECIMAL(12,4)):** Cantidad facturada.
- **`PrecioUnitarioSinDescuento` / `PrecioUnitarioConDescuento` (DECIMAL(18,4)):** Precios unitarios.
- **`Importe` (DECIMAL(18,2)):** Importe neto de la partida.

#### `dbo.Facturas_Archivos`
Almacenamiento seguro de metadatos de archivos XML y PDF.
- **`ArchivoId` (BIGINT, PK Clustered, Identity):** Clave primaria.
- **`FacturaId` (BIGINT, FK):** Factura a la que pertenece el archivo.
- **`TipoArchivo` (VARCHAR(4)):** 'XML' o 'PDF'.
- **`NombreAlmacenamiento` (VARCHAR(100)):** GUID aleatorio generado en disco.
- **`RutaFisicaSegura` (VARCHAR(300)):** Directorio fuera de `wwwroot` (`C:\AppStorage\Facturas\...`).
- **`HashSha256` (CHAR(64), Unique):** Hash criptográfico para garantizar la integridad y evitar duplicidad física de archivos.
- **`TamanoBytes` (INT):** Tamaño validado (máximo 5 MB por política de seguridad).
- **`XmlContenido` (VARCHAR(MAX), Nullable):** Contenido del XML para consultas sin acceso a disco.

#### `dbo.Sync_Transacciones_Cola`
Cola transaccional para sincronización asíncrona hacia el ERP central (Outbox Pattern).
- **`SyncId` (BIGINT, PK Clustered, Identity):** Identificador del trabajo.
- **`FacturaId` (BIGINT, FK):** Factura por sincronizar.
- **`TipoOperacion` (VARCHAR(40)):** `COMPRA_NORMAL` o `COMPRA_MESA_CONTROL`.
- **`EstadoSync` (VARCHAR(20)):** `PENDIENTE`, `EN_PROCESO`, `COMPLETADO`, `FALLIDO`.
- **`Intentos` (TINYINT):** Contador de reintentos ante caídas de red o bloqueos temporales en central.

---

#### `dbo.Configuracion_Empresa_Reglas`
Parámetros fiscales y de negocio de la empresa receptora (Radial Llantas) para validación offline ultra-rápida.
- **`ConfiguracionId` (INT, PK Clustered, Identity):** Identificador de configuración.
- **`RfcEmpresaReceptora` (VARCHAR(15)):** RFC oficial de la empresa receptora (`RLA8103252S2`).
- **`RazonSocialReceptora` (VARCHAR(200)):** `RADIAL LLANTAS`.
- **`RegimenFiscalReceptor` (VARCHAR(5)):** Régimen fiscal (`601` - General de Ley Personas Morales).
- **`CodigoPostalReceptor` (VARCHAR(10)):** Código postal (`44920`).
- **`DiasToleranciaFactura` (INT):** Límite máximo de días hacia atrás permitidos para la emisión de la factura (3 días).
- **`ToleranciaPrecioOC` (DECIMAL(5,2)):** Tolerancia en centavos/pesos permitida en precio unitario (+/- $0.99).
- **`LimiteTamanoArchivoMB` (INT):** Límite de carga de XML/PDF (5 MB).
- **`VersionCfdiPermitida` (VARCHAR(5)):** Versión exigida del comprobante (`4.0`).

#### `dbo.Usuarios_Administradores`
Usuarios internos de la empresa que gestionan el portal (personal de Sistemas, Compras y Mesa de Control).
- **`AdminId` (INT, PK Clustered, Identity):** Identificador del empleado interno.
- **`CodigoEmpleado` (VARCHAR(15), Unique):** Número de nómina o empleado.
- **`NombreCompleto` (VARCHAR(150)):** Nombre del administrador.
- **`Email` (VARCHAR(120), Unique):** Correo corporativo.
- **`PasswordHash` (VARCHAR(255)):** Hash BCrypt / PBKDF2.
- **`Rol` (VARCHAR(30)):** Rol interno (`ADMIN`, `COMPRAS`, `MESA_CONTROL`).
- **`Activo` (BIT):** Estatus del usuario administrativo.

---

## 4. Procedimientos Almacenados

| Procedimiento | Capa / Módulo | Descripción Técnica |
| :--- | :--- | :--- |
| `sp_Portal_Usuario_ObtenerPorLogin` | Seguridad Proveedor | Obtiene credenciales y estado del proveedor por RFC o Email. |
| `sp_Portal_Usuario_RegistrarIntentoFallido` | Seguridad Proveedor | Control de fuerza bruta; bloquea por 15 min al 5to intento. |
| `sp_Portal_Usuario_RegistrarLoginExitoso` | Seguridad Proveedor | Reinicia intentos y registra IP en bitácora. |
| `sp_Portal_ValidarFacturaPrevia` | Facturación Web | **Optimización Sargable:** Búsqueda ultra-rápida por índice en UUID y Folio. |
| `sp_Portal_RegistrarFacturaCompleta` | Facturación Web | **Transacción Atómica:** Inserta cabecera, detalle (TVP), archivos (TVP) y encola en `Sync_Transacciones_Cola`. |
| `sp_Portal_ListarFacturasProveedor` | Consulta Web | **Paginación Eficiente:** `OFFSET ... FETCH NEXT` sobre índice covering filtrado por proveedor. |
| `sp_Portal_ObtenerFacturaDetalle` | Consulta Web | Devuelve cabecera, partidas y archivos validando pertenencia del proveedor. |
| `sp_Portal_ObtenerConfiguracionEmpresa` | Validación Fiscal | Obtiene RFC receptor, régimen y días de tolerancia para validación en el cliente/servidor. |
| `sp_Portal_CompararOrdenCompra` | Validación de Negocio | **Cotejo de OC:** Compara artículos, precios (tolerancia \$0.99) y cantidades restantes contra la Orden de Compra. |
| `sp_Portal_Admin_Login` | Administración Interna | Autenticación para empleados internos administradores. |
| `sp_Portal_Admin_CrearUsuarioProveedor` | Administración Interna | Alta y aprovisionamiento controlado de credenciales a proveedores por personal interno. |
| `sp_Portal_Admin_ResetearPasswordProveedor` | Administración Interna | Restablecimiento directo de contraseñas por personal interno. |
| `sp_Portal_Admin_CambiarEstatusUsuario` | Administración Interna | Activación o bloqueo administrativo de cuentas de proveedores. |
| `sp_Sync_ObtenerLotePendiente` | Sincronización | Despacho concurrente seguro mediante `UPDLOCK, READPAST`. |
| `sp_Sync_ConfirmarSincronizacion` | Sincronización | Actualiza consecutivo central y marca trabajo completado. |
| `sp_Sync_RegistrarFallo` | Sincronización | Incrementa reintentos y registra bitácora del error. |
| `sp_Sync_EjecutarInsertCentral_EnMismaInstancia` | Sincronización | Ejecuta la transacción en `Punto_de_Venta`: incrementa consecutivo en `Datos_De_Sistema_Central`, inserta en `Compras_Y_Devoluciones`, `Detalle` (sin columna computada) y `Repositorio_De_Compras_Xml`. |
| `sp_Sync_RefrescarCatalogosDesdeCentral` | Sincronización | MERGE de catálogos desde las vistas centrales hacia las tablas locales del portal. |
| `sp_Sync_ActualizarEstatusEntregaMercancia` | Sincronización | Monitorea la recepción física en tiendas/central y actualiza el estatus a `ENTREGADA_SUCURSAL`. |

---

## 5. Guía de Transición: De Entorno de Pruebas a Producción

Actualmente, por ser entorno de pruebas, `PortalProveedores_DB` convive en la misma instancia y dirección IP (`10.0.3.5`) que `Punto_de_Venta`.

### Pasos para migrar a Servidor Dedicado en Producción:
1. **Ejecutar Respaldos:**
   - Generar un backup completo de `PortalProveedores_DB` usando el script `04_Seguridad_Roles_Y_Restauracion.sql`.
2. **Restaurar en el Nuevo Servidor:**
   - Restaurar en la nueva máquina y ejecutar el bloque de **reparación de usuarios huérfanos** (`ALTER USER ... WITH LOGIN`).
3. **Mecanismo de Comunicación:**
   - Al usar el **Background Worker Service en C# .NET (Recomendado)**: Simplemente se configuran dos cadenas de conexión en `appsettings.json`:
     - `ConnectionStrings:PortalDb` -> Servidor del Portal.
     - `ConnectionStrings:CentralDb` -> Servidor Central (`10.0.3.5`).
     No se requiere ningún cambio en el esquema ni en el código SQL.
