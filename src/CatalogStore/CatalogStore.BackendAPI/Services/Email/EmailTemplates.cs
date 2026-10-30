using System.Globalization;
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

        // ₡18,000.00 siempre, igual que las pantallas, sin depender de la cultura con la que corra el servidor.
        private static string Money(decimal value) => value.ToString("N2", CultureInfo.InvariantCulture);

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

        public record ProformaLine(string? Code, string Name, int Quantity, decimal UnitPrice, decimal DiscountPercent, decimal LineSubtotal);

        // Proforma (cotización, no fiscal): la tabla va en el cuerpo del correo porque la cola no admite adjuntos.
        public static string Proforma(string clientName, string number, DateTime issued, int validDays, IReadOnlyList<ProformaLine> lines,
            decimal subtotal, decimal discount, decimal iva, decimal total, bool applyIva, string? notes)
        {
            var rows = string.Concat(lines.Select(l => $"""
                <tr>
                  <td style="padding:8px 6px;border-bottom:1px solid #e5e7eb;font-size:13px;">{E(l.Name)}{(string.IsNullOrWhiteSpace(l.Code) ? "" : $"""<br><span style="color:{Muted};font-size:11px;">{E(l.Code)}</span>""")}</td>
                  <td style="padding:8px 6px;border-bottom:1px solid #e5e7eb;font-size:13px;text-align:right;">{l.Quantity}</td>
                  <td style="padding:8px 6px;border-bottom:1px solid #e5e7eb;font-size:13px;text-align:right;white-space:nowrap;">₡{Money(l.UnitPrice)}{(l.DiscountPercent > 0 ? $"""<br><span style="color:{Muted};font-size:11px;">- {l.DiscountPercent.ToString("0.##", CultureInfo.InvariantCulture)} %</span>""" : "")}</td>
                  <td style="padding:8px 6px;border-bottom:1px solid #e5e7eb;font-size:13px;text-align:right;white-space:nowrap;">₡{Money(l.LineSubtotal)}</td>
                </tr>
                """));
            string Total(string label, decimal value, bool bold = false) =>
                $"""<tr><td style="padding:3px 6px;text-align:right;font-size:13px;{(bold ? "font-weight:700;font-size:15px;" : "")}">{label}</td><td style="padding:3px 6px;text-align:right;font-size:13px;white-space:nowrap;{(bold ? "font-weight:700;font-size:15px;" : "")}">₡{Money(value)}</td></tr>""";

            return Layout(
                $"Proforma {number}",
                P($"Hola {E(clientName)},") +
                P($"Esta es tu proforma del {issued:dd/MM/yyyy}. Los precios son válidos por <strong>{validDays} días</strong>.") +
                $"""
                <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="margin:12px 0;border-collapse:collapse;">
                  <tr style="background:#f9fafb;">
                    <th align="left" style="padding:8px 6px;font-size:11px;color:{Muted};text-transform:uppercase;">Producto</th>
                    <th align="right" style="padding:8px 6px;font-size:11px;color:{Muted};text-transform:uppercase;">Cant.</th>
                    <th align="right" style="padding:8px 6px;font-size:11px;color:{Muted};text-transform:uppercase;">Precio</th>
                    <th align="right" style="padding:8px 6px;font-size:11px;color:{Muted};text-transform:uppercase;">Subtotal</th>
                  </tr>
                  {rows}
                </table>
                <table role="presentation" align="right" cellpadding="0" cellspacing="0" style="margin:0 0 16px;">
                  {Total("Subtotal", subtotal + discount)}
                  {(discount > 0 ? Total("Descuento", -discount) : "")}
                  {Total(applyIva ? "IVA" : "IVA (no aplica)", iva)}
                  {Total("Total", total, bold: true)}
                </table>
                <div style="clear:both;"></div>
                """ +
                (string.IsNullOrWhiteSpace(notes) ? "" : P($"""<span style="font-size:13px;color:{Muted};">Notas: {E(notes)}</span>""")) +
                P($"""<span style="font-size:12px;color:{Muted};">Documento informativo: no es un comprobante fiscal ni una factura.</span>"""),
                "Para aceptar esta proforma, respondé a tu vendedor.");
        }

        // Comprobante electrónico aceptado: el enlace lleva al PDF (el XML lo conserva el emisor).
        public static string InvoiceIssued(string clientName, string documentLabel, string saleNumber, decimal total, string pdfUrl, string clave) => Layout(
            documentLabel,
            P($"Hola {E(clientName)},") +
            P($"Te enviamos tu <strong>{E(documentLabel.ToLowerInvariant())}</strong> de la venta <strong>{E(saleNumber)}</strong> por un total de <strong>₡{Money(total)}</strong>.") +
            Button("Ver comprobante (PDF)", pdfUrl) +
            P($"""<span style="font-size:12px;color:{Muted};word-break:break-all;">Clave: {E(clave)}</span>"""),
            "Este comprobante fue emitido electrónicamente y aceptado por el Ministerio de Hacienda.");

        // Nota de crédito aceptada: le explica al cliente que la factura quedó anulada y le da el enlace al PDF.
        public static string CreditNoteIssued(string clientName, string saleNumber, string? originalNumber, decimal total, string? reason, string pdfUrl, string clave) => Layout(
            "Nota de crédito",
            P($"Hola {E(clientName)},") +
            P($"Anulamos {(string.IsNullOrWhiteSpace(originalNumber) ? "la factura" : $"la factura <strong>N.º {E(originalNumber)}</strong>")} de la venta <strong>{E(saleNumber)}</strong> " +
              $"y emitimos la <strong>nota de crédito</strong> por un total de <strong>₡{Money(total)}</strong>.") +
            (string.IsNullOrWhiteSpace(reason) ? "" : P($"""<span style="font-size:13px;color:{Muted};">Motivo: {E(reason)}</span>""")) +
            Button("Ver nota de crédito (PDF)", pdfUrl) +
            P($"""<span style="font-size:12px;color:{Muted};word-break:break-all;">Clave: {E(clave)}</span>"""),
            "Este comprobante fue emitido electrónicamente y aceptado por el Ministerio de Hacienda.");

        // Al vendedor: un administrador anuló un abono de una venta suya.
        public static string PaymentVoided(string sellerName, string saleNumber, string clientName, decimal amount, string method, DateTime paidOn,
            string reason, string voidedBy, decimal balance, string link) => Layout(
            "Se anuló un abono",
            P($"Hola {E(sellerName)},") +
            P($"<strong>{E(voidedBy)}</strong> anuló un abono de la venta <strong>{E(saleNumber)}</strong> ({E(clientName)}):") +
            $"""
            <table role="presentation" cellpadding="0" cellspacing="0" style="margin:6px 0 14px;font-size:14px;line-height:1.7;">
              <tr><td style="padding-right:16px;color:{Muted};">Monto</td><td><strong>₡{Money(amount)}</strong></td></tr>
              <tr><td style="padding-right:16px;color:{Muted};">Método</td><td>{E(method)}</td></tr>
              <tr><td style="padding-right:16px;color:{Muted};">Fecha del pago</td><td>{paidOn:dd/MM/yyyy}</td></tr>
              <tr><td style="padding-right:16px;color:{Muted};">Motivo</td><td>{E(reason)}</td></tr>
              <tr><td style="padding-right:16px;color:{Muted};">Saldo ahora</td><td><strong>₡{Money(balance)}</strong></td></tr>
            </table>
            """ +
            Button("Ver la venta", link) +
            P($"""<span style="font-size:13px;color:{Muted};">Si el cliente ya había pagado, revisá con el administrador antes de volver a cobrarle.</span>"""),
            "Recibís este aviso porque sos el vendedor de la venta. Podés apagar las notificaciones desde tu usuario.");

        // Al vendedor: su orden lleva días en revisión y se cancelará sola si no se completa.
        public static string OrderExpiryWarning(string sellerName, string orderNumber, string clientName, decimal total, int daysPending, DateTime expiresOn, string link) => Layout(
            "Tu orden está por cancelarse",
            P($"Hola {E(sellerName)},") +
            P($"La orden <strong>{E(orderNumber)}</strong> de {E(clientName)} por <strong>₡{Money(total)}</strong> lleva <strong>{daysPending} días</strong> en revisión.") +
            P($"Si no se completa antes del <strong>{expiresOn:dd/MM/yyyy}</strong>, se cancelará automáticamente y el inventario reservado volverá a estar disponible.") +
            Button("Ver la orden", link) +
            P($"""<span style="font-size:13px;color:{Muted};">Si ya no se va a vender, podés cancelarla vos mismo; si el cliente sigue interesado, completala.</span>"""),
            "Recibís este aviso porque sos el vendedor de la orden. Podés apagar las notificaciones desde tu usuario.");

        // Al vendedor: su orden se canceló sola por pasar demasiado tiempo en revisión.
        public static string OrderAutoCancelled(string sellerName, string orderNumber, string clientName, decimal total, int daysPending, string link) => Layout(
            "Se canceló una orden",
            P($"Hola {E(sellerName)},") +
            P($"La orden <strong>{E(orderNumber)}</strong> de {E(clientName)} por <strong>₡{Money(total)}</strong> llevaba <strong>{daysPending} días</strong> en revisión sin completarse, así que se canceló automáticamente.") +
            P("El inventario que tenía reservado ya volvió a estar disponible.") +
            Button("Ver la orden", link) +
            P($"""<span style="font-size:13px;color:{Muted};">Si el cliente todavía la necesita, creá una orden nueva.</span>"""),
            "Recibís este aviso porque sos el vendedor de la orden. Podés apagar las notificaciones desde tu usuario.");

        // Aviso de seguridad después de cualquier cambio de contraseña.
        public static string PasswordChanged(string fullName, DateTime localWhen) => Layout(
            "Tu contraseña cambió",
            P($"Hola {E(fullName)},") +
            P($"La contraseña de tu usuario se cambió el <strong>{localWhen:dd/MM/yyyy} a las {localWhen:HH:mm}</strong>.") +
            P("Si fuiste vos, no tenés que hacer nada."),
            "Si no reconocés este cambio, avisale de inmediato a un administrador.");
    }
}
