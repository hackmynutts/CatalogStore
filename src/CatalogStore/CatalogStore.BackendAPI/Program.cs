using CatalogStore.BackendAPI.Data;
using CatalogStore.BackendAPI.Repository.Status;
using CatalogStore.BackendAPI.Services;
using CatalogStore.BackendAPI.Services.Status;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Configurar la cadena de conexión para SQL Server
builder.Services.AddDbContext<ApplicationDBContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddControllers();
builder.Services.AddSwaggerGen();
builder.Services.AddIdentity<ApplicationUser, ApplicationRole>()
    .AddEntityFrameworkStores<ApplicationDBContext>();

builder.Services.AddScoped<IStatusRepository, StatusRepository>();
builder.Services.AddScoped<IStatusServices, StatusServices>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDBContext>();
    dbContext.Database.Migrate();

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
    if(!await roleManager.RoleExistsAsync("AdminIT"))
    {
        await roleManager.CreateAsync(new ApplicationRole { Name = "AdminIT" });
    }
    if(!await roleManager.RoleExistsAsync("Admin"))
    {
        await roleManager.CreateAsync(new ApplicationRole { Name = "Admin" });
    }
}
UserServices.MapToEndpoint(app);

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();