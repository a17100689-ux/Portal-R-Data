using System.Text;
using PortalProveedores.Application.Security;
using Xunit;

namespace PortalProveedores.UnitTests;

public class CryptoAndCfdiTests
{
    private readonly CryptoService _cryptoService = new();
    private readonly CfdiXmlParser _cfdiParser = new();

    [Fact]
    public void CryptoService_HashAndVerify_DebeValidarCorrectamente()
    {
        string rawPassword = "StrongPassword#2026";
        var (hash, salt) = _cryptoService.HashPassword(rawPassword);

        Assert.NotEmpty(hash);
        Assert.NotEmpty(salt);

        bool isValid = _cryptoService.VerifyPassword(rawPassword, hash, salt);
        Assert.True(isValid);

        bool isInvalid = _cryptoService.VerifyPassword("WrongPassword", hash, salt);
        Assert.False(isInvalid);
    }

    [Fact]
    public void CfdiXmlParser_ExtraeDatosCorrectamente()
    {
        string sampleCfdi = @"<?xml version=""1.0"" encoding=""utf-8""?>
<cfdi:Comprobante xmlns:cfdi=""http://www.sat.gob.mx/cfd/4"" xmlns:tfd=""http://www.sat.gob.mx/TimbreFiscalDigital"" Version=""4.0"" Serie=""A"" Folio=""1024"" Fecha=""2026-08-27T10:00:00"" SubTotal=""1000.00"" Total=""1160.00"" Moneda=""MXN"">
    <cfdi:Emisor Rfc=""AAA010101AAA"" Nombre=""PROVEEDOR DEMO SA DE CV"" />
    <cfdi:Receptor Rfc=""RDA200101XYZ"" Nombre=""PORTAL R-DATA SA DE CV"" />
    <cfdi:Complemento>
        <tfd:TimbreFiscalDigital UUID=""550E8400-E29B-41D4-A716-446655440000"" FechaTimbrado=""2026-08-27T10:05:00"" />
    </cfdi:Complemento>
</cfdi:Comprobante>";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(sampleCfdi));
        var resultado = _cfdiParser.ParsearCfdi(stream);

        Assert.Equal("550E8400-E29B-41D4-A716-446655440000", resultado.UUID);
        Assert.Equal("AAA010101AAA", resultado.RFCEmisor);
        Assert.Equal("RDA200101XYZ", resultado.RFCReceptor);
        Assert.Equal(1000.00m, resultado.Subtotal);
        Assert.Equal(1160.00m, resultado.Total);
        Assert.Equal("A", resultado.Serie);
        Assert.Equal("1024", resultado.Folio);
    }
}
