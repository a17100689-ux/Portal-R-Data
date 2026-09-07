using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.Features;
using PortalProveedores.Application.Interfaces;
using PortalProveedores.Application.Security;
using PortalProveedores.Application.Services;
using PortalProveedores.Infrastructure.Data;
using PortalProveedores.Infrastructure.Storage;
using PortalProveedores.Web.Middlewares;
using PortalProveedores.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Inyección de Dependencias
builder.Services.AddSingleton<IFileSecurityValidator, FileSecurityValidator>();
builder.Services.AddSingleton<ICfdiXmlParser, CfdiXmlParser>();
builder.Services.AddSingleton<ICryptoService, CryptoService>();
builder.Services.AddSingleton<IStorageService, SecureFileStorageService>();

builder.Services.AddScoped<IFacturaRepository, SqlFacturaRepository>();
builder.Services.AddScoped<IProveedorRepository, SqlProveedorRepository>();
builder.Services.AddScoped<IUsuarioRepository, SqlUsuarioRepository>();
builder.Services.AddScoped<ISyncRepository, SqlSyncRepository>();

builder.Services.AddScoped<IFacturaService, FacturaService>();
builder.Services.AddScoped<IProveedorService, ProveedorService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ISyncService, SyncService>();

// Sincronización continua en segundo plano (Background Worker Service)
builder.Services.AddHostedService<PortalSyncBackgroundService>();

// 2. Configuración de Controladores y Vistas con Protección Antiforgery y Recarga en Tiempo Real
var mvcBuilder = builder.Services.AddControllersWithViews(options =>
{
    // Aplicar validación de token Antiforgery global o por atributos
});

if (builder.Environment.IsDevelopment())
{
    mvcBuilder.AddRazorRuntimeCompilation();
}

// Configuración de Antiforgery con Cookies Seguras
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "__Host-Portal-Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.HeaderName = "X-CSRF-TOKEN";
});

// 3. Autenticación y Cookies Seguras (HttpOnly, Secure, SameSite=Strict)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "__Host-Portal-Session";
        options.Cookie.HttpOnly = true; // Previene acceso vía JavaScript / XSS
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // Forzar HTTPS exclusivamente
        options.Cookie.SameSite = SameSiteMode.Strict; // Mitigación de CSRF
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.SlidingExpiration = true;
        options.LoginPath = "/Auth/Login";
        options.LogoutPath = "/Auth/Logout";
        options.AccessDeniedPath = "/Auth/AccessDenied";
    });

// 4. Límite de tamaño de subida (5 MB máximo para prevenir ataques de denegación de servicio DoS)
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 5 * 1024 * 1024; // 5 MB
});

// Forzar HSTS y Redirección HTTPS
builder.Services.AddHsts(options =>
{
    options.Preload = true;
    options.IncludeSubDomains = true;
    options.MaxAge = TimeSpan.FromDays(365);
});

var app = builder.Build();

// 5. Pipeline HTTP de Seguridad
app.UseSecurityHeaders(); // Cabeceras de seguridad y remoción de Server / X-Powered-By
app.UseGlobalExceptionHandler(); // Manejo global de excepciones sin exponer stack traces

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
