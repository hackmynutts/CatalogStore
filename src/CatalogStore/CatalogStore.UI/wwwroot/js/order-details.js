// Detalle de una orden (Views/Order/Details.cshtml). Cada acción llama a OrderController (UI) y, si sale bien,
// recarga la página: la vista ya viene armada del servidor con los montos que calcula el backend.
document.addEventListener('DOMContentLoaded', function () {
    const root = document.getElementById('orderDetails');
    if (!root) return;

    const token = document.querySelector('#orderDetailsTokenForm input[name="__RequestVerificationToken"]').value;
    const orderId = root.dataset.orderId;
    const orderNumber = root.dataset.orderNumber;
    const brandColor = getComputedStyle(document.documentElement).getPropertyValue('--brand-primary').trim() || undefined;
    let busy = false;

    async function post(url, data) {
        const body = new FormData();
        body.append('__RequestVerificationToken', token);
        body.append('orderId', orderId);
        Object.entries(data || {}).forEach(([k, v]) => body.append(k, v));
        const response = await fetch(url, { method: 'POST', body });
        if (!response.ok) return { success: false, message: 'Error de comunicación con el servidor.' };
        return response.json();
    }

    // Ejecuta una acción; si sale bien muestra el aviso (opcional) y recarga. Si falla, muestra el mensaje de la API.
    async function run(url, data, successTitle, errorTitle) {
        if (busy) return;
        busy = true;
        document.body.classList.add('order-busy');
        try {
            const result = await post(url, data);
            if (!result.success) {
                await Swal.fire({ icon: 'error', title: errorTitle, text: result.message || 'Intentá de nuevo.' });
                location.reload();
                return;
            }
            if (result.redirectUrl) { location.href = result.redirectUrl; return; }
            if (successTitle) {
                await Swal.fire({ icon: 'success', title: successTitle, timer: 1500, showConfirmButton: false });
            }
            location.reload();
        } finally {
            busy = false;
            document.body.classList.remove('order-busy');
        }
    }

    async function confirm(options) {
        const result = await Swal.fire(Object.assign({
            icon: 'question', showCancelButton: true, cancelButtonText: 'Volver', confirmButtonColor: brandColor
        }, options));
        return result.isConfirmed;
    }

    // ───────── botones de la cabecera ─────────

    document.addEventListener('click', async function (e) {
        const btn = e.target.closest('[data-action]');
        if (!btn || btn.disabled) return;
        const action = btn.getAttribute('data-action');

        if (action === 'activate') {
            run('/Order/Activate', {}, null, 'No se pudo activar la orden');
        }
        else if (action === 'submit') {
            if (await confirm({
                title: `¿Enviar ${orderNumber} a revisión?`,
                html: 'Los precios se actualizan con los vigentes y la orden queda bloqueada hasta que se complete o se devuelva a edición.',
                confirmButtonText: 'Enviar a revisión'
            })) run('/Order/Submit', {}, 'Orden enviada a revisión', 'No se pudo enviar a revisión');
        }
        else if (action === 'reopen') {
            if (await confirm({
                title: `¿Devolver ${orderNumber} a edición?`,
                html: 'Vas a poder agregar, quitar y cambiar productos. Los productos siguen reservados.',
                confirmButtonText: 'Devolver a edición'
            })) run('/Order/Reopen', {}, 'Orden devuelta a edición', 'No se pudo devolver a edición');
        }
        else if (action === 'complete') {
            if (await confirm({
                icon: 'warning',
                title: `¿Completar ${orderNumber}?`,
                html: `Total <strong>${root.dataset.total}</strong>. Se descuentan los productos del inventario y la orden ya no se puede modificar ni cancelar.`,
                confirmButtonText: 'Completar orden'
            })) run('/Order/Complete', {}, 'Orden completada', 'No se pudo completar la orden');
        }
        else if (action === 'cancel') {
            const result = await Swal.fire({
                icon: 'warning',
                title: `¿Cancelar ${orderNumber}?`,
                text: 'Se liberan todos los productos reservados. Esta acción no se puede deshacer.',
                input: 'textarea',
                inputLabel: 'Motivo de la cancelación',
                inputPlaceholder: 'Ej. el cliente desistió de la compra',
                inputAttributes: { maxlength: '250' },
                inputValidator: value => !value || !value.trim() ? 'Escribí el motivo.' : undefined,
                showCancelButton: true,
                confirmButtonText: 'Cancelar orden',
                cancelButtonText: 'Volver',
                confirmButtonColor: '#dc3545'
            });
            if (result.isConfirmed) run('/Order/Cancel', { reason: result.value.trim() }, 'Orden cancelada', 'No se pudo cancelar la orden');
        }
        else if (action === 'remove-line') {
            const row = btn.closest('tr');
            run('/Order/RemoveLine', { lineId: row.dataset.lineId }, null, 'No se pudo quitar el producto');
        }
        else if (action === 'order-discount') {
            const lines = [...document.querySelectorAll('#orderLinesTable tbody tr[data-line-id]')].map(row => ({
                name: row.querySelector('[data-line-name]').dataset.lineName,
                discount: Number(row.dataset.discount)
            }));
            const discount = await OrderDiscount.ask({ lines, maxDiscount: Number(root.dataset.maxDiscount), confirmButtonColor: brandColor });
            if (discount === null) return;
            run('/Order/SetDiscount', { discount: String(discount) },
                discount === 0 ? 'Descuentos quitados' : `Descuento de ${discount} % aplicado`, 'No se pudo aplicar el descuento');
        }
        else if (action === 'save-notes') {
            const notes = document.getElementById('orderNotes').value;
            run('/Order/UpdateNotes', { clientId: root.dataset.clientId, notes }, 'Notas guardadas', 'No se pudieron guardar las notas');
        }
    });

    // ───────── cantidad y descuento por línea (se guardan al salir del campo) ─────────

    document.querySelectorAll('.order-input').forEach(function (input) {
        input.addEventListener('change', function () {
            const row = input.closest('tr');
            const quantityInput = row.querySelector('[data-field="quantity"]');
            const discountInput = row.querySelector('[data-field="discount"]');
            const quantity = Number(quantityInput.value);
            const discount = Number(discountInput.value || 0);

            if (!Number.isInteger(quantity) || quantity < 1) {
                Swal.fire({ icon: 'error', title: 'Cantidad inválida', text: 'La cantidad debe ser un número entero mayor a cero.' });
                quantityInput.value = row.dataset.quantity;
                return;
            }
            if (Number.isNaN(discount) || discount < 0 || discount > 99.99) {
                Swal.fire({ icon: 'error', title: 'Descuento inválido', text: 'El descuento debe estar entre 0 y 99.99 %.' });
                discountInput.value = row.dataset.discount;
                return;
            }
            // Por texto: en JS 5.55 * 100 no da exacto.
            if (discountInput.value && !/^\d+(\.\d{1,2})?$/.test(discountInput.value.trim())) {
                Swal.fire({ icon: 'error', title: 'Descuento inválido', text: 'El descuento admite como máximo dos decimales.' });
                discountInput.value = row.dataset.discount;
                return;
            }
            run('/Order/UpdateLine', { lineId: row.dataset.lineId, quantity, discount: String(discount) }, null, 'No se pudo actualizar el producto');
        });

        input.addEventListener('keydown', function (e) {
            if (e.key === 'Enter') { e.preventDefault(); input.blur(); }
        });
    });

    const ivaSwitch = document.getElementById('orderApplyIva');
    if (ivaSwitch) {
        ivaSwitch.addEventListener('change', function () {
            run('/Order/SetIva', { applyIva: ivaSwitch.checked }, null, 'No se pudo cambiar el IVA');
        });
    }

    const notes = document.getElementById('orderNotes');
    const notesCount = document.getElementById('orderNotesCount');
    if (notes && notesCount) {
        notes.addEventListener('input', () => { notesCount.textContent = notes.value.length; });
    }
});
