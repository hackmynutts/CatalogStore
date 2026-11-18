// Catálogo público de la landing (Views/Home/PublicCatalog.cshtml): filtros, lista de consulta guardada en el navegador con la cantidad
// de cada producto, descarga en PDF (con leyenda previa) y mensaje de WhatsApp al vendedor que elija el visitante.
// No hay precios ni sesión; el servidor revalida los productos y las cantidades al generar el PDF.
(function () {
    const MAX_LINES = 80, MAX_QTY = 9999, KEY = 'lp-consulta-v3', OLD_KEY = 'lp-consulta-v2', SELLER_KEY = 'lp-vendedor';
    const GENERAL = 'Atención general'; // misma etiqueta que CatalogSeller.Options en el servidor
    const $ = (id) => document.getElementById(id);
    const section = $('catalogo');
    if (!section) return;

    const cards = Array.from(section.querySelectorAll('.lp-pcard'));

    // Fotos verticales: se muestran completas (contain) en vez de recortarlas; las demás llenan el recuadro (cover).
    function fitImage(img) {
        if (img.naturalWidth && img.naturalHeight / img.naturalWidth > 1.2) img.classList.add('is-portrait');
    }
    section.querySelectorAll('.lp-pcard-media img:not(.lp-pcard-fallback)').forEach((img) => {
        if (img.complete) fitImage(img);
        else img.addEventListener('load', () => fitImage(img), { once: true });
    });

    const info = new Map(cards.map((c) => [c.dataset.id, { id: c.dataset.id, name: c.dataset.name, code: c.dataset.code }]));
    const clampQty = (value) => {
        const n = parseInt(value, 10);
        return Number.isFinite(n) ? Math.min(MAX_QTY, Math.max(1, n)) : 1;
    };

    // Lista elegida: [{ id, qty }] en localStorage; si el navegador no deja guardar, vive solo en esta página.
    // La lista de la versión anterior (solo ids) se migra con cantidad 1.
    let list = [];
    try {
        let saved = JSON.parse(localStorage.getItem(KEY) || 'null');
        if (!Array.isArray(saved)) {
            const old = JSON.parse(localStorage.getItem(OLD_KEY) || '[]');
            saved = Array.isArray(old) ? old.map((id) => ({ id: String(id), qty: 1 })) : [];
        }
        const seen = new Set();
        list = saved
            .map((x) => ({ id: String(x && x.id), qty: clampQty(x && x.qty) }))
            .filter((x) => info.has(x.id) && !seen.has(x.id) && seen.add(x.id));
    } catch { list = []; }
    const save = () => { try { localStorage.setItem(KEY, JSON.stringify(list)); } catch { /* sin almacenamiento: se sigue en memoria */ } };
    const find = (id) => list.find((x) => x.id === id);
    const units = () => list.reduce((sum, x) => sum + x.qty, 0);

    function setQty(id, value) {
        const line = find(id);
        if (!line) return;
        line.qty = clampQty(value);
        save(); render();
    }

    // A quién se le escribe: con una sola opción va directo; con varias hay un selector (#lpSeller) y hay que elegir.
    const sellerSelect = $('lpSeller');
    function currentContact() {
        if (sellerSelect) {
            const opt = sellerSelect.selectedOptions[0];
            return opt && opt.value ? { number: opt.value, name: opt.dataset.name || '' } : null;
        }
        return section.dataset.whatsapp ? { number: section.dataset.whatsapp, name: section.dataset.contactName || '' } : null;
    }
    if (sellerSelect) {
        try {
            const savedSeller = localStorage.getItem(SELLER_KEY);
            if (savedSeller && Array.from(sellerSelect.options).some((o) => o.value === savedSeller)) sellerSelect.value = savedSeller;
        } catch { /* sin almacenamiento */ }
        sellerSelect.addEventListener('change', () => {
            try { localStorage.setItem(SELLER_KEY, sellerSelect.value); } catch { /* sin almacenamiento */ }
            render();
        });
    }

    // Filtros
    let category = 'all';
    function applyFilters() {
        const term = $('lpSearch').value.trim().toLowerCase();
        let shown = 0;
        for (const card of cards) {
            const match = (category === 'all' || card.dataset.category === category)
                && (!term || card.dataset.name.toLowerCase().includes(term) || card.dataset.code.toLowerCase().includes(term));
            card.hidden = !match;
            if (match) shown++;
        }
        $('lpEmpty').hidden = shown > 0;
    }
    $('lpSearch').addEventListener('input', applyFilters);
    section.querySelectorAll('.lp-pill').forEach((pill) => pill.addEventListener('click', () => {
        category = pill.dataset.category;
        section.querySelectorAll('.lp-pill').forEach((p) => p.classList.toggle('is-active', p === pill));
        applyFilters();
    }));

    // Tarjetas: elegir o quitar, y cambiar la cantidad
    $('lpGrid').addEventListener('click', (e) => {
        const step = e.target.closest('[data-qty-step]');
        if (step) {
            const id = step.closest('.lp-pcard').dataset.id;
            const line = find(id);
            if (line) setQty(id, line.qty + parseInt(step.dataset.qtyStep, 10));
            return;
        }
        const btn = e.target.closest('[data-toggle]');
        if (!btn) return;
        const id = btn.closest('.lp-pcard').dataset.id;
        if (find(id)) list = list.filter((x) => x.id !== id);
        else if (list.length >= MAX_LINES) { alert('La lista admite hasta ' + MAX_LINES + ' productos.'); return; }
        else list.push({ id, qty: 1 });
        save(); render();
    });
    $('lpGrid').addEventListener('change', (e) => {
        const input = e.target.closest('[data-card-qty]');
        if (input) setQty(input.closest('.lp-pcard').dataset.id, input.value);
    });

    // Panel de la lista
    const fab = $('lpListFab'), panel = $('lpList'), items = $('lpListItems');
    function render() {
        $('lpListCount').textContent = list.length;
        fab.hidden = list.length === 0 && panel.hidden;
        $('lpListEmpty').hidden = list.length > 0;
        for (const card of cards) {
            const line = find(card.dataset.id);
            const btn = card.querySelector('[data-toggle]');
            btn.classList.toggle('is-selected', !!line);
            btn.setAttribute('aria-pressed', !!line);
            btn.textContent = line ? 'Quitar de mi lista' : 'Agregar a mi lista';
            const qtyRow = card.querySelector('.lp-pcard-qty');
            if (qtyRow) {
                qtyRow.hidden = !line;
                const input = qtyRow.querySelector('input');
                if (line && document.activeElement !== input) input.value = line.qty;
            }
        }
        items.innerHTML = '';
        for (const line of list) {
            const p = info.get(line.id);
            const li = document.createElement('li');
            const name = document.createElement('span');
            name.className = 'lp-li-name';
            name.textContent = p.name;
            if (p.code) { const small = document.createElement('small'); small.textContent = 'Código ' + p.code; name.appendChild(small); }

            const qty = document.createElement('div');
            qty.className = 'lp-qty lp-qty-sm';
            qty.setAttribute('role', 'group');
            qty.setAttribute('aria-label', 'Cantidad de ' + p.name);
            const minus = document.createElement('button');
            minus.type = 'button'; minus.setAttribute('aria-label', 'Restar una unidad de ' + p.name);
            minus.innerHTML = '<i class="fa-solid fa-minus" aria-hidden="true"></i>';
            minus.addEventListener('click', () => setQty(line.id, line.qty - 1));
            const input = document.createElement('input');
            input.type = 'number'; input.min = '1'; input.max = String(MAX_QTY); input.step = '1'; input.value = line.qty;
            input.inputMode = 'numeric'; input.setAttribute('aria-label', 'Cantidad de ' + p.name);
            input.addEventListener('change', () => setQty(line.id, input.value));
            const plus = document.createElement('button');
            plus.type = 'button'; plus.setAttribute('aria-label', 'Sumar una unidad de ' + p.name);
            plus.innerHTML = '<i class="fa-solid fa-plus" aria-hidden="true"></i>';
            plus.addEventListener('click', () => setQty(line.id, line.qty + 1));
            qty.append(minus, input, plus);

            const remove = document.createElement('button');
            remove.type = 'button'; remove.className = 'lp-li-remove';
            remove.setAttribute('aria-label', 'Quitar ' + p.name);
            remove.innerHTML = '<i class="fa-solid fa-trash-can" aria-hidden="true"></i>';
            remove.addEventListener('click', () => { list = list.filter((x) => x.id !== line.id); save(); render(); });
            li.append(name, qty, remove);
            items.appendChild(li);
        }
        $('lpPdfBtn').disabled = list.length === 0;

        // WhatsApp: necesita productos y, si hay varios vendedores, que se haya elegido uno.
        const w = $('lpWhatsBtn');
        if (w) {
            const contact = currentContact();
            const ready = list.length > 0 && !!contact;
            w.setAttribute('aria-disabled', !ready);
            w.style.pointerEvents = ready ? '' : 'none';
            w.style.opacity = ready ? '' : '.5';
            $('lpWhatsLabel').textContent = contact && contact.name && sellerSelect
                ? 'Enviar a ' + (contact.name === GENERAL ? 'atención general' : contact.name) + ' por WhatsApp'
                : (sellerSelect ? 'Elegí un vendedor para enviar' : 'Enviar por WhatsApp');
        }
    }
    function toggle(open) {
        panel.hidden = !open;
        fab.setAttribute('aria-expanded', open);
        if (open) $('lpListClose').focus(); else fab.focus();
        render();
    }
    fab.addEventListener('click', () => toggle(panel.hidden));
    $('lpListClose').addEventListener('click', () => toggle(false));
    document.addEventListener('keydown', (e) => { if (e.key === 'Escape' && !panel.hidden && !$('lpDisclaimer').open) toggle(false); });
    $('lpListClear').addEventListener('click', () => { list = []; save(); render(); });

    // PDF: primero la leyenda ("solo consulta, no garantiza disponibilidad"); al aceptar se envía el formulario con los ids y cantidades.
    const dialog = $('lpDisclaimer'), form = $('lpPdfForm');
    form.addEventListener('submit', (e) => {
        if (form.dataset.confirmed === '1') { form.dataset.confirmed = ''; return; }
        e.preventDefault();
        if (list.length === 0) return;
        dialog.showModal();
    });
    $('lpDisclaimerCancel').addEventListener('click', () => dialog.close());
    $('lpDisclaimerOk').addEventListener('click', () => {
        dialog.close();
        $('lpPdfItems').value = list.map((x) => x.id + 'x' + x.qty).join(',');
        $('lpPdfCustomer').value = $('lpCustomer').value.trim();
        const contact = currentContact();
        $('lpPdfSeller').value = contact ? contact.name : '';
        form.dataset.confirmed = '1';
        form.requestSubmit();
    });

    // WhatsApp: el enlace wa.me no puede adjuntar archivos, así que lleva la lista escrita (con cantidades) en el mensaje,
    // y va al número del vendedor elegido.
    const whatsBtn = $('lpWhatsBtn');
    if (whatsBtn) {
        whatsBtn.addEventListener('click', (e) => {
            const contact = currentContact();
            if (list.length === 0 || !contact) { e.preventDefault(); return; }
            const who = $('lpCustomer').value.trim();
            const lines = list.map((x) => { const p = info.get(x.id); return '• ' + x.qty + ' × ' + p.name + (p.code ? ' (' + p.code + ')' : ''); });
            const greeting = contact.name && sellerSelect && contact.name !== GENERAL ? 'Hola ' + contact.name + ', ' : 'Hola, ';
            const text = greeting + (who ? 'soy ' + who + ' y ' : '') + 'quisiera consultar disponibilidad y precio de:\n' + lines.join('\n');
            whatsBtn.href = 'https://wa.me/' + contact.number + '?text=' + encodeURIComponent(text);
        });
    }

    render();
})();
