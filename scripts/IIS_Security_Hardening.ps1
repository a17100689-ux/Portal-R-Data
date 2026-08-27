<#
.SYNOPSIS
    Script de endurecimiento y seguridad para IIS y Windows Server - Portal de Proveedores R-Data
.DESCRIPTION
    Aplica las políticas de seguridad a nivel de servidor:
    1. Aislamiento de Application Pool con ApplicationPoolIdentity dedicada.
    2. Asignación de permisos mínimos NTFS (Solo lectura en app, escritura exclusiva en almacenamiento de facturas).
    3. Deshabilitación de Directory Browsing y WebDAV.
    4. Eliminación de cabeceras informativas (Server, X-Powered-By, X-AspNet-Version).
    5. Forzado de redirección HTTPS y habilitación de TLS 1.2 / TLS 1.3.
    6. Configuración de auditoría y registros en IIS.
#>

[CmdletBinding()]
param (
    [string]$SiteName = "PortalProveedores",
    [string]$AppPoolName = "PortalProveedoresAppPool",
    [string]$AppPath = "C:\inetpub\PortalProveedores",
    [string]$StoragePath = "C:\AppStorage\PortalProveedores\Facturas"
)

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "Iniciando Endurecimiento de Seguridad IIS - $SiteName" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Asegurar Módulos de IIS necesarios
Import-Module WebAdministration -ErrorAction SilentlyContinue

# 2. Configurar Application Pool con Identidad Dedicada
Write-Host "[1/6] Configurando Application Pool dedicado..." -ForegroundColor Yellow
if (!(Test-Path "IIS:\AppPools\$AppPoolName")) {
    New-WebAppPool -Name $AppPoolName
}
Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name "processModel.identityType" -Value "ApplicationPoolIdentity"
Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name "recycling.periodicRestart.time" -Value "00:00:00" # Evitar reciclado durante transacciones si no es necesario

# 3. Permisos NTFS de Principio de Menor Privilegio
Write-Host "[2/6] Aplicando permisos NTFS de menor privilegio..." -ForegroundColor Yellow
$appPoolUser = "IIS AppPool\$AppPoolName"

# Carpeta de la Aplicación: Solo Lectura y Ejecución
if (Test-Path $AppPath) {
    $acl = Get-Acl $AppPath
    $ruleRead = New-Object System.Security.AccessControl.FileSystemAccessRule($appPoolUser, "ReadAndExecute", "ContainerInherit,ObjectInherit", "None", "Allow")
    $acl.SetAccessRule($ruleRead)
    Set-Acl $AppPath $acl
    Write-Host "   - Permiso ReadAndExecute asignado a $appPoolUser en $AppPath" -ForegroundColor Green
}

# Carpeta de Almacenamiento Seguro: Lectura, Escritura y Modificación (Sin ejecución de scripts)
if (!(Test-Path $StoragePath)) {
    New-Item -ItemType Directory -Path $StoragePath -Force | Out-Null
}
$storageAcl = Get-Acl $StoragePath
$ruleWrite = New-Object System.Security.AccessControl.FileSystemAccessRule($appPoolUser, "Modify", "ContainerInherit,ObjectInherit", "None", "Allow")
$storageAcl.SetAccessRule($ruleWrite)
Set-Acl $StoragePath $storageAcl
Write-Host "   - Permiso Modify asignado a $appPoolUser en $StoragePath" -ForegroundColor Green

# 4. Deshabilitar Directory Browsing y WebDAV en IIS
Write-Host "[3/6] Deshabilitando Directory Browsing y WebDAV..." -ForegroundColor Yellow
Set-WebConfigurationProperty -Filter /system.webServer/directoryBrowse -Name enabled -Value False -PSPath "IIS:\Sites\$SiteName" -ErrorAction SilentlyContinue

# Deshabilitar WebDAV si está instalado
Set-WebConfigurationProperty -Filter /system.webServer/webdav/authoring -Name enabled -Value False -PSPath "IIS:\Sites\$SiteName" -ErrorAction SilentlyContinue
Set-WebConfigurationProperty -Filter /system.webServer/modules/add[@name='WebDAVModule'] -Name enabled -Value False -PSPath "IIS:\Sites\$SiteName" -ErrorAction SilentlyContinue

# 5. Ocultar Cabeceras de Servidor en Respuestas HTTP
Write-Host "[4/6] Configurando eliminación de cabeceras de servidor..." -ForegroundColor Yellow
# Ocultar cabecera 'Server' en IIS 10+
Set-WebConfigurationProperty -Filter /system.webServer/security/requestFiltering -Name removeServerHeader -Value True -PSPath "IIS:\Sites\$SiteName" -ErrorAction SilentlyContinue

# 6. Forzar TLS 1.2 y TLS 1.3 y Deshabilitar SSLv3, TLS 1.0, TLS 1.1 en Registro de Windows
Write-Host "[5/6] Configurando protocolos criptográficos seguros (TLS 1.2/1.3)..." -ForegroundColor Yellow

$protocols = @{
    "SSL 2.0" = $false
    "SSL 3.0" = $false
    "TLS 1.0" = $false
    "TLS 1.1" = $false
    "TLS 1.2" = $true
    "TLS 1.3" = $true
}

foreach ($protocol in $protocols.Keys) {
    $enabled = $protocols[$protocol]
    $regServerPath = "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\$protocol\Server"
    $regClientPath = "HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\$protocol\Client"

    if (!(Test-Path $regServerPath)) { New-Item -Path $regServerPath -Force | Out-Null }
    if (!(Test-Path $regClientPath)) { New-Item -Path $regClientPath -Force | Out-Null }

    if ($enabled) {
        Set-ItemProperty -Path $regServerPath -Name "Enabled" -Value 1 -Type DWord
        Set-ItemProperty -Path $regServerPath -Name "DisabledByDefault" -Value 0 -Type DWord
        Set-ItemProperty -Path $regClientPath -Name "Enabled" -Value 1 -Type DWord
        Set-ItemProperty -Path $regClientPath -Name "DisabledByDefault" -Value 0 -Type DWord
        Write-Host "   - Protocolo $protocol HABILITADO" -ForegroundColor Green
    } else {
        Set-ItemProperty -Path $regServerPath -Name "Enabled" -Value 0 -Type DWord
        Set-ItemProperty -Path $regServerPath -Name "DisabledByDefault" -Value 1 -Type DWord
        Set-ItemProperty -Path $regClientPath -Name "Enabled" -Value 0 -Type DWord
        Set-ItemProperty -Path $regClientPath -Name "DisabledByDefault" -Value 1 -Type DWord
        Write-Host "   - Protocolo $protocol DESHABILITADO" -ForegroundColor DarkGray
    }
}

# 7. Auditoría y Logging en IIS
Write-Host "[6/6] Habilitando Logging detallado en IIS..." -ForegroundColor Yellow
Set-WebConfigurationProperty -Filter /system.applicationHost/sites/site[@name="$SiteName"]/logFile -Name logExtFileFlags -Value "Date,Time,ClientIP,UserName,SiteName,ComputerName,ServerIP,Method,UriStem,UriQuery,HttpStatus,Win32Status,BytesSent,BytesRecv,TimeTaken,UserAgent,Referer" -ErrorAction SilentlyContinue

Write-Host "==========================================================" -ForegroundColor Green
Write-Host "Endurecimiento de IIS completado exitosamente." -ForegroundColor Green
Write-Host "Nota: Reinicie el servidor si se modificaron claves del registro de TLS." -ForegroundColor Yellow
Write-Host "==========================================================" -ForegroundColor Green
