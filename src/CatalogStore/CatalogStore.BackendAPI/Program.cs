using CatalogStore.BackendAPI.Data;
using CatalogStore.BackendAPI.Repository.Status;
using CatalogStore.BackendAPI.Services.Auth;
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
    .AddEntityFrameworkStores<ApplicationDBContext>();
//Dependency Injection for Services and Repositories
builder.Services.AddScoped<IStatusRepository, StatusRepository>();
builder.Services.AddScoped<IStatusServices, StatusServices>();
builder.Services.AddScoped<IUserServices, UserServices>();


//JWT Services
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
    {
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

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();