// Líneas de inventario. Se usa en dos páginas:
// - Detalle de la bodega (Views/Inventory/Details.cshtml): la tabla se carga por AJAX y se vuelve a pedir
//   después de cada cambio, sin recargar la página.
// - Detalle de la línea (Views/InventoryLine/Details.cshtml): solo el botón Editar; al guardar se recarga la página.
document.addEventListener('DOMContentLoaded', function () {
    const container = document.getElementById('inventory-lines');
    const modalEl = document.getElementById('lineModal');
    if (!container && !modalEl) return;

    const inventoryId = container ? container.getAttribute('data-inventory-id') : null;
    // getOrCreateInstance: en el detalle de la línea, inventory-transaction.js usa el mismo modal.
    const modal = modalEl ? bootstrap.Modal.getOrCreateInstance(modalEl) : null;
    const modalContent = document.getElementById('lineModalContent');
    const addBtn = document.getElementById('btn-add-line');

    function showApiError(result, fallbackMessage) {
        let messages = null;
        let specificMessage = null;

        try {
            const parsed = JSON.parse(result.details);
            if (parsed && parsed.errors) {
                messages = Object.values(parsed.errors).flat();
            } else if (parsed && parsed.message) {
                specificMessage = parsed.message;
            }
        } catch (e) { }

        if (messages && messages.length) {
            Swal.fire({ icon: 'error', title: 'Revisá estos campos', html: messages.join('<br>') });
        } else {
            Swal.fire('Error', specificMessage || result.message || fallbackMessage, 'error');
        }
    }

    function antiForgeryToken() {
        const input = document.querySelector('input[name="__RequestVerificationToken"]');
        return input ? input.value : '';
    }

    // Quita tildes y mayúsculas para que "puno" encuentre "PUÑO".
    function normalize(text) {
        return text.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase();
    }

    // Después de un cambio: en la bodega se refresca solo la tabla al momento;
    // en el detalle de la línea se recarga la página cuando termina el aviso de éxito.
    function notifyAndRefresh(title) {
        const toast = Swal.fire({ icon: 'success', title: title, timer: 1500, showConfirmButton: false });
        if (container) {
            loadLines();
        } else {
            toast.then(() => location.reload());
        }
    }

    async function loadLines() {
        if (!container) return;
        try {
            const response = await fetch(`/InventoryLine/ByInventory/${inventoryId}`);
            if (!response.ok) throw new Error();
            container.innerHTML = await response.text();
            if (window.initAppTables) window.initAppTables(container);
        } catch (e) {
            container.innerHTML = '<div class="app-table-empty"><i class="fa-solid fa-triangle-exclamation"></i><span>No se pudieron cargar los productos de la bodega.</span></div>';
        }
    }

    async function openModal(url, onLoaded) {
        const response = await fetch(url);
        if (!response.ok) {
            Swal.fire('Error', 'No se pudo cargar el formulario.', 'error');
            return;
        }
        modalContent.innerHTML = await response.text();
        modal.show();
        onLoaded();
    }

    // Envía un formulario del modal; si sale bien, cierra el modal y refresca.
    function wireForm(form, url, successTitle, fallbackMessage) {
        form.addEventListener('submit', async function (e) {
            e.preventDefault();
            const response = await fetch(url, { method: 'POST', body: new FormData(form) });
            const result = await response.json();

            if (result.success) {
                modal.hide();
                notifyAndRefresh(successTitle);
            } else {
                showApiError(result, fallbackMessage);
            }
        });
    }

    // Buscador del modal de alta: filtra el <select> reconstruyendo sus opciones
    // (ocultar <option> no funciona igual en todos los navegadores).
    function wireProductSearch() {
        const search = modalContent.querySelector('#lineProductSearch');
        const select = modalContent.querySelector('#ProductID');
        const count = modalContent.querySelector('#lineProductCount');
        if (!search || !select) return;

        const all = Array.from(select.options).map(o => ({ value: o.value, text: o.text, key: normalize(o.text) }));

        search.addEventListener('input', function () {
            const term = normalize(search.value.trim());
            const selected = select.value;
            const matches = term ? all.filter(o => o.key.includes(term)) : all;

            select.innerHTML = '';
            matches.forEach(function (o) {
                const option = new Option(o.text, o.value, false, o.value === selected);
                select.add(option);
            });
            if (count) count.textContent = matches.length;
        });
    }

    if (addBtn && modal) {
        addBtn.addEventListener('click', function () {
            openModal(`/InventoryLine/CreatePartial/${inventoryId}`, function () {
                const form = modalContent.querySelector('#createLineForm');
                if (!form) return;
                wireProductSearch();
                wireForm(form, '/InventoryLine/Create', 'Producto agregado', 'No se pudo agregar el producto.');
            });
        });
    }

    // Delegación de eventos en el documento: los botones de la tabla se reemplazan en cada recarga,
    // y el botón Editar del detalle de la línea está fuera de la tabla.
    document.addEventListener('click', function (e) {
        const editBtn = e.target.closest('.btn-edit-line');
        if (editBtn && modal) {
            const id = editBtn.getAttribute('data-id');
            openModal(`/InventoryLine/EditPartial/${id}`, function () {
                const form = modalContent.querySelector('#editLineForm');
                if (form) wireForm(form, `/InventoryLine/Edit/${id}`, 'Línea actualizada', 'No se pudo actualizar la línea.');
            });
            return;
        }

        const inactivateBtn = e.target.closest('.btn-inactivate-line');
        if (inactivateBtn) {
            const id = inactivateBtn.getAttribute('data-id');
            const name = inactivateBtn.getAttribute('data-name');

            Swal.fire({
                title: `¿Inactivar "${name}" en esta bodega?`,
                text: 'El producto deja de estar activo en esta bodega.',
                icon: 'warning',
                showCancelButton: true,
                confirmButtonText: 'Inactivar',
                cancelButtonText: 'Cancelar',
                confirmButtonColor: '#dc3545'
            }).then(async function (choice) {
                if (!choice.isConfirmed) return;

                const formData = new FormData();
                formData.append('__RequestVerificationToken', antiForgeryToken());
                const response = await fetch(`/InventoryLine/Inactivate/${id}`, { method: 'POST', body: formData });
                const result = await response.json();

                if (result.success) {
                    notifyAndRefresh('Línea inactivada');
                } else {
                    showApiError(result, 'No se pudo inactivar la línea.');
                }
            });
        }
    });

    loadLines();
});
