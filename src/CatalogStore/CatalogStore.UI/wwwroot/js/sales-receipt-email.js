// Reenviar el tiquete (venta sin IVA, no fiscal) al correo que el cliente tiene registrado.
// Botón "Enviar al cliente" de la tarjeta "Comprobante de venta" de Views/Sales/Details.cshtml. La factura con IVA se reenvía desde la tarjeta de facturación (sales-invoice.js).
document.addEventListener('DOMContentLoaded', function () {
    const btn = document.querySelector('[data-action="receipt-email"]');
    const tokenInput = document.querySelector('#salesDetailsTokenForm input[name="__RequestVerificationToken"]');
    if (!btn || !tokenInput) return;

    const brandColor = getComputedStyle(document.documentElement).getPropertyValue('--brand-primary').trim() || undefined;
    const esc = value => { const d = document.createElement('div'); d.textContent = value ?? ''; return d.innerHTML.replace(/"/g, '&quot;'); };

    btn.addEventListener('click', async function () {
        const c = await Swal.fire({
            icon: 'question',
            title: '¿Enviar el tiquete al cliente?',
            html: `Se le manda a <strong>${esc(btn.dataset.email)}</strong> un correo con el detalle de la compra. No se envía solo: lo decidís vos.`,
            showCancelButton: true,
            confirmButtonText: 'Enviar',
            cancelButtonText: 'Cancelar',
            confirmButtonColor: brandColor
        });
        if (!c.isConfirmed) return;

        btn.disabled = true;
        const body = new FormData();
        body.append('__RequestVerificationToken', tokenInput.value);
        body.append('salesHeaderId', btn.dataset.saleId);
        let result;
        try {
            const response = await fetch('/Sales/ReceiptEmail', { method: 'POST', body });
            result = response.ok ? await response.json() : { success: false, message: 'Error de comunicación con el servidor.' };
        } catch {
            result = { success: false, message: 'No hubo respuesta del servidor.' };
        }
        btn.disabled = false;
        if (result.success) {
            Swal.fire({ icon: 'success', title: 'Correo en cola', text: 'Sale en unos segundos.', timer: 2200, showConfirmButton: false });
        } else {
            Swal.fire({ icon: 'error', title: 'No se pudo enviar el correo', text: result.message || 'Intentá de nuevo.', confirmButtonColor: brandColor });
        }
    });
});
