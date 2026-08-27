using System.Text;
using PortalProveedores.Application.Security;
using Xunit;

namespace PortalProveedores.UnitTests;

public class FileSecurityValidatorTests
{
    private readonly FileSecurityValidator _validator = new();

    [Fact]
    public void ValidarPdf_ConCabeceraPdfValida_DebeSerExitoso()
    {
        // %PDF-1.4 dummy stream
        byte[] validPdfBytes = Encoding.ASCII.GetBytes("%PDF-1.4\n%âãÏÓ\n1 0 obj\n<<>>\nendobj\ntrailer\n<<>>\n%%EOF");
        using var stream = new MemoryStream(validPdfBytes);

        var resultado = _validator.ValidarPdf(stream, validPdfBytes.Length, "factura.pdf");

        Assert.True(resultado.EsValido);
        Assert.Equal("application/pdf", resultado.TipoDetectado);
    }

    [Fact]
    public void ValidarPdf_ConArchivoEjecutableDisfrazado_DebeFallar()
    {
        // MZ header (EXE) disguised as .pdf
        byte[] exeBytes = Encoding.ASCII.GetBytes("MZ\x90\x00\x03\x00\x00\x00\x04\x00\x00\x00");
        using var stream = new MemoryStream(exeBytes);

        var resultado = _validator.ValidarPdf(stream, exeBytes.Length, "malware.pdf");

        Assert.False(resultado.EsValido);
        Assert.Contains("Magic Number", resultado.MensajeError);
    }

    [Fact]
    public void ValidarPdf_ExcedeLimite5MB_DebeFallar()
    {
        using var stream = new MemoryStream(new byte[100]);
        long lengthExceeded = 6 * 1024 * 1024; // 6 MB

        var resultado = _validator.ValidarPdf(stream, lengthExceeded, "pesado.pdf");

        Assert.False(resultado.EsValido);
        Assert.Contains("5 MB", resultado.MensajeError);
    }

    [Fact]
    public void ValidarXml_ConEstructuraCfdiValida_DebeSerExitoso()
    {
        string cfdiContent = "<?xml version=\"1.0\" encoding=\"utf-8\"?><cfdi:Comprobante xmlns:cfdi=\"http://www.sat.gob.mx/cfd/4\" Version=\"4.0\"></cfdi:Comprobante>";
        byte[] xmlBytes = Encoding.UTF8.GetBytes(cfdiContent);
        using var stream = new MemoryStream(xmlBytes);

        var resultado = _validator.ValidarXml(stream, xmlBytes.Length, "factura.xml");

        Assert.True(resultado.EsValido);
        Assert.Equal("application/xml", resultado.TipoDetectado);
    }

    [Fact]
    public void ValidarXml_ConScriptDisfrazado_DebeFallar()
    {
        string fakeContent = "<html><script>alert('xss');</script></html>";
        byte[] bytes = Encoding.UTF8.GetBytes(fakeContent);
        using var stream = new MemoryStream(bytes);

        var resultado = _validator.ValidarXml(stream, bytes.Length, "fake.xml");

        Assert.False(resultado.EsValido);
        Assert.Contains("CFDI", resultado.MensajeError);
    }
}
