using CatalogStore.BackendAPI.DTO.User;
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
            return result.Succeeded ? Ok(new { Message = "Usuario registrado exitosamente.", TemporaryPassword = result.TemporaryPassword })
                                    : BadRequest(result.Errors);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] AuthModels.LoginRequest request)
        {
            var result = await _userServices.LoginAsync(request);
            return result.Succeeded
                                    ? Ok(new { Token = result.Token, MustChangePassword = result.MustChangePassword })
                                    : Unauthorized();
        }

        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] AuthModels.RefreshTokenRequest request)
        {
            var newToken = await _userServices.RefreshTokenAsync(request.Token);
            if (newToken == null) return Unauthorized();
            return Ok(new { Token = newToken });
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            var userIdClaim = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
            if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
                return Unauthorized();

            await _userServices.LogoutAsync(userId);
            return Ok();
        }

        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] AuthModels.ChangePasswordRequest request)
        {
            var userIdClaim = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
            if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
                return Unauthorized();

            var result = await _userServices.ChangePasswordAsync(userId, request);
            return result.Succeeded ? Ok() : BadRequest(result.Errors);
        }

        [HttpPost("{id}/reset-password")]
        [Authorize(Roles = "Admin,AdminIT")]
        public async Task<IActionResult> ResetPassword(Guid id)
        {
            var result = await _userServices.ResetPasswordAsync(id);
            return result.Succeeded
                ? Ok(new { TemporaryPassword = result.TemporaryPassword })
                : BadRequest(result.Errors);
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetMe()
        {
            var userIdClaim = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
            if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
                return Unauthorized();

            var user = await _userServices.GetUserAsync(userId);
            if (user == null) return NotFound();
            return Ok(user);
        }

        [HttpGet]
        [Authorize(Roles = "Admin,AdminIT")]
        public async Task<IActionResult> GetAll()
        {
            var users = await _userServices.GetAllUsersAsync();
            return Ok(users);
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "Admin,AdminIT")]
        public async Task<IActionResult> Get(Guid id)
        {
            var user = await _userServices.GetUserAsync(id);
            if (user == null) return NotFound();
            return Ok(user);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin,AdminIT")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserDTO request)
        {
            var updated = await _userServices.UpdateUserAsync(id, request);
            if (!updated) return NotFound();
            return Ok();
        }

        [HttpPut("{id}/role")]
        [Authorize(Roles = "Admin,AdminIT")]
        public async Task<IActionResult> ChangeRole(Guid id, [FromBody] string newRole)
        {
            var updated = await _userServices.ChangeRoleAsync(id, newRole);
            if (!updated) return NotFound();
            return Ok();
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin,AdminIT")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var deleted = await _userServices.DeleteUserAsync(id);
            if (!deleted) return NotFound();
            return Ok();
        }
    }
}
