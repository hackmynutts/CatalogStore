// Excepción del bloqueo por mora (Views/Client/Details.cshtml). Solo la cargan los administradores.
// "Autorizar excepción" pide motivo y fecha límite; "Retirar excepción" la quita. La API valida todo de nuevo.
document.addEventListener('DOMContentLoaded', function () {
    const tokenInput = document.querySelector('#creditTokenForm input[name="__RequestVerificationToken"]');
    if (!tokenInput) return;

    const MAX_DAYS = 90;
    const DEFAULT_DAYS = 7;

    function isoDate(daysFromToday) {
        const d = new Date();
        d.setDate(d.getDate() + daysFromToday);
        const pad = n => String(n).padStart(2, '0');
        return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
    }

    async function post(url, fields) {
        const body = new FormData();
        body.append('__RequestVerificationToken', tokenInput.value);
        Object.entries(fields).forEach(([k, v]) => body.append(k, v));
        const response = await fetch(url, { method: 'POST', body });
        return response.ok ? response.json() : { success: false, message: 'Intentá de nuevo.' };
    }

    document.querySelectorAll('.btn-credit-override').forEach(btn => {
        btn.addEventListener('click', async function () {
            const { value: form } = await Swal.fire({
                title: 'Autorizar excepción',
                html:
                    '<p class="text-muted" style="font-size:.9rem">El cliente podrá iniciar y completar órdenes hasta la fecha límite, aunque tenga ventas vencidas.</p>' +
                    '<input id="creditReason" class="swal2-input" maxlength="250" placeholder="Motivo (obligatorio)">' +
                    `<input id="creditUntil" type="date" class="swal2-input" min="${isoDate(0)}" max="${isoDate(MAX_DAYS)}" value="${isoDate(DEFAULT_DAYS)}">`,
                focusConfirm: false,
                showCancelButton: true,
                confirmButtonText: 'Autorizar',
                cancelButtonText: 'Cancelar',
                preConfirm: () => {
                    const reason = document.getElementById('creditReason').value.trim();
                    const until = document.getElementById('creditUntil').value;
                    if (!reason) { Swal.showValidationMessage('Escribí el motivo.'); return false; }
                    if (!until) { Swal.showValidationMessage('Elegí la fecha límite.'); return false; }
                    return { reason, until };
                }
            });
            if (!form) return;

            const result = await post(`/Client/CreditOverride/${btn.dataset.clientId}`, form);
            if (result.success) { location.reload(); return; }
            Swal.fire({ icon: 'error', title: 'No se pudo autorizar', text: result.message || 'Intentá de nuevo.' });
        });
    });

    document.querySelectorAll('.btn-credit-clear').forEach(btn => {
        btn.addEventListener('click', async function () {
            const confirm = await Swal.fire({
                icon: 'question',
                title: '¿Retirar la excepción?',
                text: 'Si el cliente sigue con ventas vencidas, quedará bloqueado de nuevo.',
                showCancelButton: true,
                confirmButtonText: 'Retirar',
                cancelButtonText: 'Cancelar'
            });
            if (!confirm.isConfirmed) return;

            const result = await post(`/Client/ClearCreditOverride/${btn.dataset.clientId}`, {});
            if (result.success) { location.reload(); return; }
            Swal.fire({ icon: 'error', title: 'No se pudo retirar', text: result.message || 'Intentá de nuevo.' });
        });
    });
});
