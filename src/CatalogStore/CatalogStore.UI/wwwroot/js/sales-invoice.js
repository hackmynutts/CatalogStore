// Facturación electrónica de una venta (tarjeta "Facturación electrónica" de Views/Sales/Details.cshtml).
// Emitir es un acto fiscal REAL: siempre se revisa primero (vista previa, no envía nada) y se pide confirmación.
// Cada acción llama a InvoicesController (UI); si sale bien, recarga porque la tarjeta ya viene armada desde el servidor.
document.addEventListener('DOMContentLoaded', function () {
    const card = document.getElementById('invoiceCard');
    const tokenInput = document.querySelector('#salesDetailsTokenForm input[name="__RequestVerificationToken"]');
    if (!card || !tokenInput) return;

    const token = tokenInput.value;
    const saleId = card.dataset.saleId;
    const brandColor = getComputedStyle(document.documentElement).getPropertyValue('--brand-primary').trim() || undefined;
    const money = new Intl.NumberFormat('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    const methods = { 1: 'Efectivo', 2: 'Tarjeta', 3: 'Cheque', 4: 'Transferencia / depósito', 6: 'SINPE Móvil', 7: 'Plataforma digital', 99: 'Otros' };

    function escapeHtml(value) {
        const div = document.createElement('div');
        div.textContent = value ?? '';
        return div.innerHTML.replace(/"/g, '&quot;');
    }

    async function post(url, data) {
        const body = new FormData();
        body.append('__RequestVerificationToken', token);
        Object.entries(data || {}).forEach(([k, v]) => { if (v !== null && v !== undefined) body.append(k, v); });
        try {
            const response = await fetch(url, { method: 'POST', body });
            if (!response.ok) return { success: false, message: 'Error de comunicación con el servidor.' };
            return await response.json();
        } catch {
            // Sin respuesta no se sabe si el servidor llegó a emitir: no se afirma nada.
            return { success: false, message: 'No hubo respuesta del servidor. Revisá el estado de la venta antes de volver a intentar.' };
        }
    }

    async function done(title, text) {
        await Swal.fire({ icon: 'success', title, text, timer: 2200, showConfirmButton: false });
        location.href = location.pathname + '#invoiceCard';
        location.reload();
    }

    function fail(title, message) {
        return Swal.fire({ icon: 'error', title, text: message || 'Intentá de nuevo.', confirmButtonColor: brandColor });
    }

    const reasonValidator = value => {
        const v = (value || '').trim();
        if (v.length < 5) return 'Escribí al menos 5 caracteres.';
        if (/[|_]/.test(v)) return 'No uses los caracteres | ni _.';
        return undefined;
    };

    // ───────── Revisar y emitir ─────────

    async function start(btn) {
        btn.disabled = true;
        let data;
        try { data = await (await fetch(`/Invoices/Preview?saleId=${encodeURIComponent(saleId)}`)).json(); }
        catch { data = { success: false, message: 'No se pudo revisar la venta.' }; }
        btn.disabled = false;
        if (!data.success) return fail('No se pudo revisar la venta', data.message);

        const p = data.preview;
        const doc = p.documentTypeName.toLowerCase();
        const needsMethod = p.paymentMethod === null || p.paymentMethod === undefined;
        // Elegir el medio de pago se resuelve acá mismo; todo lo demás impide emitir.
        const blocking = p.errors.filter(e => !e.includes('medio de pago'));

        const summary = `
            <dl class="invoice-summary">
                <div><dt>Comprobante</dt><dd>${escapeHtml(p.documentTypeName)}</dd></div>
                <div><dt>Productos</dt><dd>${p.lineCount}</dd></div>
                <div><dt>Subtotal</dt><dd>₡${money.format(p.subtotal)}</dd></div>
                <div><dt>IVA</dt><dd>₡${money.format(p.iva)}</dd></div>
                <div class="is-total"><dt>Total</dt><dd>₡${money.format(p.total)}</dd></div>
                <div><dt>Medio de pago</dt><dd>${needsMethod
                    ? `<select id="invMethod" class="form-select form-select-sm" aria-label="Medio de pago">
                           <option value="">Elegí uno…</option>
                           ${Object.entries(methods).map(([k, v]) => `<option value="${k}">${v}</option>`).join('')}
                       </select>`
                    : escapeHtml(methods[p.paymentMethod] || String(p.paymentMethod)) + ' <small>(el del primer abono)</small>'}</dd></div>
            </dl>`;

        if (blocking.length > 0) {
            await Swal.fire({
                icon: 'warning',
                title: `Todavía no se puede emitir ${doc === 'factura electrónica' ? 'la factura' : 'el ' + doc.split(' ')[0]}`,
                html: summary.replace(/<select[\s\S]*?<\/select>/, '<em>Se elige al emitir</em>') +
                    `<ul class="invoice-errors">${blocking.map(e => `<li>${escapeHtml(e)}</li>`).join('')}</ul>`,
                confirmButtonText: 'Entendido',
                confirmButtonColor: brandColor
            });
            return;
        }

        const result = await Swal.fire({
            icon: 'warning',
            title: `Emitir ${doc}`,
            html: summary + `<p class="invoice-warning"><strong>Es un comprobante real.</strong> Se envía a Hacienda a través del proveedor y no se puede deshacer; si te equivocás se corrige con una nota de crédito. Si se corta la conexión, el intento queda "por conciliar" y no se reenvía solo.</p>`,
            showCancelButton: true,
            confirmButtonText: 'Emitir ahora',
            cancelButtonText: 'Volver',
            confirmButtonColor: '#b42318',
            allowOutsideClick: () => !Swal.isLoading(),
            preConfirm: async () => {
                let method = null;
                if (needsMethod) {
                    method = document.getElementById('invMethod').value;
                    if (!method) { Swal.showValidationMessage('Elegí el medio de pago.'); return false; }
                }
                // El spinner de SweetAlert queda hasta que el servidor responde (puede tardar: habla con Hacienda).
                return await post('/Invoices/Emit', { salesHeaderId: saleId, paymentMethod: method });
            }
        });
        if (!result.isConfirmed) return;
        if (result.value.success) return done('Enviada a Hacienda', 'Actualizá el estado en unos segundos para ver si la aceptó y traer el PDF.');
        await fail('No se emitió', result.value.message);
        location.reload(); // si quedó un intento "por conciliar" tiene que verse
    }

    // ───────── Acciones sobre un comprobante ─────────

    async function refresh(btn) {
        btn.disabled = true;
        const r = await post('/Invoices/Refresh', { id: btn.dataset.id });
        if (r.success) return done('Estado actualizado');
        btn.disabled = false;
        return fail('No se pudo consultar el estado', r.message);
    }

    async function email(btn) {
        const c = await Swal.fire({
            icon: 'question',
            title: `¿Enviar la ${escapeHtml(btn.dataset.doc || 'comprobante')} al cliente?`,
            html: `Se le manda a <strong>${escapeHtml(btn.dataset.email)}</strong> un correo con el enlace al PDF. No se envía solo: lo decidís vos.`,
            showCancelButton: true,
            confirmButtonText: 'Enviar',
            cancelButtonText: 'Cancelar',
            confirmButtonColor: brandColor
        });
        if (!c.isConfirmed) return;
        btn.disabled = true;
        const r = await post('/Invoices/Email', { id: btn.dataset.id });
        if (r.success) return done('Correo en cola', 'Sale en unos segundos.');
        btn.disabled = false;
        return fail('No se pudo enviar el correo', r.message);
    }

    async function discard(btn) {
        const c = await Swal.fire({
            icon: 'warning',
            title: '¿Descartar este intento?',
            html: 'Hacelo <strong>solo si verificaste con el proveedor que el comprobante NO se emitió</strong>. Si sí se emitió y lo descartás, podrías facturar la venta dos veces.',
            input: 'textarea',
            inputLabel: 'Cómo lo verificaste',
            inputPlaceholder: 'Ej. el proveedor confirmó que no hay comprobante con esa clave',
            inputAttributes: { maxlength: '250' },
            inputValidator: reasonValidator,
            showCancelButton: true,
            confirmButtonText: 'Descartar intento',
            cancelButtonText: 'Volver',
            confirmButtonColor: '#dc3545'
        });
        if (!c.isConfirmed) return;
        btn.disabled = true;
        const r = await post('/Invoices/Discard', { id: btn.dataset.id, reason: c.value.trim() });
        if (r.success) return done('Intento descartado', 'Ya se puede volver a emitir.');
        btn.disabled = false;
        return fail('No se pudo descartar', r.message);
    }

    async function creditNote(btn) {
        const c = await Swal.fire({
            icon: 'warning',
            title: 'Emitir nota de crédito',
            html: `Anula <strong>por completo</strong> el ${escapeHtml(btn.dataset.label)} de esta venta ante Hacienda. Después podrás anular la venta. <strong>No se puede deshacer.</strong>`,
            input: 'textarea',
            inputLabel: 'Motivo',
            inputPlaceholder: 'Ej. el cliente devolvió la mercadería, error en la venta',
            inputAttributes: { maxlength: '180' },
            inputValidator: reasonValidator,
            showCancelButton: true,
            confirmButtonText: 'Emitir nota de crédito',
            cancelButtonText: 'Volver',
            confirmButtonColor: '#b42318',
            allowOutsideClick: () => !Swal.isLoading(),
            showLoaderOnConfirm: true,
            preConfirm: reason => post('/Invoices/CreditNote', { salesHeaderId: saleId, reason: reason.trim() })
        });
        if (!c.isConfirmed) return;
        if (c.value.success) return done('Nota de crédito enviada', 'Actualizá el estado hasta que Hacienda la acepte; después ya se puede anular la venta.');
        await fail('No se emitió la nota de crédito', c.value.message);
        location.reload();
    }

    card.addEventListener('click', function (e) {
        const btn = e.target.closest('[data-action]');
        if (!btn) return;
        switch (btn.dataset.action) {
            case 'invoice-start': return start(btn);
            case 'invoice-refresh': return refresh(btn);
            case 'invoice-email': return email(btn);
            case 'invoice-discard': return discard(btn);
            case 'invoice-credit-note': return creditNote(btn);
        }
    });
});
