using CatalogStore.BackendAPI.Data;
using CatalogStore.BackendAPI.Models.Auth;
using CatalogStore.BackendAPI.Services.Auth;
using Microsoft.AspNetCore.Identity;
using static CatalogStore.BackendAPI.Models.Auth.AuthModels;

namespace CatalogStore.BackendAPI.Services.User
{
    public class UserServices : IUserServices
    {
        private readonly UserManager<Data.ApplicationUser> _userManager;
        private readonly IJwtTokenService _jwtTokenService;
        public UserServices(UserManager<Data.ApplicationUser> userManager, IJwtTokenService jwtTokenService)
        {
            _userManager = userManager;
            _jwtTokenService = jwtTokenService;
        }
        public async Task<AuthModels.RegisterResult> RegisterAsync(AuthModels.RegisterRequest request)
        {
            Data.ApplicationUser user = new Data.ApplicationUser
            {
                FullName = request.FullName,
                Email = request.Email,
                UserName = request.UserName,
                SendNotifications = request.SendNotifications
            };
            IdentityResult result = await _userManager.CreateAsync(user, request.Password);
            if (result.Succeeded)
            {
                IdentityResult addRoleResult = await _userManager.AddToRoleAsync(user, request.Role);
                return new RegisterResult(true, Enumerable.Empty<string>());
            }
            else
            {
                return new RegisterResult(false, result.Errors.Select(e => e.Description));
            }
        }

        //login
        public async Task<AuthModels.LoginResult> LoginAsync(AuthModels.LoginRequest request)
        {
            Data.ApplicationUser? user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
                return new AuthModels.LoginResult(false, null);


            bool passCheck = await _userManager.CheckPasswordAsync(user, request.Password);
            if (!passCheck)
                return new AuthModels.LoginResult(false, null);
                           
            IList<string> roles = await _userManager.GetRolesAsync(user);
            var token = _jwtTokenService.GenerateToken(user, roles);
            return new AuthModels.LoginResult(true, token);                 
        }
    }
}
