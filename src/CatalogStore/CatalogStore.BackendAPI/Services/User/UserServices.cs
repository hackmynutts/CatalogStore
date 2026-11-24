using CatalogStore.BackendAPI.DTO.User;
using CatalogStore.BackendAPI.Models.Auth;
using CatalogStore.BackendAPI.Models.EventLogs;
using CatalogStore.BackendAPI.Services.Auth;
using CatalogStore.BackendAPI.Services.Email;
using CatalogStore.BackendAPI.Services.EventLogs;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using System.Text;
using static CatalogStore.BackendAPI.Models.Auth.AuthModels;

namespace CatalogStore.BackendAPI.Services.User
{
    public class UserServices : IUserServices
    {
        private const string UsersTable = "AspNetUsers";
        private const string UserRolesTable = "AspNetUserRoles";

        // "Olvidé mi contraseña": como máximo un correo por usuario en esta ventana (evita llenarle el buzón).
        private static readonly TimeSpan ForgotPasswordThrottle = TimeSpan.FromMinutes(5);
        private const string InvalidInviteMessage = "La invitación no es válida, ya se usó o venció. Pedile a un administrador que te la reenvíe.";
        private const string InvalidResetMessage = "El enlace no es válido, ya se usó o venció. Pedí uno nuevo.";

        private readonly UserManager<Data.ApplicationUser> _userManager;
        private readonly IJwtTokenService _jwtTokenService;
        private readonly IEventlogServices _eventlogServices;
        private readonly IEmailServices _emailServices;
        private readonly TimeProvider _time;
        private readonly ILogger<UserServices> _logger;
        private readonly Microsoft.Extensions.Caching.Memory.IMemoryCache _cache;
        private readonly Security.LoginThrottle _loginThrottle;

        public UserServices(UserManager<Data.ApplicationUser> userManager, IJwtTokenService jwtTokenService, IEventlogServices eventlogServices,
            IEmailServices emailServices, TimeProvider time, ILogger<UserServices> logger,
            Microsoft.Extensions.Caching.Memory.IMemoryCache cache, Security.LoginThrottle loginThrottle)
        {
            _userManager = userManager;
            _jwtTokenService = jwtTokenService;
            _eventlogServices = eventlogServices;
            _emailServices = emailServices;
            _time = time;
            _logger = logger;
            _cache = cache;
            _loginThrottle = loginThrottle;
        }

