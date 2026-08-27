using CatalogStore.BackendAPI.DTO.User;
using CatalogStore.BackendAPI.Models.Auth;
using CatalogStore.BackendAPI.Models.EventLogs;
using CatalogStore.BackendAPI.Services.Auth;
using CatalogStore.BackendAPI.Services.EventLogs;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using static CatalogStore.BackendAPI.Models.Auth.AuthModels;

namespace CatalogStore.BackendAPI.Services.User
{
    public class UserServices : IUserServices
    {
        private const string UsersTable = "AspNetUsers";
        private const string UserRolesTable = "AspNetUserRoles";

        private readonly UserManager<Data.ApplicationUser> _userManager;
        private readonly IJwtTokenService _jwtTokenService;
        private readonly IEventlogServices _eventlogServices;

        public UserServices(UserManager<Data.ApplicationUser> userManager, IJwtTokenService jwtTokenService, IEventlogServices eventlogServices)
        {
            _userManager = userManager;
            _jwtTokenService = jwtTokenService;
            _eventlogServices = eventlogServices;
        }

        public async Task<AuthModels.RegisterResult> RegisterAsync(AuthModels.RegisterRequest request)
        {
            Data.ApplicationUser? user = null;
            try
            {
                string temporaryPassword = GenerateTemporaryPassword();
                user = new Data.ApplicationUser
                {
                    FullName = request.FullName,
                    Email = request.Email,
                    UserName = request.UserName,
                    SendNotifications = request.SendNotifications,
                    MustChangePassword = true
                };

                IdentityResult result = await _userManager.CreateAsync(user, temporaryPassword);
                if (!result.Succeeded)
                {
                    await _eventlogServices.LogAsync(
                        typeEvent.RegisterFail,
                        "Administration/Users",
                        UsersTable,
                        request.Email,
                        "No se pudo registrar el usuario.",
                        postData: result.Errors.Select(e => e.Description));
                    return new RegisterResult(false, result.Errors.Select(e => e.Description));
                }

                IdentityResult addRoleResult = await _userManager.AddToRoleAsync(user, request.Role);
                if (!addRoleResult.Succeeded)
                {
                    await _eventlogServices.LogAsync(
                        typeEvent.RegisterFail,
                        "Administration/Users",
                        UserRolesTable,
                        user.Id.ToString(),
                        $"Usuario creado, pero no se pudo asignar el rol '{request.Role}'.",
                        postData: addRoleResult.Errors.Select(e => e.Description));
                    return new RegisterResult(false, addRoleResult.Errors.Select(e => e.Description));
                }

                await _eventlogServices.LogAsync(
                    typeEvent.Register,
                    "Administration/Users",
                    UsersTable,
                    user.Id.ToString(),
                    "Se ha registrado un usuario de manera correcta en el sistema.",
                    postData: new UserDTO
                    {
                        Id = user.Id,
                        FullName = user.FullName ?? string.Empty,
                        Email = user.Email ?? string.Empty,
                        UserName = user.UserName ?? string.Empty,
                        SendNotifications = user.SendNotifications,
                        Roles = new List<string> { request.Role }
                    });

                return new RegisterResult(true, Enumerable.Empty<string>(), temporaryPassword);
            }
            catch (Exception ex)
            {
                await _eventlogServices.LogAsync(
                    typeEvent.RegisterFail,
                    "Administration/Users",
                    UsersTable,
                    user?.Id.ToString() ?? request.Email,
                    "Excepción no controlada al registrar usuario.",
                    stackTrace: ex.ToString());
                return new RegisterResult(false, new[] { ex.Message });
            }
        }

        public async Task<AuthModels.ChangePasswordResult> ChangePasswordAsync(Guid userId, AuthModels.ChangePasswordRequest request)
        {
            var usuario = await _userManager.FindByIdAsync(userId.ToString());
            if (usuario == null)
                return new ChangePasswordResult(false, new[] { "Usuario no encontrado." });

            var result = await _userManager.ChangePasswordAsync(usuario, request.CurrentPassword, request.NewPassword);
            if (!result.Succeeded)
            {
                await _eventlogServices.LogAsync(
                    typeEvent.EditFail,
                    "Administration/Users",
                    UsersTable,
                    userId.ToString(),
                    "No se pudo cambiar la contraseña.",
                    postData: result.Errors.Select(e => e.Description));
                return new ChangePasswordResult(false, result.Errors.Select(e => e.Description));
            }

            usuario.MustChangePassword = false;
            await _userManager.UpdateAsync(usuario);

            await _eventlogServices.LogAsync(
                typeEvent.Edit,
                "Administration/Users",
                UsersTable,
                userId.ToString(),
                "Cambio de contraseña exitoso.");

            return new ChangePasswordResult(true, Enumerable.Empty<string>());
        }

        //login
        public async Task<AuthModels.LoginResult> LoginAsync(AuthModels.LoginRequest request)
        {
            Data.ApplicationUser? user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
            {
                await _eventlogServices.LogAsync(
                    typeEvent.LogInFail,
                    "Administration/Users",
                    UsersTable,
                    request.Email,
                    "Intento de inicio de sesión con un email no registrado.",
                    actorOverride: request.Email);
                return new AuthModels.LoginResult(false, null);
            }

            bool passCheck = await _userManager.CheckPasswordAsync(user, request.Password);
            if (!passCheck)
            {
                await _eventlogServices.LogAsync(
                    typeEvent.LogInFail,
                    "Administration/Users",
                    UsersTable,
                    user.Id.ToString(),
                    "Contraseña incorrecta.",
                    actorOverride: request.Email);
                return new AuthModels.LoginResult(false, null);
            }

            IList<string> roles = await _userManager.GetRolesAsync(user);
            var token = _jwtTokenService.GenerateToken(user, roles);

            await _eventlogServices.LogAsync(
                typeEvent.LogIn,
                "Administration/Users",
                UsersTable,
                user.Id.ToString(),
                "Inicio de sesión exitoso.",
                actorOverride: request.Email);

            return new AuthModels.LoginResult(true, token, user.MustChangePassword);
        }

