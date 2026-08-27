namespace PortalProveedores.Web.Middlewares;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var correlationId = Guid.NewGuid().ToString("N");
            _logger.LogError(ex, "Excepción no controlada detectada en la petición {Path}. CorrelationId: {CorrelationId}", context.Request.Path, correlationId);

            if (!context.Response.HasStarted)
            {
                context.Response.Clear();
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;

                if (context.Request.Headers["Accept"].ToString().Contains("application/json") ||
                    context.Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsJsonAsync(new
                    {
                        exitoso = false,
                        mensaje = "Ocurrió un error inesperado al procesar su solicitud. Por favor intente más tarde o contacte a soporte.",
                        ticket = correlationId
                    });
                }
                else
                {
                    context.Response.Redirect($"/Home/Error?ticket={correlationId}");
                }
            }
        }
    }
}

public static class GlobalExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<GlobalExceptionMiddleware>();
    }
}