        public async Task<AuthModels.RegisterResult> RegisterAsync(AuthModels.RegisterRequest request, string actor)
        {
            Data.ApplicationUser? user = null;
            try
            {
                user = new Data.ApplicationUser
                {
                    FullName = request.FullName?.Trim() ?? string.Empty,
                    Email = request.Email?.Trim(),
                    UserName = request.UserName?.Trim(),
                    SendNotifications = request.SendNotifications,
                    MustChangePassword = false
                };

                // Sin contraseña: no puede iniciar sesión hasta crearla con el enlace de la invitación.
                IdentityResult result = await _userManager.CreateAsync(user);
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

            // El usuario ya existe: si la invitación falla no se deshace, se reenvía desde la lista de usuarios.
            try
            {
                var delivery = await DeliverLinkAsync(user, PasswordLinks.ModeInvite, actor, requestedByAdmin: true);
                return new RegisterResult(true, Enumerable.Empty<string>(), delivery);
            }
            catch (Exception ex)
            {
                await _eventlogServices.LogAsync(
                    typeEvent.Error,
                    "Administration/Users",
                    UsersTable,
                    user.Id.ToString(),
                    "Usuario creado, pero no se pudo enviar la invitación.",
                    stackTrace: ex.ToString());
                return new RegisterResult(true, Enumerable.Empty<string>());
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

            await QueuePasswordChangedNoticeAsync(usuario);
            return new ChangePasswordResult(true, Enumerable.Empty<string>());
        }

        //login
        public async Task<AuthModels.LoginResult> LoginAsync(AuthModels.LoginRequest request)
        {
            // Demasiados intentos fallidos con este correo: pausa (no se mira ni la contraseña).
            if (_loginThrottle.IsBlocked(request.Email, out var retryAfter))
                return new AuthModels.LoginResult(false, null, Throttled: true, RetryAfter: retryAfter);

            Data.ApplicationUser? user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
            {
                _loginThrottle.RecordFailure(request.Email);
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
                _loginThrottle.RecordFailure(request.Email);
                await _eventlogServices.LogAsync(
                    typeEvent.LogInFail,
                    "Administration/Users",
                    UsersTable,
                    user.Id.ToString(),
                    "Contraseña incorrecta.",
                    actorOverride: request.Email);
                return new AuthModels.LoginResult(false, null);
            }

            // Contraseña correcta pero el administrador lo inactivó: no entra. Recién aquí se dice, para no revelar quién tiene cuenta.
            if (Security.UserSessionGuard.IsInactive(user))
            {
                await _eventlogServices.LogAsync(
                    typeEvent.LogInFail,
                    "Administration/Users",
                    UsersTable,
                    user.Id.ToString(),
                    "Intento de inicio de sesión de un usuario inactivo.",
                    actorOverride: request.Email);
                return new AuthModels.LoginResult(false, null, Inactive: true);
            }

            _loginThrottle.Reset(request.Email);
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
            if (user == null || Security.UserSessionGuard.IsInactive(user)) return null;

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

        // La contraseña actual sigue sirviendo hasta que el usuario use el enlace.
        public async Task<AuthModels.ResetPasswordResult> ResetPasswordAsync(Guid id, string actor)
        {
            var usuario = await _userManager.FindByIdAsync(id.ToString());
            if (usuario == null)
                return new ResetPasswordResult(false, new[] { "Usuario no encontrado." });
            if (string.IsNullOrWhiteSpace(usuario.Email))
                return new ResetPasswordResult(false, new[] { "El usuario no tiene correo registrado." });

            var delivery = await DeliverLinkAsync(usuario, PasswordLinks.ModeReset, actor, requestedByAdmin: true);

            await _eventlogServices.LogAsync(
                typeEvent.Edit,
                "Administration/Users",
                UsersTable,
                id.ToString(),
                delivery.EmailSentTo is null
                    ? "Restablecimiento de contraseña por un administrador: enlace entregado al administrador (correo deshabilitado)."
                    : "Restablecimiento de contraseña por un administrador: enlace enviado por correo.");

            return new ResetPasswordResult(true, Enumerable.Empty<string>(), delivery);
        }

        public async Task<AuthModels.ResetPasswordResult> ResendInvitationAsync(Guid id, string actor)
        {
            var usuario = await _userManager.FindByIdAsync(id.ToString());
            if (usuario == null)
                return new ResetPasswordResult(false, new[] { "Usuario no encontrado." });
            if (usuario.PasswordHash != null)
                return new ResetPasswordResult(false, new[] { "Este usuario ya creó su contraseña. Usá \"Restablecer contraseña\"." });
            if (string.IsNullOrWhiteSpace(usuario.Email))
                return new ResetPasswordResult(false, new[] { "El usuario no tiene correo registrado." });

            // Nuevo SecurityStamp: las invitaciones anteriores dejan de servir y solo vale la que se envía ahora.
            await _userManager.UpdateSecurityStampAsync(usuario);
            var delivery = await DeliverLinkAsync(usuario, PasswordLinks.ModeInvite, actor, requestedByAdmin: true);

            await _eventlogServices.LogAsync(
                typeEvent.Edit,
                "Administration/Users",
                UsersTable,
                id.ToString(),
                "Invitación reenviada.");

            return new ResetPasswordResult(true, Enumerable.Empty<string>(), delivery);
        }

        public async Task ForgotPasswordAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return;
            email = email.Trim();

            // Nunca lanza ni cambia la respuesta: el controller contesta lo mismo exista o no el correo.
            try
            {
                var usuario = await _userManager.FindByEmailAsync(email);
                if (usuario == null)
                {
                    await _eventlogServices.LogAsync(
                        typeEvent.EditFail,
                        "Administration/Users",
                        UsersTable,
                        email,
                        "Solicitud de restablecimiento de contraseña para un correo no registrado.",
                        actorOverride: email);
                    return;
                }

                if (!_emailServices.Enabled)
                {
                    _logger.LogWarning("Olvidé mi contraseña de {UserId}: el correo está deshabilitado (Email:Enabled=false), no se envió nada.", usuario.Id);
                    return;
                }

                if (await _emailServices.WasQueuedRecentlyAsync(EmailKinds.PasswordReset, UsersTable, usuario.Id.ToString(), ForgotPasswordThrottle))
                    return;

                await DeliverLinkAsync(usuario, PasswordLinks.ModeReset, usuario.UserName ?? email, requestedByAdmin: false);

                await _eventlogServices.LogAsync(
                    typeEvent.Edit,
                    "Administration/Users",
                    UsersTable,
                    usuario.Id.ToString(),
                    "Se envió un enlace para restablecer la contraseña (olvidé mi contraseña).",
                    actorOverride: email);
            }
            catch (Exception ex)
            {
                await _eventlogServices.LogAsync(
                    typeEvent.Error,
                    "Administration/Users",
                    UsersTable,
                    email,
                    "Excepción no controlada en olvidé mi contraseña.",
                    stackTrace: ex.ToString(),
                    actorOverride: email);
            }
        }

        public async Task<AuthModels.PasswordLinkCheckResult> CheckPasswordLinkAsync(AuthModels.PasswordLinkRequest request)
        {
            var (usuario, token) = await ResolveLinkAsync(request.UserId, request.Token, request.Mode);
            bool valid = usuario != null && token != null && await IsLinkTokenValidAsync(usuario, request.Mode, token);
            if (!valid)
                return new PasswordLinkCheckResult(false, Message: InvalidLinkMessage(request.Mode));

            return new PasswordLinkCheckResult(true, usuario!.FullName, usuario.UserName);
        }

        public async Task<AuthModels.ChangePasswordResult> SetPasswordFromLinkAsync(AuthModels.SetPasswordRequest request)
        {
            var (usuario, token) = await ResolveLinkAsync(request.UserId, request.Token, request.Mode);
            if (usuario == null || token == null)
                return new ChangePasswordResult(false, new[] { InvalidLinkMessage(request.Mode) });

            IdentityResult result;
            if (request.Mode == PasswordLinks.ModeInvite)
            {
                if (!await IsLinkTokenValidAsync(usuario, request.Mode, token))
                    return new ChangePasswordResult(false, new[] { InvalidInviteMessage });
                // Cambia el SecurityStamp: la invitación queda usada.
                result = await _userManager.AddPasswordAsync(usuario, request.NewPassword);
            }
            else
            {
                // ResetPasswordAsync valida el token y cambia el SecurityStamp: el enlace queda usado.
                result = await _userManager.ResetPasswordAsync(usuario, token, request.NewPassword);
                if (!result.Succeeded && result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.InvalidToken)))
                    return new ChangePasswordResult(false, new[] { InvalidResetMessage });
            }

            if (!result.Succeeded)
            {
                await _eventlogServices.LogAsync(
                    typeEvent.EditFail,
                    "Administration/Users",
                    UsersTable,
                    usuario.Id.ToString(),
                    "No se pudo crear la contraseña con el enlace.",
                    postData: result.Errors.Select(e => e.Description),
                    actorOverride: usuario.Email);
                return new ChangePasswordResult(false, result.Errors.Select(e => e.Description));
            }

            // Usar el enlace prueba que el correo es suyo.
            usuario.MustChangePassword = false;
            usuario.EmailConfirmed = true;
            await _userManager.UpdateAsync(usuario);

            await _eventlogServices.LogAsync(
                typeEvent.Edit,
                "Administration/Users",
                UsersTable,
                usuario.Id.ToString(),
                request.Mode == PasswordLinks.ModeInvite
                    ? "El usuario creó su contraseña con la invitación."
                    : "El usuario restableció su contraseña con el enlace del correo.",
                actorOverride: usuario.Email);

            await QueuePasswordChangedNoticeAsync(usuario);
            return new ChangePasswordResult(true, Enumerable.Empty<string>());
        }

