namespace PortalProveedores.Web.Middlewares;

public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // 1. Remover cabeceras de servidor que revelan tecnología o infraestructura
        context.Response.Headers.Remove("Server");
        context.Response.Headers.Remove("X-Powered-By");
        context.Response.Headers.Remove("X-AspNet-Version");
        context.Response.Headers.Remove("X-AspNetMvc-Version");

        // 2. Prevenir ataques de MIME-Sniffing
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";

        // 3. Prevenir Clickjacking mediante iframe
        context.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";

        // 4. Forzar política de Referrer segura
        context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        // 5. Protección XSS en navegadores antiguos
        context.Response.Headers["X-XSS-Protection"] = "1; mode=block";

        // 6. Política de permisos de funciones del navegador
        context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

        // 7. Content Security Policy básica
        context.Response.Headers["Content-Security-Policy"] = 
            "default-src 'self'; " +
            "script-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; " +
            "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; " +
            "font-src 'self' https://cdn.jsdelivr.net; " +
            "img-src 'self' data:; " +
            "object-src 'none'; " +
            "frame-ancestors 'self';";

        await _next(context);
    }
}

public static class SecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SecurityHeadersMiddleware>();
    }
}
