using CatalogStore.BackendAPI.DTO.User;
using CatalogStore.BackendAPI.Models.Auth;
using CatalogStore.BackendAPI.Services.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
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
            string temporaryPassword = GenerateTemporaryPassword();

            Data.ApplicationUser user = new Data.ApplicationUser
            {
                FullName = request.FullName,
                Email = request.Email,
                UserName = request.UserName,
                SendNotifications = request.SendNotifications,
                MustChangePassword = true
            };
            IdentityResult result = await _userManager.CreateAsync(user, temporaryPassword);
            if (result.Succeeded)
            {
                IdentityResult addRoleResult = await _userManager.AddToRoleAsync(user, request.Role);
                if (addRoleResult.Succeeded)
                    return new RegisterResult(true, Enumerable.Empty<string>(), temporaryPassword);
                return new RegisterResult(false, addRoleResult.Errors.Select(e => e.Description));
            }
            else
            {
                return new RegisterResult(false, result.Errors.Select(e => e.Description));
            }
        }
        public async Task<AuthModels.ChangePasswordResult> ChangePasswordAsync(Guid userId, AuthModels.ChangePasswordRequest request)
        {
            var usuario = await _userManager.FindByIdAsync(userId.ToString());
            if (usuario == null)
                return new ChangePasswordResult(false, new[] { "Usuario no encontrado." });

            var result = await _userManager.ChangePasswordAsync(usuario, request.CurrentPassword, request.NewPassword);
            if (!result.Succeeded)
                return new ChangePasswordResult(false, result.Errors.Select(e => e.Description));

            usuario.MustChangePassword = false;
            await _userManager.UpdateAsync(usuario);

            return new ChangePasswordResult(true, Enumerable.Empty<string>());
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
            return new AuthModels.LoginResult(true, token, user.MustChangePassword);                 
        }
        public async Task<AuthModels.ResetPasswordResult> ResetPasswordAsync(Guid id)
        {
            var usuario = await _userManager.FindByIdAsync(id.ToString());
            if (usuario == null)
                return new ResetPasswordResult(false, new[] { "Usuario no encontrado." });

            string temporaryPassword = GenerateTemporaryPassword();
            var token = await _userManager.GeneratePasswordResetTokenAsync(usuario);
            var result = await _userManager.ResetPasswordAsync(usuario, token, temporaryPassword);

            if (!result.Succeeded)
                return new ResetPasswordResult(false, result.Errors.Select(e => e.Description));

            usuario.MustChangePassword = true;
            await _userManager.UpdateAsync(usuario);

            return new ResetPasswordResult(true, Enumerable.Empty<string>(), temporaryPassword);
        }

        //get
        public async Task<UserDTO?> GetUserAsync(Guid id)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null)
                return null;

            var roles = await _userManager.GetRolesAsync(user);

            return new UserDTO
            {
                Id = user.Id,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                FullName = user.FullName,
                SendNotifications = user.SendNotifications,
                Roles = roles
            };
        }

        //getall
        public async Task<List<UserDTO>> GetAllUsersAsync()
        {
            var users = await _userManager.Users.ToListAsync();
            var result = new List<UserDTO>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                result.Add(new UserDTO
                {
                    Id = user.Id,
                    UserName = user.UserName ?? string.Empty,
                    Email = user.Email ?? string.Empty,
                    FullName = user.FullName,
                    SendNotifications = user.SendNotifications,
                    Roles = roles
                });
            }

            return result;
        }

        //edit user
        public async Task<bool> UpdateUserAsync(Guid id, UpdateUserDTO request)
        {
            var usuario = await _userManager.FindByIdAsync(id.ToString());
            if (usuario == null)
                return false;

            usuario.FullName = request.FullName;
            usuario.Email = request.Email;
            usuario.SendNotifications = request.SendNotifications;

            var result = await _userManager.UpdateAsync(usuario);
            return result.Succeeded;
        }

        public async Task<bool> ChangeRoleAsync(Guid id, string newRole)
        {
            var usuario = await _userManager.FindByIdAsync(id.ToString());
            if (usuario == null)
                return false;

            var currentRoles = await _userManager.GetRolesAsync(usuario);
            if (currentRoles.Any())
            {
                var removeResult = await _userManager.RemoveFromRolesAsync(usuario, currentRoles);
                if (!removeResult.Succeeded)
                    return false;
            }

            var addResult = await _userManager.AddToRoleAsync(usuario, newRole);
            return addResult.Succeeded;
        }

        public async Task<bool> DeleteUserAsync(Guid id)
        {
            var usuario = await _userManager.FindByIdAsync(id.ToString());
            if (usuario == null)
                return false;

            var result = await _userManager.DeleteAsync(usuario);
            return result.Succeeded;
        }
        private static string GenerateTemporaryPassword()
        {
            const string uppercase = "ABCDEFGHJKLMNPQRSTUVWXYZ";
            const string lowercase = "abcdefghijkmnpqrstuvwxyz";
            const string digits = "23456789";
            const string symbols = "!@#$%?";
            string all = uppercase + lowercase + digits + symbols;

            var chars = new char[12];
            chars[0] = PickRandomChar(uppercase);
            chars[1] = PickRandomChar(lowercase);
            chars[2] = PickRandomChar(digits);
            chars[3] = PickRandomChar(symbols);

            for (int i = 4; i < chars.Length; i++)
                chars[i] = PickRandomChar(all);

            for (int i = chars.Length - 1; i > 0; i--)
            {
                int j = RandomNumberGenerator.GetInt32(i + 1);
                (chars[i], chars[j]) = (chars[j], chars[i]);
            }

            return new string(chars);
        }

        private static char PickRandomChar(string pool)
        {
            int index = RandomNumberGenerator.GetInt32(pool.Length);
            return pool[index];
        }
    }
}
