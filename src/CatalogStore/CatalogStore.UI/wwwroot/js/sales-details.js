// Detalle de una venta (Views/Sales/Details.cshtml): registrar un pago, anular abonos y anular la venta.
// Cada acción llama a SalesController (UI) y, si sale bien, recarga: la vista ya viene armada desde el servidor.
document.addEventListener('DOMContentLoaded', function () {
    const root = document.getElementById('saleDetails');
    if (!root) return;

    const token = document.querySelector('#salesDetailsTokenForm input[name="__RequestVerificationToken"]').value;
    const saleId = root.dataset.saleId;
    const saleNumber = root.dataset.saleNumber;
    const brandColor = getComputedStyle(document.documentElement).getPropertyValue('--brand-primary').trim() || undefined;
    const money = new Intl.NumberFormat('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });

    async function post(url, data) {
        const body = new FormData();
        body.append('__RequestVerificationToken', token);
        body.append('salesHeaderId', saleId);
        Object.entries(data).forEach(([k, v]) => body.append(k, v ?? ''));
        const response = await fetch(url, { method: 'POST', body });
        if (!response.ok) return { success: false, message: 'Error de comunicación con el servidor.' };
        return response.json();
    }

    // ───────── Registrar pago ─────────

    const form = document.getElementById('paymentForm');
    if (form) {
        const modalEl = document.getElementById('paymentModal');
        const amount = document.getElementById('payAmount');
        const amountError = document.getElementById('payAmountError');
        const submit = document.getElementById('paySubmit');
        const maxAmount = Number(amount.max);

        // Validación al salir del campo (y al enviar): el mensaje aparece junto al monto, no arriba.
        function validateAmount() {
            const value = Number(amount.value);
            let message = '';
            if (!amount.value || Number.isNaN(value) || value <= 0) message = 'Ingresá un monto mayor a cero.';
            else if (value > maxAmount) message = `No puede superar el saldo de ₡${money.format(maxAmount)}.`;
            else if (Math.round(value * 100) !== value * 100) message = 'Usá como máximo dos decimales.';
            amount.classList.toggle('is-invalid', !!message);
            amountError.textContent = message;
            amountError.hidden = !message;
            return !message;
        }

        amount.addEventListener('blur', validateAmount);
        amount.addEventListener('input', () => { if (amount.classList.contains('is-invalid')) validateAmount(); });
        document.getElementById('payFullBalance').addEventListener('click', () => {
            amount.value = maxAmount.toFixed(2);
            validateAmount();
            amount.focus();
        });
        modalEl.addEventListener('shown.bs.modal', () => { amount.focus(); amount.select(); });

        form.addEventListener('submit', async function (e) {
            e.preventDefault();
            if (!validateAmount()) { amount.focus(); return; }

            const value = Number(amount.value);
            const method = document.getElementById('payMethod');
            const methodLabel = method.options[method.selectedIndex].text;
            const isFull = Math.abs(value - maxAmount) < 0.005;

            submit.disabled = true;
            const label = submit.innerHTML;
            submit.innerHTML = '<span class="spinner-border spinner-border-sm" aria-hidden="true"></span> Registrando…';
            try {
                const result = await post('/Sales/RegisterPayment', {
                    amount: value.toFixed(2),
                    paymentMethod: method.value,
                    paymentDate: document.getElementById('payDate').value,
                    reference: document.getElementById('payReference').value.trim(),
                    notes: document.getElementById('payNotes').value.trim()
                });
                if (!result.success) {
                    Swal.fire({ icon: 'error', title: 'No se pudo registrar el pago', text: result.message || 'Intentá de nuevo.' });
                    return;
                }
                bootstrap.Modal.getInstance(modalEl)?.hide();
                await Swal.fire({
                    icon: 'success',
                    title: isFull ? `${saleNumber} quedó pagada` : 'Abono registrado',
                    text: `₡${money.format(value)} por ${methodLabel}.`,
                    timer: 1800,
                    showConfirmButton: false
                });
                location.href = location.pathname; // recarga sin ?created=true
            } finally {
                submit.disabled = false;
                submit.innerHTML = label;
            }
        });
    }

    // ───────── Anular abono (solo admin; el botón solo existe para admins) ─────────

    document.addEventListener('click', async function (e) {
        const btn = e.target.closest('[data-action="void-payment"]');
        if (!btn) return;

        const result = await Swal.fire({
            icon: 'warning',
            title: `¿Anular el abono de ${btn.dataset.amount}?`,
            text: 'El abono queda en el historial marcado como anulado y deja de contar para el saldo. No se puede deshacer.',
            input: 'textarea',
            inputLabel: 'Motivo de la anulación',
            inputPlaceholder: 'Ej. registrado dos veces, monto equivocado',
            inputAttributes: { maxlength: '250' },
            inputValidator: value => !value || !value.trim() ? 'Escribí el motivo.' : undefined,
            showCancelButton: true,
            confirmButtonText: 'Anular abono',
            cancelButtonText: 'Volver',
            confirmButtonColor: '#dc3545'
        });
        if (!result.isConfirmed) return;

        btn.disabled = true;
        const response = await post('/Sales/VoidPayment', { salesPaymentId: btn.dataset.paymentId, reason: result.value.trim() });
        if (!response.success) {
            btn.disabled = false;
            Swal.fire({ icon: 'error', title: 'No se pudo anular el abono', text: response.message || 'Intentá de nuevo.' });
            return;
        }
        await Swal.fire({ icon: 'success', title: 'Abono anulado', timer: 1500, showConfirmButton: false, confirmButtonColor: brandColor });
        location.href = location.pathname;
    });

    // ───────── Anular venta (solo admin; el botón solo existe para admins) ─────────
    // Con abonos vigentes la API la rechaza: se avisa antes para no pedir un motivo que no va a servir.

    document.addEventListener('click', async function (e) {
        const btn = e.target.closest('[data-action="void-sale"]');
        if (!btn) return;

        // Una venta con factura o tiquete electrónico no se anula sin su nota de crédito aceptada por Hacienda.
        const invoiceState = btn.dataset.invoiceState;
        if (invoiceState === 'needs-credit-note' || invoiceState === 'credit-pending') {
            const card = document.getElementById('invoiceCard');
            const r = await Swal.fire({
                icon: 'info',
                title: invoiceState === 'needs-credit-note' ? 'Primero la nota de crédito' : 'La nota de crédito está pendiente',
                text: invoiceState === 'needs-credit-note'
                    ? 'Esta venta tiene un comprobante electrónico vigente. Emití la nota de crédito, esperá a que Hacienda la acepte y después anulá la venta.'
                    : 'La nota de crédito todavía no está aceptada por Hacienda. Actualizá su estado en "Facturación electrónica" y volvé a intentar.',
                showCancelButton: !!card,
                confirmButtonText: card ? 'Ir a Facturación' : 'Entendido',
                cancelButtonText: 'Cerrar',
                confirmButtonColor: brandColor
            });
            if (r.isConfirmed && card) card.scrollIntoView({ behavior: 'smooth', block: 'start' });
            return;
        }

        const activePayments = Number(btn.dataset.activePayments || 0);
        if (activePayments > 0) {
            Swal.fire({
                icon: 'info',
                title: 'Primero anulá los abonos',
                text: activePayments === 1
                    ? 'La venta tiene 1 abono vigente. Anulalo (con su motivo) y después anulá la venta.'
                    : `La venta tiene ${activePayments} abonos vigentes. Anulalos (cada uno con su motivo) y después anulá la venta.`,
                confirmButtonColor: brandColor
            });
            return;
        }

        const result = await Swal.fire({
            icon: 'warning',
            title: `¿Anular la venta ${saleNumber}?`,
            html: `Total <strong>${btn.dataset.total}</strong>. Los productos vuelven al inventario y la orden <strong>${btn.dataset.orderNumber}</strong> queda retornada. <strong>No se puede deshacer.</strong>`,
            input: 'textarea',
            inputLabel: 'Motivo de la anulación',
            inputPlaceholder: 'Ej. el cliente devolvió la mercadería, venta duplicada',
            inputAttributes: { maxlength: '250' },
            inputValidator: value => !value || !value.trim() ? 'Escribí el motivo.' : undefined,
            showCancelButton: true,
            confirmButtonText: 'Anular venta',
            cancelButtonText: 'Volver',
            confirmButtonColor: '#dc3545'
        });
        if (!result.isConfirmed) return;

        btn.disabled = true;
        const response = await post('/Sales/VoidSale', { reason: result.value.trim() });
        if (!response.success) {
            btn.disabled = false;
            Swal.fire({ icon: 'error', title: 'No se pudo anular la venta', text: response.message || 'Intentá de nuevo.' });
            return;
        }
        await Swal.fire({ icon: 'success', title: `${saleNumber} anulada`, text: 'El stock volvió al inventario.', timer: 1800, showConfirmButton: false });
        location.href = location.pathname;
    });
});