        // ───────── Enlaces de contraseña ─────────

        // Nombre para los correos; Trim por usuarios guardados con espacios de más.
        private static string DisplayName(Data.ApplicationUser usuario) =>
            (string.IsNullOrWhiteSpace(usuario.FullName) ? usuario.UserName ?? string.Empty : usuario.FullName).Trim();

        private static string InvalidLinkMessage(string? mode) =>
            mode == PasswordLinks.ModeInvite ? InvalidInviteMessage : InvalidResetMessage;

        // Usuario y token decodificado del enlace; (null, null) si algo no cuadra. No dice cuál parte falló.
        private async Task<(Data.ApplicationUser? User, string? Token)> ResolveLinkAsync(Guid userId, string? encodedToken, string? mode)
        {
            if (mode != PasswordLinks.ModeInvite && mode != PasswordLinks.ModeReset)
                return (null, null);
            if (userId == Guid.Empty || string.IsNullOrWhiteSpace(encodedToken))
                return (null, null);

            string token;
            try
            {
                token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(encodedToken));
            }
            catch (FormatException)
            {
                return (null, null);
            }

            var usuario = await _userManager.FindByIdAsync(userId.ToString());
            return usuario == null ? (null, null) : (usuario, token);
        }

        private async Task<bool> IsLinkTokenValidAsync(Data.ApplicationUser usuario, string mode, string token)
        {
            if (mode == PasswordLinks.ModeInvite)
            {
                // Una invitación solo sirve mientras el usuario no tenga contraseña.
                return usuario.PasswordHash == null
                    && await _userManager.VerifyUserTokenAsync(usuario, PasswordLinks.InvitationProvider, PasswordLinks.InvitationPurpose, token);
            }

            return await _userManager.VerifyUserTokenAsync(usuario, _userManager.Options.Tokens.PasswordResetTokenProvider,
                UserManager<Data.ApplicationUser>.ResetPasswordTokenPurpose, token);
        }

