using CatalogStore.BackendAPI.Data;
using Microsoft.AspNetCore.Identity;

namespace CatalogStore.BackendAPI.Services
{
    public class UserServices
    {
        public record Request(string UserName, string Password, string FullName, string Email, bool SendNotifications = false);
        public static void MapToEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPost("register", async (Request request, UserManager<ApplicationUser> userManager) =>
            {
                var user = new ApplicationUser
                {
                    UserName = request.UserName,
                    Email = request.Email,
                    FullName = request.FullName,
                    SendNotifications = request.SendNotifications
                };
                IdentityResult result = await userManager.CreateAsync(user, request.Password);
                if (result.Succeeded)
                {
                    return Results.Ok(new { Message = "User registered successfully." });
                }
                else
                {
                    return Results.BadRequest(result.Errors);
                }
            });
        }
    }
}
