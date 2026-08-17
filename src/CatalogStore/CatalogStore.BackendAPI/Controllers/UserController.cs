using CatalogStore.BackendAPI.Models.Auth;
using CatalogStore.BackendAPI.Services.User;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CatalogStore.BackendAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly IUserServices _userServices;
        public UserController(IUserServices userServices)
        {
            _userServices = userServices;
        }

        [HttpPost("register")]
        [Authorize(Roles = "Admin,AdminIT")]
        public async Task<IActionResult> Register([FromBody] AuthModels.RegisterRequest request)
        {
            var result = await _userServices.RegisterAsync(request);
            return result.Succeeded ? Ok(new { Message = "Usuario registrado exitosamente." }) : BadRequest(result.Errors);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] AuthModels.LoginRequest request)
        {
            var result = await _userServices.LoginAsync(request);
            return result.Succeeded ? Ok(new { Token = result.Token }) : Unauthorized();
        }
    }
}
