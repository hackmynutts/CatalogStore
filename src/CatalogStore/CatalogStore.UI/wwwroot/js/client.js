document.addEventListener('DOMContentLoaded', function () {
    const editModalEl = document.getElementById('editClientModal');
    const createModalEl = document.getElementById('createClientModal');
    if (!editModalEl && !createModalEl) return;

    const editModal = editModalEl ? new bootstrap.Modal(editModalEl) : null;
    const editModalContent = document.getElementById('editClientModalContent');

    const createModal = createModalEl ? new bootstrap.Modal(createModalEl) : null;
    const createModalContent = document.getElementById('createClientModalContent');
    const newClientBtn = document.getElementById('btn-new-client');

    document.querySelectorAll('.btn-edit-client').forEach(function (btn) {
        btn.addEventListener('click', async function () {
            const id = btn.getAttribute('data-id');
            const response = await fetch(`/Client/EditPartial/${id}`);

            if (!response.ok) {
                Swal.fire('Error', 'No se pudo cargar el cliente.', 'error');
                return;
            }

            editModalContent.innerHTML = await response.text();
            editModal.show();
            wireEditForm();
        });
    });

    function wireEditForm() {
        const form = editModalContent.querySelector('#editClientForm');
        form.addEventListener('submit', async function (e) {
            e.preventDefault();
            const id = form.getAttribute('data-id');
            const formData = new FormData(form);

            const response = await fetch(`/Client/Edit/${id}`, {
                method: 'POST',
                body: formData
            });

            const result = await response.json();

            if (result.success) {
                editModal.hide();
                Swal.fire({ icon: 'success', title: 'Cliente actualizado', timer: 1500, showConfirmButton: false })
                    .then(() => location.reload());
            } else {
                Swal.fire('Error', result.message || 'No se pudo actualizar el cliente.', 'error');
            }
        });
    }

    if (newClientBtn && createModal) {
        newClientBtn.addEventListener('click', async function () {
            const response = await fetch('/Client/CreatePartial');

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
        const form = createModalContent.querySelector('#createClientForm');
        const lookupBtn = createModalContent.querySelector('#btn-lookup-client');
        const identificationInput = createModalContent.querySelector('#clientIdentification');
        const lookupStatus = createModalContent.querySelector('#lookupStatus');
        const nameInput = createModalContent.querySelector('[name="ClientName"]');

        lookupBtn.addEventListener('click', async function () {
            const cedula = identificationInput.value.trim();
            if (!cedula) return;

            lookupStatus.textContent = 'Buscando en Hacienda...';
            const response = await fetch(`/Client/Lookup?identification=${encodeURIComponent(cedula)}`);
            const data = await response.json();

            if (data.nombre && data.nombre !== 'No encontrado.') {
                nameInput.value = data.nombre;
                lookupStatus.textContent = 'Nombre encontrado en Hacienda.';
            } else {
                lookupStatus.textContent = 'No se encontró esa cédula en Hacienda — completá el nombre a mano.';
            }
        });

        form.addEventListener('submit', async function (e) {
            e.preventDefault();
            const formData = new FormData(form);

            const response = await fetch('/Client/Create', {
                method: 'POST',
                body: formData
            });

            const result = await response.json();

            if (result.success) {
                createModal.hide();
                Swal.fire({ icon: 'success', title: 'Cliente creado', timer: 1500, showConfirmButton: false })
                    .then(() => location.reload());
            } else {
                Swal.fire('Error', result.message || 'No se pudo crear el cliente.', 'error');
            }
        });
    }

    document.querySelectorAll('.btn-inactivate-client').forEach(function (btn) {
        btn.addEventListener('click', function () {
            const id = btn.getAttribute('data-id');
            const name = btn.getAttribute('data-name');

            Swal.fire({
                title: `¿Inactivar a "${name}"?`,
                text: 'El cliente deja de aparecer en el listado de activos.',
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

                const response = await fetch(`/Client/Inactivate/${id}`, {
                    method: 'POST',
                    body: formData
                });

                const data = await response.json();

                if (data.success) {
                    Swal.fire({ icon: 'success', title: 'Cliente inactivado', timer: 1500, showConfirmButton: false })
                        .then(() => location.reload());
                } else {
                    Swal.fire('Error', data.message || 'No se pudo inactivar el cliente.', 'error');
                }
            });
        });
    });

    document.querySelectorAll('.btn-delete-client').forEach(function (btn) {
        btn.addEventListener('click', function () {
            const id = btn.getAttribute('data-id');
            const name = btn.getAttribute('data-name');

            Swal.fire({
                title: `¿Eliminar a "${name}"?`,
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
                form.action = `/Client/Delete/${id}`;

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
