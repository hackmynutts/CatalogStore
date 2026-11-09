document.addEventListener('DOMContentLoaded', function () {
    const editModalEl = document.getElementById('editProductModal');
    const createModalEl = document.getElementById('createProductModal');
    if (!editModalEl && !createModalEl) return;

    const editModal = editModalEl ? new bootstrap.Modal(editModalEl) : null;
    const editModalContent = document.getElementById('editProductModalContent');

    const createModal = createModalEl ? new bootstrap.Modal(createModalEl) : null;
    const createModalContent = document.getElementById('createProductModalContent');
    const newProductBtn = document.getElementById('btn-new-product');

    document.querySelectorAll('.btn-edit-product').forEach(function (btn) {
        btn.addEventListener('click', async function () {
            const id = btn.getAttribute('data-id');
            const response = await fetch(`/Product/EditPartial/${id}`);

            if (!response.ok) {
                Swal.fire('Error', 'No se pudo cargar el producto.', 'error');
                return;
            }

            editModalContent.innerHTML = await response.text();
            editModal.show();
            wireEditForm();
        });
    });

    function wireEditForm() {
        const form = editModalContent.querySelector('#editProductForm');
        form.addEventListener('submit', async function (e) {
            e.preventDefault();
            const id = form.getAttribute('data-id');
            const formData = new FormData(form);

            const response = await fetch(`/Product/Edit/${id}`, {
                method: 'POST',
                body: formData
            });

            const result = await response.json();

            if (result.success) {
                editModal.hide();
                Swal.fire({ icon: 'success', title: 'Producto actualizado', timer: 1500, showConfirmButton: false })
                    .then(() => location.reload());
            } else {
                showApiError(result, 'No se pudo actualizar el producto.');
            }
        });
    }

    if (newProductBtn && createModal) {
        newProductBtn.addEventListener('click', async function () {
            const response = await fetch('/Product/CreatePartial');

            if (!response.ok) {
                Swal.fire('Error', 'No se pudo cargar el formulario.', 'error');
                return;
            }

            createModalContent.innerHTML = await response.text();
            createModal.show();
            wireCreateForm();
        });
    }

    function wireCreateForm() {
        const form = createModalContent.querySelector('#createProductForm');

        form.addEventListener('submit', async function (e) {
            e.preventDefault();
            const formData = new FormData(form);

            const response = await fetch('/Product/Create', {
                method: 'POST',
                body: formData
            });

            const result = await response.json();

            if (result.success) {
                // Si la pregunta de inventario no se puede mostrar, todo sigue como antes: aviso y recarga.
                if (result.id && await showInventoryStep(result.id)) return;
                finishCreated('Producto creado');
            } else {
                showApiError(result, 'No se pudo crear el producto.');
            }
        });
    }

    // Cierra el modal con el aviso de siempre y recarga para que el producto aparezca en la lista.
    function finishCreated(title) {
        reloadOnClose = false;
        createModal.hide();
        Swal.fire({ icon: 'success', title: title, timer: 1500, showConfirmButton: false })
            .then(() => location.reload());
    }

    // Después de crear el producto: pregunta si se agrega al inventario. Solo con "Sí" aparece el formulario de la línea
    // y la carga inicial. Devuelve false si el paso no se pudo cargar (el producto ya está creado).
    let reloadOnClose = false;
    async function showInventoryStep(productId) {
        let html;
        try {
            const response = await fetch(`/Product/InventoryStepPartial/${productId}`);
            if (!response.ok) return false;
            html = await response.text();
        } catch (e) {
            return false;
        }

        createModalContent.innerHTML = html;
        // Cerrar el modal con la X o fuera de él también refresca la lista: el producto ya existe.
        reloadOnClose = true;
        createModalEl.addEventListener('hidden.bs.modal', function () {
            if (reloadOnClose) location.reload();
        }, { once: true });
        wireInventoryStep();
        return true;
    }

    function wireInventoryStep() {
        const no = createModalContent.querySelector('#invStepNo');
        const yes = createModalContent.querySelector('#invStepYes');
        const skip = createModalContent.querySelector('#invStepSkip');
        const question = createModalContent.querySelector('#invStepQuestion');
        const form = createModalContent.querySelector('#addToInventoryForm');

        if (no) no.addEventListener('click', () => finishCreated('Producto creado'));
        if (skip) skip.addEventListener('click', () => finishCreated('Producto creado'));
        if (!yes || !form) return;

        const quantity = form.querySelector('#InitialQuantity');
        const restock = form.querySelector('#QuantityRestock');
        const preview = form.querySelector('#invStepPreview');

        function updatePreview() {
            const qty = parseInt(quantity.value, 10) || 0;
            const min = parseInt(restock.value, 10) || 0;
            if (qty <= 0) {
                preview.textContent = 'No se registra ingreso: solo se crea la línea del producto en la bodega.';
            } else {
                preview.textContent = `Quedará con ${qty} ${qty === 1 ? 'unidad disponible' : 'unidades disponibles'}.` +
                    (min > 0 && qty <= min ? ' Ya aparecerá como "Reponer".' : '');
            }
        }
        quantity.addEventListener('input', updatePreview);
        restock.addEventListener('input', updatePreview);

        yes.addEventListener('click', function () {
            question.hidden = true;
            form.hidden = false;
            updatePreview();
            quantity.focus();
        });

        form.addEventListener('submit', async function (e) {
            e.preventDefault();
            const submit = form.querySelector('button[type="submit"]');
            submit.disabled = true;
            try {
                const response = await fetch('/Product/AddToInventory', { method: 'POST', body: new FormData(form) });
                const result = await response.json();
                if (result.success) {
                    const qty = result.quantity || 0;
                    finishCreated(qty > 0
                        ? `Producto agregado al inventario con ${qty} ${qty === 1 ? 'unidad' : 'unidades'}`
                        : 'Producto agregado al inventario');
                } else {
                    // Si la línea ya se creó y falló la carga, un nuevo intento reutiliza esa línea.
                    showApiError(result, 'No se pudo agregar al inventario.');
                }
            } catch (err) {
                Swal.fire('Error', 'No se pudo agregar al inventario. Revisá tu conexión e intentá de nuevo.', 'error');
            } finally {
                submit.disabled = false;
            }
        });
    }

    document.querySelectorAll('.btn-inactivate-product').forEach(function (btn) {
        btn.addEventListener('click', function () {
            const id = btn.getAttribute('data-id');
            const name = btn.getAttribute('data-name');

            Swal.fire({
                title: `¿Inactivar "${name}"?`,
                text: 'El producto deja de aparecer en el listado de activos.',
                icon: 'warning',
                showCancelButton: true,
                confirmButtonText: 'Inactivar',
                cancelButtonText: 'Cancelar',
                confirmButtonColor: '#dc3545'
            }).then(async function (result) {
                if (!result.isConfirmed) return;

                const token = document.querySelector('input[name="__RequestVerificationToken"]');
                const formData = new FormData();
                if (token) formData.append('__RequestVerificationToken', token.value);

                const response = await fetch(`/Product/Inactivate/${id}`, {
                    method: 'POST',
                    body: formData
                });

                const data = await response.json();

                if (data.success) {
                    Swal.fire({ icon: 'success', title: 'Producto inactivado', timer: 1500, showConfirmButton: false })
                        .then(() => location.reload());
                } else {
                    showApiError(data, 'No se pudo inactivar el producto.');
                }
            });
        });
    });

    document.querySelectorAll('.btn-delete-product').forEach(function (btn) {
        btn.addEventListener('click', function () {
            const id = btn.getAttribute('data-id');
            const name = btn.getAttribute('data-name');

            Swal.fire({
                title: `¿Eliminar "${name}"?`,
                text: 'Esta acción no se puede deshacer.',
                icon: 'warning',
                showCancelButton: true,
                confirmButtonText: 'Eliminar',
                cancelButtonText: 'Cancelar',
                confirmButtonColor: '#dc3545'
            }).then(async function (result) {
                if (!result.isConfirmed) return;

                const token = document.querySelector('input[name="__RequestVerificationToken"]');
                const formData = new FormData();
                if (token) formData.append('__RequestVerificationToken', token.value);

                const response = await fetch(`/Product/Delete/${id}`, { method: 'POST', body: formData });
                const data = response.ok ? await response.json() : { success: false };

                if (data.success) {
                    Swal.fire({ icon: 'success', title: 'Producto eliminado', timer: 1500, showConfirmButton: false })
                        .then(() => location.reload());
                } else {
                    showApiError(data, 'No se pudo eliminar el producto.');
                }
            });
        });
    });

    // ── Sincronización del catálogo del proveedor ──
    // Flujo: primero una simulación (dryRun) que no guarda nada; si hay cambios, el admin confirma y recién ahí se importa.
    const importBtn = document.getElementById('btn-import-catalog');
    if (importBtn) importBtn.addEventListener('click', runCatalogImport);

    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    function messageList(title, messages, total) {
        if (!messages || !messages.length) return '';
        const extra = total > messages.length ? `<li>… y ${total - messages.length} más</li>` : '';
        const items = messages.map(m => `<li>${escapeHtml(m)}</li>`).join('');
        return `<details class="text-start mt-2"><summary>${title} (${total})</summary>`
            + `<ul style="max-height:180px; overflow:auto; font-size:.85rem;">${items}${extra}</ul></details>`;
    }

    function summaryHtml(r) {
        return `<div class="text-start">`
            + `<p class="mb-1"><strong>En el proveedor:</strong> ${r.totalInSource} productos</p>`
            + `<ul class="mb-0">`
            + `<li>Nuevos: <strong>${r.created}</strong></li>`
            + `<li>Actualizados: <strong>${r.updated}</strong></li>`
            + `<li>Sin cambios: <strong>${r.unchanged}</strong></li>`
            + `<li>Omitidos por error: <strong>${r.skipped}</strong></li>`
            + `</ul></div>`
            + messageList('Errores', r.errors, r.errorCount)
            + messageList('Advertencias', r.warnings, r.warningCount);
    }

    // Devuelve el resumen del backend, o null si algo falló (el error ya se mostró al usuario).
    async function callImport(dryRun) {
        Swal.fire({
            title: dryRun ? 'Consultando el catálogo del proveedor…' : 'Importando productos…',
            allowOutsideClick: false,
            allowEscapeKey: false,
            showConfirmButton: false,
            didOpen: () => Swal.showLoading()
        });

        try {
            const token = document.querySelector('input[name="__RequestVerificationToken"]');
            const formData = new FormData();
            if (token) formData.append('__RequestVerificationToken', token.value);

            const response = await fetch(`/Product/ImportCatalog?dryRun=${dryRun}`, { method: 'POST', body: formData });
            const data = await response.json();
            if (data.success) return data.result;

            showApiError(data, 'No se pudo importar el catálogo.');
        } catch (e) {
            Swal.fire('Error', 'No se pudo conectar con el servidor.', 'error');
        }
        return null;
    }

    async function runCatalogImport() {
        const preview = await callImport(true);
        if (!preview) return;

        if (preview.created === 0 && preview.updated === 0) {
            await Swal.fire({ icon: 'info', title: 'El catálogo ya está sincronizado', html: summaryHtml(preview) });
            return;
        }

        const confirmation = await Swal.fire({
            icon: 'question',
            title: 'Simulación completada',
            html: summaryHtml(preview) + '<p class="mt-3 mb-0">¿Importar estos cambios?</p>',
            showCancelButton: true,
            confirmButtonText: 'Importar ahora',
            cancelButtonText: 'Cancelar'
        });
        if (!confirmation.isConfirmed) return;

        const result = await callImport(false);
        if (!result) return;

        await Swal.fire({ icon: 'success', title: 'Importación completada', html: summaryHtml(result) });
        location.reload();
    }
});
