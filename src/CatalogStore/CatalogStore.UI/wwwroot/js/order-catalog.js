// Integración del catálogo con las órdenes (Views/Product/Catalog.cshtml).
// - Barra de "orden activa" arriba del catálogo y panel lateral (offcanvas) con sus líneas.
// - Botón "+" en cada tarjeta: agrega 1 unidad a la orden activa; si no hay, pide el cliente y la crea.
// Todo pasa por OrderController (UI), que llama a la API. Los montos vienen del backend: aquí no se calcula nada.
document.addEventListener('DOMContentLoaded', function () {
    const bar = document.getElementById('orderBar');
    if (!bar) return;

    const token = document.querySelector('#orderTokenForm input[name="__RequestVerificationToken"]').value;
    const panelEl = document.getElementById('orderPanel');
    const panel = bootstrap.Offcanvas.getOrCreateInstance(panelEl);
    const panelTitle = document.getElementById('orderPanelTitle');
    const panelSub = document.getElementById('orderPanelSub');
    const panelBody = document.getElementById('orderPanelBody');
    const panelFooter = document.getElementById('orderPanelFooter');
    const clientModalEl = document.getElementById('orderClientModal');
    const clientModal = bootstrap.Modal.getOrCreateInstance(clientModalEl);
    const clientSearch = document.getElementById('orderClientSearch');
    const clientList = document.getElementById('orderClientList');
    const clientHint = document.getElementById('orderClientHint');

    // Mismo formato que el servidor usa en las tarjetas (ToString("N2")): ₡3,500.00
    const money = new Intl.NumberFormat('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    const fmt = v => '₡' + money.format(v);

    let order = null;          // orden activa (JSON de /Order/Active) o null
    let pendingAddBtn = null;  // "+" que quedó esperando a que se elija el cliente
    let clients = null;        // cache de /Order/Clients
    let busy = false;

    // ───────── utilidades ─────────

    function esc(value) {
        return String(value ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    }

    async function post(url, data) {
        const body = new FormData();
        body.append('__RequestVerificationToken', token);
        Object.entries(data || {}).forEach(([k, v]) => body.append(k, v));
        const response = await fetch(url, { method: 'POST', body });
        if (!response.ok) return { success: false, message: 'Error de comunicación con el servidor.' };
        return response.json();
    }

    const toast = Swal.mixin({ toast: true, position: 'bottom-end', showConfirmButton: false, timer: 2500, timerProgressBar: true });

    function showError(title, message) {
        Swal.fire({ icon: 'error', title, text: message || 'Intentá de nuevo.' });
    }

    function setBusy(value) {
        busy = value;
        panelEl.classList.toggle('is-busy', value);
        panelEl.setAttribute('aria-busy', value ? 'true' : 'false');
    }

    function lineCountText(n) {
        return n === 1 ? '1 producto' : `${n} productos`;
    }

    // ───────── carga y render ─────────

    async function loadOrder() {
        const response = await fetch('/Order/Active');
        order = response.status === 200 ? await response.json() : null;
        render();
    }

    function render() {
        renderBar();
        renderPanel();
        renderCardBadges();
    }

    function renderBar() {
        if (!order) {
            bar.classList.remove('has-order');
            bar.innerHTML = `
                <div class="order-bar-icon"><i class="fa-solid fa-cart-plus" aria-hidden="true"></i></div>
                <div class="order-bar-info">
                    <span class="order-bar-title">Sin orden activa</span>
                    <span class="order-bar-meta">Al agregar un producto vas a elegir el cliente.</span>
                </div>
                <div class="order-bar-actions">
                    <button type="button" class="btn btn-sm btn-outline-brand" data-action="pick-client">
                        <i class="fa-solid fa-user-plus" aria-hidden="true"></i> Elegir cliente
                    </button>
                </div>`;
            return;
        }

        const count = order.lines.length;
        const cc = order.clientCC ? ` <span class="order-bar-cc">(${esc(order.clientCC)})</span>` : '';
        bar.classList.add('has-order');
        bar.innerHTML = `
            <div class="order-bar-icon"><i class="fa-solid fa-cart-shopping" aria-hidden="true"></i></div>
            <div class="order-bar-info">
                <span class="order-bar-title">${esc(order.orderNumber)} · ${esc(order.clientName)}${cc}</span>
                <span class="order-bar-meta">${lineCountText(count)} · <strong>${fmt(order.orderTotalAmount)}</strong>${order.applyIva ? '' : ' · sin IVA'}</span>
            </div>
            <div class="order-bar-actions">
                <button type="button" class="btn btn-sm btn-brand btn-brand-auto" data-action="open-panel">
                    <i class="fa-solid fa-list-check" aria-hidden="true"></i> Ver orden
                    <span class="order-bar-count">${count}</span>
                </button>
                <button type="button" class="btn btn-sm btn-outline-secondary order-bar-release" data-action="release"
                        aria-label="Dejar de usar esta orden" title="Dejar de usar esta orden (sigue en proceso)">
                    <i class="fa-solid fa-xmark" aria-hidden="true"></i>
                </button>
            </div>`;
    }

    function renderPanel() {
        if (!order) {
            panelTitle.textContent = 'Orden';
            panelSub.textContent = '';
            panelBody.innerHTML = '';
            panelFooter.innerHTML = '';
            return;
        }

        panelTitle.textContent = `Orden ${order.orderNumber}`;
        panelSub.innerHTML = `${esc(order.clientName)}${order.clientCC ? ' · ' + esc(order.clientCC) : ''} <span class="badge badge-status-active">${esc(order.orderStatusName)}</span>`;

        if (order.lines.length === 0) {
            panelBody.innerHTML = `
                <div class="app-table-empty order-panel-empty">
                    <i class="fa-solid fa-basket-shopping"></i>
                    <span>Todavía no hay productos. Usá el botón <strong>+</strong> de cada producto.</span>
                </div>`;
        } else {
            panelBody.innerHTML = `<ul class="order-lines">${order.lines.map(lineHtml).join('')}</ul>`;
        }

        panelFooter.innerHTML = `
            <div class="form-check form-switch order-iva-switch">
                <input class="form-check-input" type="checkbox" role="switch" id="orderApplyIva" ${order.applyIva ? 'checked' : ''}>
                <label class="form-check-label" for="orderApplyIva">Aplicar IVA</label>
            </div>
            ${order.lines.length === 0 ? '' : `
            <button type="button" class="btn btn-sm btn-outline-brand order-panel-discount" data-action="order-discount">
                <i class="fa-solid fa-percent" aria-hidden="true"></i> Descuento a toda la orden
            </button>`}
            <dl class="order-totals">
                <div><dt>Subtotal</dt><dd>${fmt(order.subtotal)}</dd></div>
                <div><dt>IVA</dt><dd>${fmt(order.ivaTotal)}</dd></div>
                <div class="order-totals-total"><dt>Total</dt><dd>${fmt(order.orderTotalAmount)}</dd></div>
            </dl>
            <button type="button" class="btn btn-brand" data-action="submit" ${order.lines.length === 0 ? 'disabled' : ''}>
                <i class="fa-solid fa-paper-plane" aria-hidden="true"></i> Enviar a revisión
            </button>
            <div class="order-panel-links">
                <a class="order-panel-release" href="/Order/Details/${order.orderID}">Ver detalle completo</a>
                <button type="button" class="btn btn-link order-panel-release" data-action="release">Dejar de usar esta orden</button>
            </div>`;
    }

    function lineHtml(l) {
        const discount = l.discount > 0 ? ` · <span class="order-line-discount">Desc. ${money.format(l.discount)} %</span>` : '';
        return `
            <li class="order-line" data-line-id="${l.orderLineID}">
                <div class="order-line-info">
                    <span class="order-line-name">${esc(l.productName)}</span>
                    <span class="order-line-meta">${esc(l.productCode || '')} · ${fmt(l.unitPrice)} c/u${discount}</span>
                </div>
                <div class="order-line-actions">
                    <div class="qty-stepper">
                        <button type="button" data-action="dec" aria-label="Restar uno a ${esc(l.productName)}" ${l.quantity <= 1 ? 'disabled' : ''}>
                            <i class="fa-solid fa-minus" aria-hidden="true"></i>
                        </button>
                        <input type="number" min="1" max="1000000" step="1" value="${l.quantity}" data-action="qty" aria-label="Cantidad de ${esc(l.productName)}" inputmode="numeric">
                        <button type="button" data-action="inc" aria-label="Sumar uno a ${esc(l.productName)}">
                            <i class="fa-solid fa-plus" aria-hidden="true"></i>
                        </button>
                    </div>
                    <span class="order-line-total">${fmt(l.lineTotalPrice)}</span>
                    <button type="button" class="order-line-remove" data-action="remove" aria-label="Quitar ${esc(l.productName)}" title="Quitar">
                        <i class="fa-solid fa-trash-can" aria-hidden="true"></i>
                    </button>
                </div>
            </li>`;
    }

    // Insignia "×3" en las tarjetas de productos que ya están en la orden.
    function renderCardBadges() {
        const qtyByProduct = new Map((order?.lines || []).map(l => [String(l.productID), l.quantity]));
        document.querySelectorAll('.catalog-card-qty').forEach(badge => {
            const qty = qtyByProduct.get(badge.getAttribute('data-qty-for'));
            badge.hidden = !qty;
            badge.textContent = qty ? `×${qty}` : '';
            badge.closest('.catalog-card').classList.toggle('in-order', !!qty);
        });
    }

    function pulseBar() {
        bar.classList.remove('pulse');
        void bar.offsetWidth; // reinicia la animación
        bar.classList.add('pulse');
    }

    // ───────── agregar desde el catálogo ─────────

    async function addProduct(btn, clientId) {
        const icon = btn.querySelector('i');
        btn.disabled = true;
        btn.classList.add('is-loading');
        icon.className = 'fa-solid fa-spinner fa-spin';
        try {
            const data = { productId: btn.dataset.productId, quantity: 1 };
            if (clientId) data.clientId = clientId;
            const result = await post('/Order/AddItem', data);

            if (result.success) {
                await loadOrder();
                pulseBar();
                toast.fire({ icon: 'success', title: `${btn.dataset.productName} agregado a ${order ? order.orderNumber : 'la orden'}` });
            } else if (result.needsClient) {
                pendingAddBtn = btn;
                await loadOrder();
                openClientPicker(result.message);
            } else {
                showError('No se pudo agregar', result.message);
            }
        } finally {
            btn.disabled = false;
            btn.classList.remove('is-loading');
            icon.className = 'fa-solid fa-plus';
        }
    }

    document.getElementById('catalogGrid')?.addEventListener('click', function (e) {
        const btn = e.target.closest('.catalog-add-btn');
        if (!btn || btn.disabled) return;
        addProduct(btn, null);
    });

    // ───────── selector de cliente ─────────

    async function openClientPicker(notice) {
        clientHint.textContent = notice || (pendingAddBtn
            ? `Elegí el cliente para agregar "${pendingAddBtn.dataset.productName}". Si ya tiene una orden en proceso, se agrega a esa.`
            : 'Si el cliente ya tiene una orden en proceso, se abre esa.');
        clientSearch.value = '';
        clientModal.show();

        if (!clients) {
            clientList.innerHTML = '<div class="order-client-empty"><span class="spinner-border spinner-border-sm" aria-hidden="true"></span> Cargando clientes…</div>';
            const response = await fetch('/Order/Clients');
            const result = response.ok ? await response.json() : { success: false };
            if (!result.success) {
                clientList.innerHTML = `<div class="order-client-empty">${esc(result.message || 'No se pudo cargar la lista de clientes.')}</div>`;
                return;
            }
            clients = result.clients;
        }
        renderClients();
    }

    function renderClients() {
        const term = clientSearch.value.trim().toLowerCase();
        const filtered = clients.filter(c => !term
            || c.name.toLowerCase().includes(term)
            || (c.cc || '').toLowerCase().includes(term)
            || (c.identification || '').toLowerCase().includes(term));

        clientList.innerHTML = filtered.length === 0
            ? '<div class="order-client-empty">Ningún cliente coincide con la búsqueda.</div>'
            : filtered.slice(0, 100).map(c => `
                <button type="button" class="order-client-item" role="listitem" data-client-id="${c.id}">
                    <span class="order-client-name">${esc(c.name)}${c.cc ? ` <span class="order-client-cc">(${esc(c.cc)})</span>` : ''}</span>
                    <span class="order-client-id">Cédula ${esc(c.identification)}</span>
                </button>`).join('');
    }

    clientSearch.addEventListener('input', () => clients && renderClients());
    clientModalEl.addEventListener('shown.bs.modal', () => clientSearch.focus());
    clientModalEl.addEventListener('hidden.bs.modal', () => { pendingAddBtn = null; });

    clientList.addEventListener('click', async function (e) {
        const item = e.target.closest('.order-client-item');
        if (!item) return;
        const clientId = item.getAttribute('data-client-id');
        const btn = pendingAddBtn;
        pendingAddBtn = null;
        clientModal.hide();

        if (btn) {
            await addProduct(btn, clientId);
            return;
        }
        // "Elegir cliente" sin producto: abre (o crea vacía) la orden en proceso del cliente.
        const result = await post(`/Order/Start/${clientId}`, {});
        if (!result.success) {
            showError('No se pudo abrir la orden', result.message);
            return;
        }
        await loadOrder();
        pulseBar();
        toast.fire({ icon: 'success', title: `Orden ${order ? order.orderNumber : ''} lista para agregar productos` });
    });

    // ───────── acciones de la barra y del panel ─────────

    async function release() {
        const result = await post('/Order/Release', {});
        if (!result.success) return;
        const number = order?.orderNumber;
        order = null;
        panel.hide();
        render();
        toast.fire({ icon: 'info', title: `${number} sigue en proceso; ya no se le agregan productos desde aquí` });
    }

    async function runLineAction(fn) {
        if (busy) return;
        setBusy(true);
        try {
            const result = await fn();
            if (!result.success) showError('No se pudo actualizar la orden', result.message);
            await loadOrder();
            return result;
        } finally {
            setBusy(false);
        }
    }

    function findLine(el) {
        const li = el.closest('.order-line');
        return li ? order.lines.find(l => String(l.orderLineID) === li.getAttribute('data-line-id')) : null;
    }

    function updateQty(line, quantity) {
        if (!Number.isInteger(quantity) || quantity < 1) { render(); return; }
        if (quantity === line.quantity) return;
        runLineAction(() => post('/Order/UpdateLine', {
            orderId: order.orderID, lineId: line.orderLineID, quantity, discount: String(line.discount)
        }));
    }

    bar.addEventListener('click', function (e) {
        const action = e.target.closest('[data-action]')?.getAttribute('data-action');
        if (action === 'open-panel') panel.show();
        else if (action === 'release') release();
        else if (action === 'pick-client') { pendingAddBtn = null; openClientPicker(); }
    });

    panelEl.addEventListener('click', async function (e) {
        const el = e.target.closest('[data-action]');
        if (!el || !order) return;
        const action = el.getAttribute('data-action');
        const line = findLine(el);

        if (action === 'inc' && line) updateQty(line, line.quantity + 1);
        else if (action === 'dec' && line) updateQty(line, line.quantity - 1);
        else if (action === 'remove' && line) {
            runLineAction(() => post('/Order/RemoveLine', { orderId: order.orderID, lineId: line.orderLineID }));
        }
        else if (action === 'release') release();
        else if (action === 'order-discount') {
            if (busy) return;
            const discount = await OrderDiscount.ask({
                lines: order.lines.map(l => ({ name: l.productName, discount: l.discount })),
                maxDiscount: Number(bar.dataset.maxDiscount),
                confirmButtonColor: getComputedStyle(document.documentElement).getPropertyValue('--brand-primary').trim() || undefined
            });
            if (discount === null) return;
            const result = await runLineAction(() => post('/Order/SetDiscount', { orderId: order.orderID, discount: String(discount) }));
            if (result?.success) toast.fire({ icon: 'success', title: discount === 0 ? 'Descuentos quitados' : `Descuento de ${discount} % aplicado` });
        }
        else if (action === 'submit') {
            const confirm = await Swal.fire({
                icon: 'question',
                title: `¿Enviar ${order.orderNumber} a revisión?`,
                html: `Total <strong>${fmt(order.orderTotalAmount)}</strong>. Los precios se actualizan con los vigentes y la orden queda bloqueada hasta que se complete o se devuelva a edición.`,
                showCancelButton: true,
                confirmButtonText: 'Enviar a revisión',
                cancelButtonText: 'Seguir editando',
                confirmButtonColor: getComputedStyle(document.documentElement).getPropertyValue('--brand-primary').trim() || undefined
            });
            if (!confirm.isConfirmed) return;

            setBusy(true);
            try {
                const number = order.orderNumber;
                const submittedId = order.orderID;
                const result = await post('/Order/Submit', { orderId: submittedId });
                if (!result.success) { showError('No se pudo enviar a revisión', result.message); await loadOrder(); return; }
                panel.hide();
                await loadOrder();
                const done = await Swal.fire({
                    icon: 'success',
                    title: `${number} enviada a revisión`,
                    text: 'Queda pendiente para validarla y completarla. La encontrás en Operación › Ventas › Órdenes.',
                    showCancelButton: true,
                    confirmButtonText: 'Ver orden',
                    cancelButtonText: 'Seguir en el catálogo',
                    confirmButtonColor: getComputedStyle(document.documentElement).getPropertyValue('--brand-primary').trim() || undefined
                });
                if (done.isConfirmed) location.href = `/Order/Details/${submittedId}`;
            } finally {
                setBusy(false);
            }
        }
    });

    panelEl.addEventListener('change', function (e) {
        if (!order) return;
        if (e.target.id === 'orderApplyIva') {
            const applyIva = e.target.checked;
            runLineAction(() => post('/Order/SetIva', { orderId: order.orderID, applyIva }));
            return;
        }
        if (e.target.getAttribute('data-action') === 'qty') {
            const line = findLine(e.target);
            if (line) updateQty(line, Number(e.target.value));
        }
    });

    // Enter en el campo de cantidad confirma sin esperar a que pierda el foco.
    panelEl.addEventListener('keydown', function (e) {
        if (e.key === 'Enter' && e.target.getAttribute('data-action') === 'qty') {
            e.preventDefault();
            e.target.blur();
        }
    });

    loadOrder();
});