        public async Task<string?> RefreshTokenAsync(string oldToken)
        {
            var userId = _jwtTokenService.GetUserIdFromExpiredToken(oldToken);
            if (userId == null) return null;

            var user = await _userManager.FindByIdAsync(userId.Value.ToString());
            if (user == null) return null;

            var roles = await _userManager.GetRolesAsync(user);
            return _jwtTokenService.GenerateToken(user, roles);
        }

        public async Task LogoutAsync(Guid userId)
        {
            await _eventlogServices.LogAsync(
                typeEvent.LogOut,
                "Administration/Users",
                UsersTable,
                userId.ToString(),
                "Cierre de sesión.");
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
            {
                await _eventlogServices.LogAsync(
                    typeEvent.EditFail,
                    "Administration/Users",
                    UsersTable,
                    id.ToString(),
                    "No se pudo restablecer la contraseña.",
                    postData: result.Errors.Select(e => e.Description));
                return new ResetPasswordResult(false, result.Errors.Select(e => e.Description));
            }

            usuario.MustChangePassword = true;
            await _userManager.UpdateAsync(usuario);

            await _eventlogServices.LogAsync(
                typeEvent.Edit,
                "Administration/Users",
                UsersTable,
                id.ToString(),
                "Restablecimiento de contraseña por un administrador.");

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
                FullName = user.FullName ?? string.Empty,
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
                    FullName = user.FullName ?? string.Empty,
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

            var roles = await _userManager.GetRolesAsync(usuario);
            var before = new UserDTO
            {
                Id = usuario.Id,
                UserName = usuario.UserName ?? string.Empty,
                Email = usuario.Email ?? string.Empty,
                FullName = usuario.FullName ?? string.Empty,
                SendNotifications = usuario.SendNotifications,
                Roles = roles
            };

            usuario.FullName = request.FullName;
            usuario.Email = request.Email;
            usuario.SendNotifications = request.SendNotifications;

            var result = await _userManager.UpdateAsync(usuario);
            if (!result.Succeeded)
            {
                await _eventlogServices.LogAsync(
                    typeEvent.EditFail,
                    "Administration/Users",
                    UsersTable,
                    id.ToString(),
                    "No se pudo actualizar el usuario.",
                    preData: before,
                    postData: result.Errors.Select(e => e.Description));
                return false;
            }

            var after = new UserDTO
            {
                Id = usuario.Id,
                UserName = usuario.UserName ?? string.Empty,
                Email = usuario.Email ?? string.Empty,
                FullName = usuario.FullName ?? string.Empty,
                SendNotifications = usuario.SendNotifications,
                Roles = roles
            };

            await _eventlogServices.LogAsync(
                typeEvent.Edit,
                "Administration/Users",
                UsersTable,
                id.ToString(),
                "Usuario actualizado.",
                preData: before,
                postData: after);

            return true;
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
                {
                    await _eventlogServices.LogAsync(
                        typeEvent.EditFail,
                        "Administration/Users",
                        UserRolesTable,
                        id.ToString(),
                        "No se pudieron quitar los roles actuales del usuario.",
                        preData: currentRoles,
                        postData: removeResult.Errors.Select(e => e.Description));
                    return false;
                }
            }

            var addResult = await _userManager.AddToRoleAsync(usuario, newRole);
            if (!addResult.Succeeded)
            {
                await _eventlogServices.LogAsync(
                    typeEvent.EditFail,
                    "Administration/Users",
                    UserRolesTable,
                    id.ToString(),
                    $"No se pudo asignar el rol '{newRole}'.",
                    preData: currentRoles,
                    postData: addResult.Errors.Select(e => e.Description));
                return false;
            }

            await _eventlogServices.LogAsync(
                typeEvent.Edit,
                "Administration/Users",
                UserRolesTable,
                id.ToString(),
                $"Rol actualizado a '{newRole}'.",
                preData: currentRoles,
                postData: new List<string> { newRole });

            return true;
        }

        public async Task<bool> DeleteUserAsync(Guid id)
        {
            var usuario = await _userManager.FindByIdAsync(id.ToString());
            if (usuario == null)
                return false;

            var roles = await _userManager.GetRolesAsync(usuario);
            var before = new UserDTO
            {
                Id = usuario.Id,
                UserName = usuario.UserName ?? string.Empty,
                Email = usuario.Email ?? string.Empty,
                FullName = usuario.FullName ?? string.Empty,
                SendNotifications = usuario.SendNotifications,
                Roles = roles
            };

            var result = await _userManager.DeleteAsync(usuario);
            if (!result.Succeeded)
            {
                await _eventlogServices.LogAsync(
                    typeEvent.DeleteFail,
                    "Administration/Users",
                    UsersTable,
                    id.ToString(),
                    "No se pudo eliminar el usuario.",
                    preData: before,
                    postData: result.Errors.Select(e => e.Description));
                return false;
            }

            await _eventlogServices.LogAsync(
                typeEvent.Delete,
                "Administration/Users",
                UsersTable,
                id.ToString(),
                "Usuario eliminado.",
                preData: before);

            return true;
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
