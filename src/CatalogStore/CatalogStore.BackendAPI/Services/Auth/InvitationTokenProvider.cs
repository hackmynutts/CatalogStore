using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace CatalogStore.BackendAPI.Services.Auth
{
    // Enlaces de contraseña que viajan por correo. Llevan un token de Identity: firmado, con fecha y atado al
    // SecurityStamp del usuario, así que deja de servir apenas la contraseña cambia (un solo uso).
    public static class PasswordLinks
    {
        public const string InvitationProvider = "Invitation";
        public const string InvitationPurpose = "Invitation";
        public const int InvitationHours = 48;
        public const int ResetMinutes = 60;

        public const string ModeInvite = "invite";
        public const string ModeReset = "reset";

        // Página de la UI que recibe el enlace (uid, token, mode).
        public const string UiPath = "Account/SetPassword";
    }

    // Opciones propias: DataProtectorTokenProvider lee la duración del tipo de opciones que recibe, así que con un
    // tipo aparte la invitación dura 48 h sin tocar los 60 min del reset (DataProtectionTokenProviderOptions).
    public class InvitationTokenProviderOptions : DataProtectionTokenProviderOptions
    {
    }

    public class InvitationTokenProvider<TUser> : DataProtectorTokenProvider<TUser> where TUser : class
    {
        public InvitationTokenProvider(
            IDataProtectionProvider dataProtectionProvider,
            IOptions<InvitationTokenProviderOptions> options,
            ILogger<DataProtectorTokenProvider<TUser>> logger)
            : base(dataProtectionProvider, options, logger)
        {
        }
    }
}