        // Genera el token, arma el enlace a la UI y lo encola por correo. Con Email:Enabled=false no encola nada y
        // devuelve el enlace para que el administrador lo comparta a mano. El token va en Base64Url: el de Identity
        // trae '+', '/' y '=' que se rompen en una URL.
        private async Task<LinkDelivery> DeliverLinkAsync(Data.ApplicationUser usuario, string mode, string actor, bool requestedByAdmin)
        {
            string token = mode == PasswordLinks.ModeInvite
                ? await _userManager.GenerateUserTokenAsync(usuario, PasswordLinks.InvitationProvider, PasswordLinks.InvitationPurpose)
                : await _userManager.GeneratePasswordResetTokenAsync(usuario);

            string link = _emailServices.UiLink(PasswordLinks.UiPath, new Dictionary<string, string>
            {
                ["uid"] = usuario.Id.ToString(),
                ["token"] = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token)),
                ["mode"] = mode
            });

            if (!_emailServices.Enabled)
                return new LinkDelivery(null, link);

            string name = DisplayName(usuario);
            var message = mode == PasswordLinks.ModeInvite
                ? new EmailMessage(usuario.Email!, name, "Bienvenido al sistema de SAM DESIGN",
                    EmailTemplates.Invitation(name, usuario.UserName ?? string.Empty, link, PasswordLinks.InvitationHours),
                    EmailKinds.Invitation, UsersTable, usuario.Id.ToString(), IsSensitive: true)
                : new EmailMessage(usuario.Email!, name, "Restablecer contraseña",
                    EmailTemplates.PasswordReset(name, link, PasswordLinks.ResetMinutes, requestedByAdmin),
                    EmailKinds.PasswordReset, UsersTable, usuario.Id.ToString(), IsSensitive: true);

            await _emailServices.QueueAndSaveAsync(message, actor);
            return new LinkDelivery(usuario.Email, null);
        }

        // Aviso de seguridad. Se envía siempre (no depende de SendNotifications). Si falla, la contraseña ya cambió:
        // se registra el error y no se le devuelve al usuario.
        private async Task QueuePasswordChangedNoticeAsync(Data.ApplicationUser usuario)
        {
            if (!_emailServices.Enabled || string.IsNullOrWhiteSpace(usuario.Email))
                return;
            try
            {
                string name = DisplayName(usuario);
                var localNow = TimeZoneInfo.ConvertTime(_time.GetUtcNow(), _time.LocalTimeZone).DateTime;
                await _emailServices.QueueAndSaveAsync(
                    new EmailMessage(usuario.Email, name, "Tu contraseña cambió", EmailTemplates.PasswordChanged(name, localNow),
                        EmailKinds.PasswordChanged, UsersTable, usuario.Id.ToString()),
                    usuario.UserName ?? "Sistema");
            }
            catch (Exception ex)
            {
                await _eventlogServices.LogAsync(
                    typeEvent.Error,
                    "Administration/Users",
                    UsersTable,
                    usuario.Id.ToString(),
                    "No se pudo encolar el aviso de cambio de contraseña.",
                    stackTrace: ex.ToString());
            }
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
                HasPassword = user.PasswordHash != null,
                IsActive = !Security.UserSessionGuard.IsInactive(user),
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
                    HasPassword = user.PasswordHash != null,
                    IsActive = !Security.UserSessionGuard.IsInactive(user),
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

            usuario.FullName = request.FullName?.Trim() ?? string.Empty;
            usuario.Email = request.Email?.Trim();
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

        // ───────── Roles, inactivación y eliminación (reglas de seguridad) ─────────
        //  · Nadie cambia su propio rol, su estado ni se elimina a sí mismo (evita dejarse sin acceso).
        //  · Solo un AdminIT puede dar el rol AdminIT o tocar a un AdminIT: un Admin no se puede subir de nivel.
        //  · Siempre tiene que quedar al menos un AdminIT activo.

        private const string TopRole = "AdminIT";

        // null si se puede; si no, el motivo.
        private async Task<string?> CheckAdminChangeAsync(Data.ApplicationUser target, IList<string> targetRoles, Guid actorId, string? newRole, bool losesAccess, string verb)
        {
            if (target.Id == actorId) return $"No podés {verb} tu propio usuario.";

            var actor = await _userManager.FindByIdAsync(actorId.ToString());
            var actorIsTop = actor is not null && await _userManager.IsInRoleAsync(actor, TopRole);
            var targetIsTop = targetRoles.Contains(TopRole);
            if (!actorIsTop && (targetIsTop || newRole == TopRole))
                return "Solo un AdminIT puede dar el rol AdminIT o modificar a otro AdminIT.";

            // Si el usuario es AdminIT y deja de serlo (o se inactiva), tiene que quedar otro AdminIT activo.
            var stopsBeingTop = targetIsTop && (losesAccess || (newRole is not null && newRole != TopRole));
            if (stopsBeingTop)
            {
                var tops = await _userManager.GetUsersInRoleAsync(TopRole);
                if (!tops.Any(u => u.Id != target.Id && !Security.UserSessionGuard.IsInactive(u)))
                    return "Tiene que quedar al menos un AdminIT activo.";
            }
            return null;
        }

        public async Task<UserActionResult> ChangeRoleAsync(Guid id, string newRole, Guid actorId)
        {
            var usuario = await _userManager.FindByIdAsync(id.ToString());
            if (usuario == null)
                return new UserActionResult(false, NotFound: true);

            var currentRoles = await _userManager.GetRolesAsync(usuario);
            if (currentRoles.Count == 1 && currentRoles[0] == newRole)
                return new UserActionResult(true);

            var blocked = await CheckAdminChangeAsync(usuario, currentRoles, actorId, newRole, losesAccess: false, verb: "cambiar el rol de");
            if (blocked is not null) return new UserActionResult(false, blocked);

            // Primero se agrega el rol nuevo y después se quitan los otros: si algo falla, el usuario no se queda sin rol.
            if (!currentRoles.Contains(newRole))
            {
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
                    return new UserActionResult(false, $"No se pudo asignar el rol '{newRole}'.");
                }
            }

            var toRemove = currentRoles.Where(r => r != newRole).ToList();
            if (toRemove.Count > 0)
            {
                var removeResult = await _userManager.RemoveFromRolesAsync(usuario, toRemove);
                if (!removeResult.Succeeded)
                {
                    await _eventlogServices.LogAsync(
                        typeEvent.EditFail,
                        "Administration/Users",
                        UserRolesTable,
                        id.ToString(),
                        "No se pudieron quitar los roles anteriores del usuario.",
                        preData: currentRoles,
                        postData: removeResult.Errors.Select(e => e.Description));
                    return new UserActionResult(false, "No se pudieron quitar los roles anteriores del usuario.");
                }
            }

            Security.UserSessionGuard.Invalidate(_cache, id);
            await _eventlogServices.LogAsync(
                typeEvent.Edit,
                "Administration/Users",
                UserRolesTable,
                id.ToString(),
                $"Rol actualizado a '{newRole}'.",
                preData: currentRoles,
                postData: new List<string> { newRole });

            return new UserActionResult(true);
        }

        // Inactivar = bloqueo permanente de Identity (LockoutEnd al máximo): no borra nada y se puede deshacer. No necesita migración.
        public async Task<UserActionResult> SetActiveAsync(Guid id, bool active, Guid actorId)
        {
            var usuario = await _userManager.FindByIdAsync(id.ToString());
            if (usuario == null)
                return new UserActionResult(false, NotFound: true);

            var wasActive = !Security.UserSessionGuard.IsInactive(usuario);
            if (wasActive == active)
                return new UserActionResult(true);

            var roles = await _userManager.GetRolesAsync(usuario);
            var blocked = await CheckAdminChangeAsync(usuario, roles, actorId, newRole: null, losesAccess: !active, verb: active ? "reactivar" : "inactivar");
            if (blocked is not null) return new UserActionResult(false, blocked);

            var enable = await _userManager.SetLockoutEnabledAsync(usuario, true);
            var set = enable.Succeeded
                ? await _userManager.SetLockoutEndDateAsync(usuario, active ? null : DateTimeOffset.MaxValue)
                : enable;
            if (!set.Succeeded)
            {
                await _eventlogServices.LogAsync(
                    typeEvent.EditFail,
                    "Administration/Users",
                    UsersTable,
                    id.ToString(),
                    active ? "No se pudo reactivar el usuario." : "No se pudo inactivar el usuario.",
                    postData: set.Errors.Select(e => e.Description));
                return new UserActionResult(false, active ? "No se pudo reactivar el usuario." : "No se pudo inactivar el usuario.");
            }

            Security.UserSessionGuard.Invalidate(_cache, id);
            await _eventlogServices.LogAsync(
                typeEvent.Edit,
                "Administration/Users",
                UsersTable,
                id.ToString(),
                active ? "Usuario reactivado." : "Usuario inactivado.",
                preData: new { IsActive = wasActive },
                postData: new { IsActive = active });
            return new UserActionResult(true);
        }

        public async Task<UserActionResult> DeleteUserAsync(Guid id, Guid actorId)
        {
            var usuario = await _userManager.FindByIdAsync(id.ToString());
            if (usuario == null)
                return new UserActionResult(false, NotFound: true);

            var roles = await _userManager.GetRolesAsync(usuario);
            var blocked = await CheckAdminChangeAsync(usuario, roles, actorId, newRole: null, losesAccess: true, verb: "eliminar");
            if (blocked is not null) return new UserActionResult(false, blocked);

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
                return new UserActionResult(false, "No se pudo eliminar el usuario.");
            }

            Security.UserSessionGuard.Invalidate(_cache, id);
            await _eventlogServices.LogAsync(
                typeEvent.Delete,
                "Administration/Users",
                UsersTable,
                id.ToString(),
                "Usuario eliminado.",
                preData: before);

            return new UserActionResult(true);
        }

    }
}
