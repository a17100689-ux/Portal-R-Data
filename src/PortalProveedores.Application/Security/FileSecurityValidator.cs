using System.Text;
using PortalProveedores.Application.DTOs;

namespace PortalProveedores.Application.Security;

public interface IFileSecurityValidator
{
    ResultadoValidacionArchivo ValidarPdf(Stream stream, long length, string fileName);
    ResultadoValidacionArchivo ValidarXml(Stream stream, long length, string fileName);
}

public class FileSecurityValidator : IFileSecurityValidator
{
    // Límite de 5 MB = 5 * 1024 * 1024 bytes
    public const long MaxSizeBytes = 5 * 1024 * 1024;

    // Magic Numbers
    // PDF: %PDF- (0x25, 0x50, 0x44, 0x46, 0x2D)
    private static readonly byte[] PdfMagicHeader = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D };

    public ResultadoValidacionArchivo ValidarPdf(Stream stream, long length, string fileName)
    {
        if (length <= 0)
        {
            return new ResultadoValidacionArchivo { EsValido = false, MensajeError = "El archivo PDF está vacío." };
        }

        if (length > MaxSizeBytes)
        {
            return new ResultadoValidacionArchivo
            {
                EsValido = false,
                MensajeError = $"El archivo PDF supera el tamaño máximo permitido de 5 MB ({length / (1024.0 * 1024.0):F2} MB)."
            };
        }

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (ext != ".pdf")
        {
            return new ResultadoValidacionArchivo { EsValido = false, MensajeError = "La extensión del archivo debe ser .pdf" };
        }

        // Validar Magic Bytes (%PDF-)
        if (stream.CanSeek)
            stream.Position = 0;

        byte[] header = new byte[PdfMagicHeader.Length];
        int bytesRead = stream.Read(header, 0, header.Length);

        if (stream.CanSeek)
            stream.Position = 0;

        if (bytesRead < PdfMagicHeader.Length)
        {
            return new ResultadoValidacionArchivo { EsValido = false, MensajeError = "El archivo no contiene una cabecera válida." };
        }

        for (int i = 0; i < PdfMagicHeader.Length; i++)
        {
            if (header[i] != PdfMagicHeader[i])
            {
                return new ResultadoValidacionArchivo
                {
                    EsValido = false,
                    MensajeError = "El archivo no es un documento PDF válido (Magic Number no coincide)."
                };
            }
        }

        return new ResultadoValidacionArchivo
        {
            EsValido = true,
            TipoDetectado = "application/pdf"
        };
    }

    public ResultadoValidacionArchivo ValidarXml(Stream stream, long length, string fileName)
    {
        if (length <= 0)
        {
            return new ResultadoValidacionArchivo { EsValido = false, MensajeError = "El archivo XML está vacío." };
        }

        if (length > MaxSizeBytes)
        {
            return new ResultadoValidacionArchivo
            {
                EsValido = false,
                MensajeError = $"El archivo XML supera el tamaño máximo permitido de 5 MB ({length / (1024.0 * 1024.0):F2} MB)."
            };
        }

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (ext != ".xml")
        {
            return new ResultadoValidacionArchivo { EsValido = false, MensajeError = "La extensión del archivo debe ser .xml" };
        }

        if (stream.CanSeek)
            stream.Position = 0;

        // Leer los primeros 512 bytes para comprobar sintaxis de cabecera XML y evitar archivos binarios disfrazados
        byte[] buffer = new byte[Math.Min(512, (int)length)];
        int bytesRead = stream.Read(buffer, 0, buffer.Length);

        if (stream.CanSeek)
            stream.Position = 0;

        if (bytesRead <= 0)
        {
            return new ResultadoValidacionArchivo { EsValido = false, MensajeError = "No se pudo leer la cabecera del archivo XML." };
        }

        string headerText = Encoding.UTF8.GetString(buffer);

        // Prevenir inyecciones o scripts binarios disfrazados
        bool hasXmlProlog = headerText.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase);
        bool hasCfdiTag = headerText.Contains("<cfdi:Comprobante", StringComparison.OrdinalIgnoreCase) ||
                          headerText.Contains("<Comprobante", StringComparison.OrdinalIgnoreCase);

        if (!hasXmlProlog && !hasCfdiTag)
        {
            return new ResultadoValidacionArchivo
            {
                EsValido = false,
                MensajeError = "El archivo no contiene una estructura XML / CFDI válida en su cabecera."
            };
        }

        return new ResultadoValidacionArchivo
        {
            EsValido = true,
            TipoDetectado = "application/xml"
        };
    }
}
