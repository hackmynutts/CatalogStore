using Microsoft.AspNetCore.RateLimiting;
using CatalogStore.BackendAPI.Services.Security;
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

        private string CurrentUser => User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.UniqueName)?.Value ?? "Sistema";

        // Respuesta común de los enlaces de contraseña. DeliveryFailed: la acción se hizo pero el enlace no salió.
        private static object LinkResponse(string message, AuthModels.LinkDelivery? delivery) => new
        {
            Message = message,
            delivery?.EmailSentTo,
            delivery?.ManualLink,
            DeliveryFailed = delivery is null
        };

        [HttpPost("register")]
        [EnableRateLimiting(RateLimitPolicies.Sensitive)]
        [Authorize(Roles = "Admin,AdminIT")]
        public async Task<IActionResult> Register([FromBody] AuthModels.RegisterRequest request)
        {
            if (request.Role == "AdminIT" && !User.IsInRole("AdminIT"))
                return BadRequest(new { message = "Solo un AdminIT puede crear otro AdminIT." });
            var result = await _userServices.RegisterAsync(request, CurrentUser);
            return result.Succeeded ? Ok(LinkResponse("Usuario registrado exitosamente.", result.Delivery))
                                    : BadRequest(new { Message = string.Join(" ", result.Errors) });
        }

        [HttpPost("login")]
        [EnableRateLimiting(RateLimitPolicies.Anonymous)]
        public async Task<IActionResult> Login([FromBody] AuthModels.LoginRequest request)
        {
            var result = await _userServices.LoginAsync(request);
            if (result.Throttled)
            {
                Response.Headers.RetryAfter = ((int)Math.Ceiling(result.RetryAfter.TotalSeconds)).ToString();
                return StatusCode(StatusCodes.Status429TooManyRequests, new { message = "Demasiados intentos fallidos. Esperá unos minutos e intentá de nuevo." });
            }
            if (result.Inactive)
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Tu usuario está inactivo. Hablá con un administrador." });
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
            return result.Succeeded ? Ok() : BadRequest(new { Message = string.Join(" ", result.Errors) });
        }

        [HttpPost("{id}/reset-password")]
        [EnableRateLimiting(RateLimitPolicies.Sensitive)]
        [Authorize(Roles = "Admin,AdminIT")]
        public async Task<IActionResult> ResetPassword(Guid id)
        {
            var result = await _userServices.ResetPasswordAsync(id, CurrentUser);
            return result.Succeeded
                ? Ok(LinkResponse("Enlace para restablecer la contraseña generado.", result.Delivery))
                : BadRequest(new { Message = string.Join(" ", result.Errors) });
        }

        [HttpPost("{id}/resend-invitation")]
        [EnableRateLimiting(RateLimitPolicies.Sensitive)]
        [Authorize(Roles = "Admin,AdminIT")]
        public async Task<IActionResult> ResendInvitation(Guid id)
        {
            var result = await _userServices.ResendInvitationAsync(id, CurrentUser);
            return result.Succeeded
                ? Ok(LinkResponse("Invitación reenviada.", result.Delivery))
                : BadRequest(new { Message = string.Join(" ", result.Errors) });
        }

        // Público. Misma respuesta exista o no el correo: no sirve para averiguar quién tiene cuenta.
        [HttpPost("forgot-password")]
        [EnableRateLimiting(RateLimitPolicies.Anonymous)]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPassword([FromBody] AuthModels.ForgotPasswordRequest request)
        {
            await _userServices.ForgotPasswordAsync(request.Email);
            return Ok(new { Message = "Si el correo está registrado, te enviamos un enlace para restablecer la contraseña." });
        }

        // Público. La UI lo llama antes de mostrar el formulario del enlace.
        [HttpPost("password-link/check")]
        [EnableRateLimiting(RateLimitPolicies.Anonymous)]
        [AllowAnonymous]
        public async Task<IActionResult> CheckPasswordLink([FromBody] AuthModels.PasswordLinkRequest request)
        {
            return Ok(await _userServices.CheckPasswordLinkAsync(request));
        }

        // Público. Crea la contraseña con el enlace de la invitación o del reset.
        [HttpPost("password-link")]
        [EnableRateLimiting(RateLimitPolicies.Anonymous)]
        [AllowAnonymous]
        public async Task<IActionResult> SetPasswordFromLink([FromBody] AuthModels.SetPasswordRequest request)
        {
            var result = await _userServices.SetPasswordFromLinkAsync(request);
            return result.Succeeded ? Ok() : BadRequest(new { Message = string.Join(" ", result.Errors) });
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

        private static readonly string[] AssignableRoles = { "AdminIT", "Admin", "Bodega", "Vendedor", "Cliente" };

        private Guid CurrentUserId => Guid.TryParse(User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value, out var id) ? id : Guid.Empty;

        private IActionResult ToResponse(UserActionResult result) =>
            result.Succeeded ? Ok()
            : result.NotFound ? NotFound()
            : BadRequest(new { message = result.Error });

        // PUT api/User/5/role — cuerpo: "Bodega". Reglas (propio usuario, AdminIT, último AdminIT) en el servicio.
        [HttpPut("{id}/role")]
        [EnableRateLimiting(RateLimitPolicies.Sensitive)]
        [Authorize(Roles = "Admin,AdminIT")]
        public async Task<IActionResult> ChangeRole(Guid id, [FromBody] string newRole)
        {
            if (!AssignableRoles.Contains(newRole))
                return BadRequest(new { message = "Ese rol no existe." });
            return ToResponse(await _userServices.ChangeRoleAsync(id, newRole, CurrentUserId));
        }

        // PUT api/User/5/active — cuerpo: true o false. Inactivo = no inicia sesión ni usa el sistema (sin borrar nada).
        [HttpPut("{id}/active")]
        [EnableRateLimiting(RateLimitPolicies.Sensitive)]
        [Authorize(Roles = "Admin,AdminIT")]
        public async Task<IActionResult> SetActive(Guid id, [FromBody] bool active) =>
            ToResponse(await _userServices.SetActiveAsync(id, active, CurrentUserId));

        [HttpDelete("{id}")]
        [EnableRateLimiting(RateLimitPolicies.Sensitive)]
        [Authorize(Roles = "Admin,AdminIT")]
        public async Task<IActionResult> Delete(Guid id) =>
            ToResponse(await _userServices.DeleteUserAsync(id, CurrentUserId));
    }
}
