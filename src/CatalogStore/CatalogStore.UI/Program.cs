using CatalogStore.UI.Controllers;
using CatalogStore.UI.Filters;
using CatalogStore.UI.Handlers;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Threading.RateLimiting;


QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();
builder.Services.AddControllersWithViews(options => options.Filters.Add<SessionExpiredFilter>());
builder.Services.AddTransient<JwtDelegatingHandler>();
builder.Services.AddHttpClient("BackendApi", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["BackendApi:BaseUrl"]!);
}).AddHttpMessageHandler<JwtDelegatingHandler>();
// Sin JwtDelegatingHandler a propósito — la usa el propio handler para refrescar el token,
// si tuviera el handler encima se auto-intercepta y entra en loop infinito.
builder.Services.AddHttpClient("BackendApiAuth", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["BackendApi:BaseUrl"]!);
});
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(1);
        options.SlidingExpiration = true;
    });
// Límites de peticiones (ver Handlers/UiRateLimiting.cs): inicio de sesión y formularios públicos por IP, PDFs y correos por persona.
builder.Services.AddUiRateLimiting(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Una sola dirección para el sitio: www.samdesigncr.com redirige (301) a samdesigncr.com. Así Google no ve dos sitios iguales.
// Solo si Seo:SiteUrl está configurado (producción).
if (Uri.TryCreate((builder.Configuration["Seo:SiteUrl"] ?? string.Empty).Trim().TrimEnd('/'), UriKind.Absolute, out var canonicalSite))
{
    app.Use(async (context, next) =>
    {
        if (context.Request.Host.Host.Equals("www." + canonicalSite.Host, StringComparison.OrdinalIgnoreCase))
        {
            context.Response.Redirect($"{canonicalSite.GetLeftPart(UriPartial.Authority)}{context.Request.Path}{context.Request.QueryString}", permanent: true);
            return;
        }
        await next();
    });
}

app.UseHttpsRedirection();
app.UseStatusCodePagesWithReExecute("/Home/StatusCode/{0}");
app.UseRouting();
// El limitador va DESPUÉS de autenticar: así cuenta por persona (no solo por IP).
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
