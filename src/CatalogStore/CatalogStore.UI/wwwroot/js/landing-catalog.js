// Catálogo público de la landing (Views/Home/Index.cshtml): filtros, lista de consulta guardada en el navegador,
// descarga en PDF (con leyenda previa) y mensaje de WhatsApp. No hay precios, cantidades ni sesión;
// el servidor revalida los productos al generar el PDF.
(function () {
    const MAX_LINES = 80, KEY = 'lp-consulta-v2';
    const $ = (id) => document.getElementById(id);
    const section = $('catalogo');
    if (!section) return;

    const cards = Array.from(section.querySelectorAll('.lp-pcard'));
    const info = new Map(cards.map((c) => [c.dataset.id, { id: c.dataset.id, name: c.dataset.name, code: c.dataset.code }]));
    const whatsApp = section.dataset.whatsapp || '';

    // Lista de ids elegidos en localStorage; si el navegador no deja guardar, vive solo en esta página.
    let list = [];
    try {
        const saved = JSON.parse(localStorage.getItem(KEY) || '[]');
        if (Array.isArray(saved)) list = saved.filter((id, i) => info.has(String(id)) && saved.indexOf(id) === i).map(String);
    } catch { list = []; }
    const save = () => { try { localStorage.setItem(KEY, JSON.stringify(list)); } catch { /* sin almacenamiento: se sigue en memoria */ } };

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

    // Tarjetas: elegir o quitar
    $('lpGrid').addEventListener('click', (e) => {
        const btn = e.target.closest('[data-toggle]');
        if (!btn) return;
        const id = btn.closest('.lp-pcard').dataset.id;
        if (list.includes(id)) list = list.filter((x) => x !== id);
        else if (list.length >= MAX_LINES) { alert('La lista admite hasta ' + MAX_LINES + ' productos.'); return; }
        else list.push(id);
        save(); render();
    });

    // Panel de la lista
    const fab = $('lpListFab'), panel = $('lpList'), items = $('lpListItems');
    function render() {
        $('lpListCount').textContent = list.length;
        fab.hidden = list.length === 0 && panel.hidden;
        $('lpListEmpty').hidden = list.length > 0;
        for (const card of cards) {
            const on = list.includes(card.dataset.id);
            const btn = card.querySelector('[data-toggle]');
            btn.classList.toggle('is-selected', on);
            btn.setAttribute('aria-pressed', on);
            btn.textContent = on ? 'Quitar de mi lista' : 'Agregar a mi lista';
        }
        items.innerHTML = '';
        for (const id of list) {
            const p = info.get(id);
            const li = document.createElement('li');
            const name = document.createElement('span');
            name.className = 'lp-li-name';
            name.textContent = p.name;
            if (p.code) { const small = document.createElement('small'); small.textContent = 'Código ' + p.code; name.appendChild(small); }
            const remove = document.createElement('button');
            remove.type = 'button'; remove.className = 'lp-li-remove';
            remove.setAttribute('aria-label', 'Quitar ' + p.name);
            remove.innerHTML = '<i class="fa-solid fa-trash-can" aria-hidden="true"></i>';
            remove.addEventListener('click', () => { list = list.filter((x) => x !== id); save(); render(); });
            li.append(name, remove);
            items.appendChild(li);
        }
        $('lpPdfBtn').disabled = list.length === 0;
        const w = $('lpWhatsBtn');
        if (w) { w.setAttribute('aria-disabled', list.length === 0); w.style.pointerEvents = list.length === 0 ? 'none' : ''; w.style.opacity = list.length === 0 ? '.5' : ''; }
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

    // PDF: primero la leyenda ("solo consulta, no garantiza disponibilidad"); al aceptar se envía el formulario con los ids.
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
        $('lpPdfItems').value = list.join(',');
        $('lpPdfCustomer').value = $('lpCustomer').value.trim();
        form.dataset.confirmed = '1';
        form.requestSubmit();
    });

    // WhatsApp: el enlace wa.me no puede adjuntar archivos, así que lleva la lista escrita en el mensaje.
    const whatsBtn = $('lpWhatsBtn');
    if (whatsBtn && whatsApp) {
        whatsBtn.addEventListener('click', (e) => {
            if (list.length === 0) { e.preventDefault(); return; }
            const who = $('lpCustomer').value.trim();
            const lines = list.map((id) => { const p = info.get(id); return '• ' + p.name + (p.code ? ' (' + p.code + ')' : ''); });
            const text = 'Hola, ' + (who ? 'soy ' + who + ' y ' : '') + 'quisiera consultar disponibilidad y precio de:\n' + lines.join('\n');
            whatsBtn.href = 'https://wa.me/' + whatsApp + '?text=' + encodeURIComponent(text);
        });
    }

    render();
})();
