using Microsoft.Extensions.Configuration;
using PortalProveedores.Application.Interfaces;

namespace PortalProveedores.Infrastructure.Storage;

public class SecureFileStorageService : IStorageService
{
    private readonly string _baseStoragePath;

    public SecureFileStorageService(IConfiguration configuration)
    {
        // Ruta externa o fuera de wwwroot configurada en appsettings
        string? configPath = configuration["Storage:SecureFolderPath"];
        if (string.IsNullOrWhiteSpace(configPath))
        {
            // Ruta segura por defecto fuera de la raíz web pública
            _baseStoragePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data", "SecureStorage", "Facturas");
        }
        else
        {
            _baseStoragePath = configPath;
        }

        if (!Directory.Exists(_baseStoragePath))
        {
            Directory.CreateDirectory(_baseStoragePath);
        }
    }

    public async Task<string> GuardarArchivoAsync(Stream stream, string extension, string subCarpeta, CancellationToken ct = default)
    {
        // Sanitizar subcarpeta para prevenir Directory Traversal (../)
        string safeSubPath = SanitizarRuta(subCarpeta);
        string targetDir = Path.Combine(_baseStoragePath, safeSubPath);

        if (!Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        // Generar nombre de archivo único con GUID para evitar sobreescritura o ejecución directa
        string safeExtension = extension.StartsWith(".") ? extension.ToLowerInvariant() : $".{extension.ToLowerInvariant()}";
        string internalFileName = $"{Guid.NewGuid():N}{safeExtension}";
        string fullPath = Path.Combine(targetDir, internalFileName);

        // Asegurar que la ruta final esté dentro del directorio base
        ValidarRutaSegura(fullPath, _baseStoragePath);

        if (stream.CanSeek)
            stream.Position = 0;

        using (var fileStream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, useAsync: true))
        {
            await stream.CopyToAsync(fileStream, ct);
        }

        return internalFileName;
    }

    public Task<Stream?> ObtenerArchivoStreamAsync(string nombreInterno, string subCarpeta, CancellationToken ct = default)
    {
        string safeSubPath = SanitizarRuta(subCarpeta);
        string safeFileName = Path.GetFileName(nombreInterno);
        string fullPath = Path.Combine(_baseStoragePath, safeSubPath, safeFileName);

        ValidarRutaSegura(fullPath, _baseStoragePath);

        if (!File.Exists(fullPath))
        {
            return Task.FromResult<Stream?>(null);
        }

        var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        return Task.FromResult<Stream?>(stream);
    }

    public Task<bool> EliminarArchivoAsync(string nombreInterno, string subCarpeta, CancellationToken ct = default)
    {
        try
        {
            string safeSubPath = SanitizarRuta(subCarpeta);
            string safeFileName = Path.GetFileName(nombreInterno);
            string fullPath = Path.Combine(_baseStoragePath, safeSubPath, safeFileName);

            ValidarRutaSegura(fullPath, _baseStoragePath);

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                return Task.FromResult(true);
            }
        }
        catch
        {
            // Ignorar en caso de error de eliminación segura
        }

        return Task.FromResult(false);
    }

    private static string SanitizarRuta(string path)
    {
        // Reemplazar separadores y remover intentos de "../"
        var partes = path.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries)
                         .Where(p => p != ".." && p != "." && !p.Contains(':'));
        return Path.Combine(partes.ToArray());
    }

    private static void ValidarRutaSegura(string fullPath, string basePath)
    {
        string fullNormalized = Path.GetFullPath(fullPath);
        string baseNormalized = Path.GetFullPath(basePath);

        if (!fullNormalized.StartsWith(baseNormalized, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Intento de acceso a una ruta no autorizada (Path Traversal detectado).");
        }
    }
}
