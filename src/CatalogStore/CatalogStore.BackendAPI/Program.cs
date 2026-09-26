using CatalogStore.BackendAPI.Data;
using CatalogStore.BackendAPI.Repository.Client;
using CatalogStore.BackendAPI.Repository.EventLogs;
using CatalogStore.BackendAPI.Repository.Inventory;
using CatalogStore.BackendAPI.Repository.Product;
using CatalogStore.BackendAPI.Repository.ProductImage;
using CatalogStore.BackendAPI.Repository.Status;
using CatalogStore.BackendAPI.Services.Auth;
using CatalogStore.BackendAPI.Services.Client;
using CatalogStore.BackendAPI.Services.Client.Hacienda;
using CatalogStore.BackendAPI.Services.EventLogs;
using CatalogStore.BackendAPI.Services.Inventory;
using CatalogStore.BackendAPI.Services.Product;
using CatalogStore.BackendAPI.Services.Product.CatalogExternal;
using CatalogStore.BackendAPI.Services.ProductImage;
using CatalogStore.BackendAPI.Services.Status;
using CatalogStore.BackendAPI.Services.User;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Configurar la cadena de conexión para SQL Server
builder.Services.AddDbContext<ApplicationDBContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddHttpContextAccessor();
builder.Services.AddControllers();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Ingresá el token así: Bearer {tu token}"
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = new List<string>()
    });
});
builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
{
    options.User.RequireUniqueEmail = true;
})
    .AddEntityFrameworkStores<ApplicationDBContext>()
    .AddDefaultTokenProviders();

builder.Services.AddHttpClient("HaciendaApi", client =>
{
    client.BaseAddress = new Uri("https://api.hacienda.go.cr/");
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddHttpClient("ExternalCatalog", client =>
{
    var baseUrl = builder.Configuration["ExternalCatalog:BaseUrl"] 
        ?? throw new InvalidOperationException("ExternalCatalog:BaseUrl no esta configurado.");
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

//Dependency Injection for Services and Repositories
builder.Services.AddScoped<IStatusRepository, StatusRepository>();
builder.Services.AddScoped<IStatusServices, StatusServices>();
builder.Services.AddScoped<IEventlogRepository, EventlogRepository>();
builder.Services.AddScoped<IEventlogServices, EventlogServices>();
builder.Services.AddScoped<IUserServices, UserServices>();
builder.Services.AddScoped<IHaciendaServices, HaciendaServices>();
builder.Services.AddScoped<ICatalogExternalServices, CatalogExternalServices>();
builder.Services.AddScoped<IProductImportServices, ProductImportServices>();
builder.Services.AddScoped<IClientRepository, ClientRepository>();
builder.Services.AddScoped<IClientServices, ClientServices>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IProductServices, ProductServices>();
builder.Services.AddScoped<IProductImageRepository, ProductImageRepository>();
builder.Services.AddScoped<IProductImageServices, ProductImageServices>();
builder.Services.AddScoped<IInventoryRepository, InventoryRepository>();
builder.Services.AddScoped<IInventoryServices, InventoryServices>();

//JWT Services
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
        };
    });
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDBContext>();
    dbContext.Database.Migrate();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var existingAdmin = await userManager.FindByEmailAsync(builder.Configuration["SeedAdmin:Email"]);
    
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
    if(!await roleManager.RoleExistsAsync("AdminIT"))
    {
        await roleManager.CreateAsync(new ApplicationRole { Name = "AdminIT" });
    }
    if(!await roleManager.RoleExistsAsync("Admin"))
    {
        await roleManager.CreateAsync(new ApplicationRole { Name = "Admin" });
    }
    if(!await roleManager.RoleExistsAsync("Vendedor"))
    {
        await roleManager.CreateAsync(new ApplicationRole { Name = "Vendedor" });
    }

    if (existingAdmin == null)
    {
        var adminUser = new ApplicationUser
        {
            UserName = builder.Configuration["SeedAdmin:UserName"],
            Email = builder.Configuration["SeedAdmin:Email"],
            FullName = builder.Configuration["SeedAdmin:FullName"],
            EmailConfirmed = true
        };

        IdentityResult result = await userManager.CreateAsync(adminUser, builder.Configuration["SeedAdmin:Password"]);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(adminUser, "AdminIT");
        }
    }

}
app.UseHttpsRedirection();
app.UseStaticFiles(); // Habilitar el uso de archivos estáticos como imagenes 
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();