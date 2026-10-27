// Metas del equipo (Views/Goals/Team.cshtml): definir o editar las metas de un vendedor y copiar las del mes anterior.
// Llama a GoalsController (UI); si sale bien recarga, porque la página ya viene armada desde el servidor.
document.addEventListener('DOMContentLoaded', function () {
    const tokenInput = document.querySelector('#goalsTokenForm input[name="__RequestVerificationToken"]');
    if (!tokenInput) return;
    const token = tokenInput.value;
    const brandColor = getComputedStyle(document.documentElement).getPropertyValue('--brand-primary').trim() || undefined;

    async function post(url, data) {
        const body = new FormData();
        body.append('__RequestVerificationToken', token);
        Object.entries(data).forEach(([k, v]) => body.append(k, v ?? ''));
        const response = await fetch(url, { method: 'POST', body });
        if (!response.ok) return { success: false, message: 'Error de comunicación con el servidor.' };
        return response.json();
    }

    // ───────── Editar / definir metas ─────────

    const modalEl = document.getElementById('goalModal');
    const form = document.getElementById('goalForm');
    const modal = new bootstrap.Modal(modalEl);
    const fields = {
        sold: document.getElementById('goalSold'),
        collected: document.getElementById('goalCollected'),
        count: document.getElementById('goalCount')
    };
    const errors = {
        sold: document.getElementById('goalSoldError'),
        collected: document.getElementById('goalCollectedError'),
        count: document.getElementById('goalCountError')
    };
    const formError = document.getElementById('goalFormError');
    const submit = document.getElementById('goalSubmit');

    // Se compara el texto con una expresión y no con n * 100: 5.55 * 100 da 554.9999… en coma flotante.
    const money = /^\d+(\.\d{1,2})?$/;
    const whole = /^\d+$/;

    function setError(key, message) {
        fields[key].classList.toggle('is-invalid', !!message);
        errors[key].textContent = message || '';
    }

    // Vacío cuenta como 0 (sin meta).
    function validate() {
        const sold = fields.sold.value.trim(), collected = fields.collected.value.trim(), count = fields.count.value.trim();
        const moneyError = v => v !== '' && !money.test(v) ? 'Usá un monto positivo con hasta dos decimales.' : '';
        setError('sold', moneyError(sold));
        setError('collected', moneyError(collected));
        setError('count', count !== '' && !whole.test(count) ? 'Usá un número entero, sin decimales.' : '');
        return !Object.values(fields).some(f => f.classList.contains('is-invalid'));
    }

    Object.entries(fields).forEach(([key, input]) => {
        input.addEventListener('blur', validate);
        input.addEventListener('input', () => { if (input.classList.contains('is-invalid')) validate(); });
    });

    document.querySelectorAll('.goal-edit-btn').forEach(btn => btn.addEventListener('click', function () {
        document.getElementById('goalModalSeller').textContent = btn.dataset.sellerName;
        document.getElementById('goalSeller').value = btn.dataset.seller;
        document.getElementById('goalRowVersion').value = btn.dataset.rv || '';
        // 0 = sin meta: se muestra vacío para que escribir sea directo.
        const show = v => (Number(v) > 0 ? v : '');
        fields.sold.value = show(btn.dataset.sold);
        fields.collected.value = show(btn.dataset.collected);
        fields.count.value = show(btn.dataset.count);
        Object.keys(fields).forEach(k => setError(k, ''));
        formError.hidden = true;
        modal.show();
    }));

    modalEl.addEventListener('shown.bs.modal', () => { fields.sold.focus(); fields.sold.select(); });

    form.addEventListener('submit', async function (e) {
        e.preventDefault();
        if (!validate()) return;

        submit.disabled = true;
        formError.hidden = true;
        const result = await post('/Goals/Save', {
            seller: document.getElementById('goalSeller').value,
            year: document.getElementById('goalYear').value,
            month: document.getElementById('goalMonth').value,
            salesAmountTarget: fields.sold.value.trim(),
            collectedAmountTarget: fields.collected.value.trim(),
            salesCountTarget: fields.count.value.trim(),
            rowVersion: document.getElementById('goalRowVersion').value
        });
        submit.disabled = false;

        if (result.success) {
            modal.hide();
            location.reload();
        } else {
            formError.textContent = result.message || 'No se pudo guardar la meta.';
            formError.hidden = false;
        }
    });

    // ───────── Copiar las metas del mes anterior ─────────

    const copyBtn = document.getElementById('goalCopyBtn');
    if (copyBtn) {
        copyBtn.addEventListener('click', async function () {
            const confirm = await Swal.fire({
                title: `¿Copiar las metas de ${copyBtn.dataset.prevLabel}?`,
                html: `Se copian a ${copyBtn.dataset.monthLabel} solo para los vendedores que todavía no tienen meta.<br><small>Las metas que ya definiste no se tocan.</small>`,
                icon: 'question',
                showCancelButton: true,
                confirmButtonText: 'Copiar',
                cancelButtonText: 'Cancelar',
                confirmButtonColor: brandColor
            });
            if (!confirm.isConfirmed) return;

            const result = await post('/Goals/CopyPrevious', { year: copyBtn.dataset.year, month: copyBtn.dataset.month });
            if (result.success) {
                await Swal.fire({
                    icon: 'success',
                    title: result.copied === 1 ? 'Se copió 1 meta' : `Se copiaron ${result.copied} metas`,
                    timer: 1500,
                    showConfirmButton: false
                });
                location.reload();
            } else {
                Swal.fire({ icon: 'info', title: 'No se copiaron metas', text: result.message, confirmButtonColor: brandColor });
            }
        });
    }
});
