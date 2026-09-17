document.addEventListener('DOMContentLoaded', function () {
    const editModalEl = document.getElementById('editInventoryModal');
    const createModalEl = document.getElementById('createInventoryModal');
    if (!editModalEl && !createModalEl) return;

    const editModal = editModalEl ? new bootstrap.Modal(editModalEl) : null;
    const editModalContent = document.getElementById('editInventoryModalContent');

    const createModal = createModalEl ? new bootstrap.Modal(createModalEl) : null;
    const createModalContent = document.getElementById('createInventoryModalContent');
    const newInventoryBtn = document.getElementById('btn-new-inventory');

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

    document.querySelectorAll('.btn-edit-inventory').forEach(function (btn) {
        btn.addEventListener('click', async function () {
            const id = btn.getAttribute('data-id');
            const response = await fetch(`/Inventory/EditPartial/${id}`);

            if (!response.ok) {
                Swal.fire('Error', 'No se pudo cargar el inventario.', 'error');
                return;
            }

            editModalContent.innerHTML = await response.text();
            editModal.show();
            wireEditForm();
        });
    });

    function wireEditForm() {
        const form = editModalContent.querySelector('#editInventoryForm');
        form.addEventListener('submit', async function (e) {
            e.preventDefault();
            const id = form.getAttribute('data-id');
            const formData = new FormData(form);

            const response = await fetch(`/Inventory/Edit/${id}`, {
                method: 'POST',
                body: formData
            });

            const result = await response.json();

            if (result.success) {
                editModal.hide();
                Swal.fire({ icon: 'success', title: 'Inventario actualizado', timer: 1500, showConfirmButton: false })
                    .then(() => location.reload());
            } else {
                showApiError(result, 'No se pudo actualizar el inventario.');
            }
        });
    }

    if (newInventoryBtn && createModal) {
        newInventoryBtn.addEventListener('click', async function () {
            const response = await fetch('/Inventory/CreatePartial');

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
        const form = createModalContent.querySelector('#createInventoryForm');
        form.addEventListener('submit', async function (e) {
            e.preventDefault();
            const formData = new FormData(form);

            const response = await fetch('/Inventory/Create', {
                method: 'POST',
                body: formData
            });

            const result = await response.json();

            if (result.success) {
                createModal.hide();
                Swal.fire({ icon: 'success', title: 'Inventario creado', timer: 1500, showConfirmButton: false })
                    .then(() => location.reload());
            } else {
                showApiError(result, 'No se pudo crear el inventario.');
            }
        });
    }

    document.querySelectorAll('.btn-inactivate-inventory').forEach(function (btn) {
        btn.addEventListener('click', function () {
            const id = btn.getAttribute('data-id');
            const name = btn.getAttribute('data-name');

            Swal.fire({
                title: `¿Inactivar "${name}"?`,
                text: 'El inventario deja de aparecer en el listado de activos.',
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

                const response = await fetch(`/Inventory/Inactivate/${id}`, {
                    method: 'POST',
                    body: formData
                });

                const data = await response.json();

                if (data.success) {
                    Swal.fire({ icon: 'success', title: 'Inventario inactivado', timer: 1500, showConfirmButton: false })
                        .then(() => location.reload());
                } else {
                    Swal.fire('Error', data.message || 'No se pudo inactivar el inventario.', 'error');
                }
            });
        });
    });

    document.querySelectorAll('.btn-delete-inventory').forEach(function (btn) {
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
            }).then(function (result) {
                if (!result.isConfirmed) return;

                const form = document.createElement('form');
                form.method = 'post';
                form.action = `/Inventory/Delete/${id}`;

                const token = document.querySelector('input[name="__RequestVerificationToken"]');
                if (token) {
                    const clone = token.cloneNode(true);
                    form.appendChild(clone);
                }

                document.body.appendChild(form);
                form.submit();
            });
        });
    });
});
