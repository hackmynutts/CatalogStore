using System.Net;

namespace CatalogStore.BackendAPI.Services.Email
{
    // Plantillas HTML de los correos. Estilos en línea y tablas: es lo único que respetan Gmail y Outlook.
    // Todo texto que venga de datos (nombres, motivos) pasa por E() para que no se pueda inyectar HTML.
    public static class EmailTemplates
    {
        private const string Brand = "#2f5d50";
        private const string Accent = "#c8a24a";
        private const string Muted = "#6b7280";

        private static string E(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

        // Marco común: encabezado con la marca, contenido y pie.
        public static string Layout(string title, string contentHtml, string? footerNote = null) => $"""
            <!doctype html>
            <html lang="es"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>{E(title)}</title></head>
            <body style="margin:0;padding:0;background:#f3f4f6;font-family:Segoe UI,Roboto,Helvetica,Arial,sans-serif;color:#1f2937;">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#f3f4f6;padding:24px 12px;">
                <tr><td align="center">
                  <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:560px;background:#ffffff;border-radius:10px;overflow:hidden;">
                    <tr><td style="background:{Brand};padding:18px 28px;border-bottom:3px solid {Accent};">
                      <span style="color:#ffffff;font-size:18px;font-weight:700;letter-spacing:.5px;">SAM DESIGN</span>
                    </td></tr>
                    <tr><td style="padding:28px;">
                      <h1 style="margin:0 0 16px;font-size:20px;color:{Brand};">{E(title)}</h1>
                      {contentHtml}
                    </td></tr>
                    <tr><td style="padding:16px 28px;background:#f9fafb;color:{Muted};font-size:12px;line-height:1.5;">
                      {(footerNote is null ? string.Empty : E(footerNote) + "<br>")}Este es un correo automático, no lo respondas.
                    </td></tr>
                  </table>
                </td></tr>
              </table>
            </body></html>
            """;

        // Botón con el enlace también escrito abajo: si el botón no se ve, el usuario puede copiar la dirección.
        public static string Button(string text, string url) => $"""
            <table role="presentation" cellpadding="0" cellspacing="0" style="margin:24px 0;"><tr>
              <td style="background:{Brand};border-radius:6px;">
                <a href="{E(url)}" style="display:inline-block;padding:12px 24px;color:#ffffff;font-weight:600;text-decoration:none;">{E(text)}</a>
              </td>
            </tr></table>
            <p style="margin:0 0 8px;font-size:12px;color:{Muted};">Si el botón no funciona, copiá esta dirección en el navegador:</p>
            <p style="margin:0;font-size:12px;word-break:break-all;"><a href="{E(url)}" style="color:{Brand};">{E(url)}</a></p>
            """;

        private static string P(string html) => $"""<p style="margin:0 0 12px;font-size:15px;line-height:1.6;">{html}</p>""";

        // Usuario nuevo: crea su contraseña con el enlace.
        public static string Invitation(string fullName, string userName, string link, int validHours) => Layout(
            "Bienvenido al sistema",
            P($"Hola {E(fullName)},") +
            P($"Se creó tu usuario <strong>{E(userName)}</strong> en el sistema de SAM DESIGN. Para entrar, primero creá tu contraseña:") +
            Button("Crear mi contraseña", link) +
            P($"""<span style="font-size:13px;color:{Muted};">El enlace vence en {validHours} horas. Si vence, pedile a un administrador que te reenvíe la invitación.</span>"""),
            "Si no esperabas este correo, podés ignorarlo.");

        // "Olvidé mi contraseña" o reset hecho por un administrador.
        public static string PasswordReset(string fullName, string link, int validMinutes, bool requestedByAdmin) => Layout(
            "Restablecer contraseña",
            P($"Hola {E(fullName)},") +
            P(requestedByAdmin
                ? "Un administrador pidió restablecer tu contraseña. Creá una nueva con el siguiente enlace:"
                : "Recibimos una solicitud para restablecer tu contraseña. Creá una nueva con el siguiente enlace:") +
            Button("Crear nueva contraseña", link) +
            P($"""<span style="font-size:13px;color:{Muted};">El enlace vence en {validMinutes} minutos y se puede usar una sola vez.</span>"""),
            "Si no pediste este cambio, ignorá este correo: tu contraseña actual sigue funcionando.");

        // Aviso de seguridad después de cualquier cambio de contraseña.
        public static string PasswordChanged(string fullName, DateTime localWhen) => Layout(
            "Tu contraseña cambió",
            P($"Hola {E(fullName)},") +
            P($"La contraseña de tu usuario se cambió el <strong>{localWhen:dd/MM/yyyy} a las {localWhen:HH:mm}</strong>.") +
            P("Si fuiste vos, no tenés que hacer nada."),
            "Si no reconocés este cambio, avisale de inmediato a un administrador.");
    }
}
