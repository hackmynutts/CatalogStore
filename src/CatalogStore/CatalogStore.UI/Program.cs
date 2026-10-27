using CatalogStore.UI.Controllers;
using CatalogStore.UI.Filters;
using CatalogStore.UI.Handlers;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Threading.RateLimiting;



var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddHttpContextAccessor();
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
// "Olvidé mi contraseña" es público: como mucho 5 envíos cada 15 minutos por IP. El límite va aquí y no en la API
// porque la API ve siempre la IP de la UI, no la del usuario. La API además limita a un correo por usuario cada 5 min.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(AccountController.PasswordFormsPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(15), QueueLimit = 0 }));
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStatusCodePagesWithReExecute("/Home/StatusCode/{0}");
app.UseRouting();
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
