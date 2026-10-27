// CABYS de productos (Views/Product/Cabys.cshtml): buscar en el catálogo oficial de Hacienda y asignar el código
// a un producto o a toda una categoría. Cada acción llama a ProductController (UI) y recarga la página.
document.addEventListener('DOMContentLoaded', function () {
    const tokenInput = document.querySelector('#cabysTokenForm input[name="__RequestVerificationToken"]');
    if (!tokenInput) return;
    const token = tokenInput.value;
    const brandColor = getComputedStyle(document.documentElement).getPropertyValue('--brand-primary').trim() || undefined;

    function escapeHtml(value) {
        const div = document.createElement('div');
        div.textContent = value ?? '';
        return div.innerHTML.replace(/"/g, '&quot;');
    }

    async function post(url, data) {
        const body = new FormData();
        body.append('__RequestVerificationToken', token);
        Object.entries(data).forEach(([k, v]) => body.append(k, v ?? ''));
        const response = await fetch(url, { method: 'POST', body });
        if (!response.ok) return { success: false, message: 'Error de comunicación con el servidor.' };
        return response.json();
    }

    // Selector del catálogo: el usuario escribe, se busca con una pausa y elige un resultado.
    // Devuelve { code, description, taxRate } o null si cancela.
    async function pickCabys(title, intro) {
        let selected = null;
        let timer = null;
        let counter = 0; // descarta respuestas viejas si el usuario sigue escribiendo

        const result = await Swal.fire({
            title,
            width: 720,
            showCancelButton: true,
            confirmButtonText: 'Usar este código',
            cancelButtonText: 'Cancelar',
            confirmButtonColor: brandColor,
            html: `
                <p class="cabys-picker-intro">${intro}</p>
                <input type="search" id="cabysQuery" class="swal2-input cabys-picker-input" placeholder="Escribí lo que es el producto (ej. casco, cadena, guantes)" autocomplete="off" aria-label="Buscar en el catálogo CABYS">
                <div id="cabysResults" class="cabys-picker-results" role="listbox" aria-label="Resultados"></div>
                <div id="cabysTax" class="cabys-picker-tax" role="status"></div>`,
            didOpen: () => {
                const query = document.getElementById('cabysQuery');
                const list = document.getElementById('cabysResults');
                const tax = document.getElementById('cabysTax');
                Swal.getConfirmButton().disabled = true;
                query.focus();

                function setSelected(item) {
                    selected = item;
                    Swal.getConfirmButton().disabled = !item;
                    tax.textContent = item && item.taxRate !== 13
                        ? `Este código lleva IVA de ${item.taxRate} %. El sistema hoy calcula 13 % en todos los productos.`
                        : '';
                }

                async function search() {
                    const text = query.value.trim();
                    const mine = ++counter;
                    setSelected(null);
                    if (text.length < 3) { list.innerHTML = '<div class="cabys-picker-empty">Escribí al menos 3 letras.</div>'; return; }
                    list.innerHTML = '<div class="cabys-picker-empty">Buscando…</div>';
                    let data;
                    try {
                        data = await (await fetch('/Product/CabysSearch?q=' + encodeURIComponent(text))).json();
                    } catch { data = { success: false, message: 'No se pudo consultar el catálogo.' }; }
                    if (mine !== counter) return;
                    if (!data.success) { list.innerHTML = `<div class="cabys-picker-empty">${escapeHtml(data.message)}</div>`; return; }
                    if (!data.items.length) { list.innerHTML = '<div class="cabys-picker-empty">Sin resultados. Probá con otra palabra.</div>'; return; }

                    list.innerHTML = data.items.map((it, i) => `
                        <label class="cabys-option">
                            <input type="radio" name="cabysPick" value="${i}">
                            <span class="cabys-option-body">
                                <span class="cabys-option-code">${escapeHtml(it.code)} <span class="cabys-option-tax">IVA ${it.taxRate} %</span></span>
                                <span class="cabys-option-desc">${escapeHtml(it.description)}</span>
                                <span class="cabys-option-cat">${escapeHtml(it.category)}</span>
                            </span>
                        </label>`).join('');
                    list.querySelectorAll('input[name="cabysPick"]').forEach(r =>
                        r.addEventListener('change', () => setSelected(data.items[Number(r.value)])));
                }

                query.addEventListener('input', () => { clearTimeout(timer); timer = setTimeout(search, 400); });
                query.addEventListener('keydown', e => { if (e.key === 'Enter') { e.preventDefault(); clearTimeout(timer); search(); } });
                list.innerHTML = '<div class="cabys-picker-empty">Escribí al menos 3 letras.</div>';
            },
            preConfirm: () => selected
        });

        return result.isConfirmed ? result.value : null;
    }

    async function finish(result, okTitle) {
        if (result.success) {
            await Swal.fire({ icon: 'success', title: okTitle, timer: 1200, showConfirmButton: false });
            location.reload();
        } else {
            Swal.fire({ icon: 'error', title: 'No se pudo guardar', text: result.message, confirmButtonColor: brandColor });
        }
    }

    // ───────── Un producto ─────────

    document.querySelectorAll('.cabys-set-btn').forEach(btn => btn.addEventListener('click', async function () {
        const picked = await pickCabys('CABYS de ' + escapeHtml(btn.dataset.name), 'Elegí el código que describe este producto en el catálogo oficial.');
        if (!picked) return;
        finish(await post('/Product/SetCabys', { id: btn.dataset.id, cabysCode: picked.code }), 'CABYS asignado');
    }));

    document.querySelectorAll('.cabys-clear-btn').forEach(btn => btn.addEventListener('click', async function () {
        const confirm = await Swal.fire({
            title: '¿Quitar el CABYS?',
            text: btn.dataset.name,
            icon: 'warning',
            showCancelButton: true,
            confirmButtonText: 'Quitar',
            cancelButtonText: 'Cancelar',
            confirmButtonColor: '#dc3545'
        });
        if (confirm.isConfirmed) finish(await post('/Product/SetCabys', { id: btn.dataset.id, cabysCode: '' }), 'CABYS quitado');
    }));

    // ───────── Una categoría completa ─────────

    document.querySelectorAll('.cabys-bulk-btn').forEach(btn => btn.addEventListener('click', async function () {
        const label = btn.dataset.label;
        const total = Number(btn.dataset.total);
        const missing = Number(btn.dataset.missing);
        const picked = await pickCabys('CABYS para ' + escapeHtml(label), `Se asigna a los productos de esta categoría (${total}). Elegí el código común.`);
        if (!picked) return;

        // Si todos tienen CABYS ya no hay nada "vacío": la única opción es reemplazar.
        let replace = missing === 0;
        if (missing > 0 && missing < total) {
            const choice = await Swal.fire({
                title: '¿A cuáles productos?',
                html: `${total - missing} de ${total} productos de <strong>${escapeHtml(label)}</strong> ya tienen un CABYS.`,
                icon: 'question',
                showDenyButton: true,
                showCancelButton: true,
                confirmButtonText: `Solo a los ${missing} sin CABYS`,
                denyButtonText: `A los ${total} (reemplaza los que tienen)`,
                cancelButtonText: 'Cancelar',
                confirmButtonColor: brandColor
            });
            if (choice.isDismissed) return;
            replace = choice.isDenied;
        } else {
            const ok = await Swal.fire({
                title: `¿Asignar ${picked.code}?`,
                html: `A <strong>${missing === 0 ? total : missing}</strong> productos de <strong>${escapeHtml(label)}</strong>.<br><small>${escapeHtml(picked.description)}</small>`,
                icon: 'question',
                showCancelButton: true,
                confirmButtonText: 'Asignar',
                cancelButtonText: 'Cancelar',
                confirmButtonColor: brandColor
            });
            if (!ok.isConfirmed) return;
        }

        const result = await post('/Product/SetCabysBulk', { category: btn.dataset.category, cabysCode: picked.code, onlyEmpty: replace ? 'false' : 'true' });
        finish(result, result.success ? `Se actualizaron ${result.count} productos` : '');
    }));

    // ───────── Filtro: solo los que faltan (en el servidor, para que la paginación cuente bien) ─────────

    const onlyMissing = document.getElementById('onlyMissing');
    if (onlyMissing) {
        onlyMissing.addEventListener('change', () => {
            location.search = onlyMissing.checked ? '?onlyMissing=true' : '';
        });
    }
});
