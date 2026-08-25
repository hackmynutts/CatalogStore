document.addEventListener('DOMContentLoaded', function () {
    const editModalEl = document.getElementById('editStatusModal');
    const createModalEl = document.getElementById('createStatusModal');
    if (!editModalEl && !createModalEl) return;

    const editModal = editModalEl ? new bootstrap.Modal(editModalEl) : null;
    const editModalContent = document.getElementById('editStatusModalContent');

    const createModal = createModalEl ? new bootstrap.Modal(createModalEl) : null;
    const createModalContent = document.getElementById('createStatusModalContent');
    const newStatusBtn = document.getElementById('btn-new-status');

    document.querySelectorAll('.btn-edit-status').forEach(function (btn) {
        btn.addEventListener('click', async function () {
            const id = btn.getAttribute('data-id');
            const response = await fetch(`/Status/EditPartial/${id}`);

            if (!response.ok) {
                Swal.fire('Error', 'No se pudo cargar el estado.', 'error');
                return;
            }

            editModalContent.innerHTML = await response.text();
            editModal.show();
            wireEditForm();
        });
    });

    function wireEditForm() {
        const form = editModalContent.querySelector('#editStatusForm');
        form.addEventListener('submit', async function (e) {
            e.preventDefault();
            const id = form.getAttribute('data-id');
            const formData = new FormData(form);

            const response = await fetch(`/Status/Edit/${id}`, {
                method: 'POST',
                body: formData
            });

            const result = await response.json();

            if (result.success) {
                editModal.hide();
                Swal.fire({ icon: 'success', title: 'Estado actualizado', timer: 1500, showConfirmButton: false })
                    .then(() => location.reload());
            } else {
                Swal.fire('Error', result.message || 'No se pudo actualizar el estado.', 'error');
            }
        });
    }

    if (newStatusBtn && createModal) {
        newStatusBtn.addEventListener('click', async function () {
            const response = await fetch('/Status/CreatePartial');

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
        const form = createModalContent.querySelector('#createStatusForm');
        form.addEventListener('submit', async function (e) {
            e.preventDefault();
            const formData = new FormData(form);

            const response = await fetch('/Status/Create', {
                method: 'POST',
                body: formData
            });

            const result = await response.json();

            if (result.success) {
                createModal.hide();
                Swal.fire({ icon: 'success', title: 'Estado creado', timer: 1500, showConfirmButton: false })
                    .then(() => location.reload());
            } else {
                Swal.fire('Error', result.message || 'No se pudo crear el estado.', 'error');
            }
        });
    }

    document.querySelectorAll('.btn-delete-status').forEach(function (btn) {
        btn.addEventListener('click', function () {
            const id = btn.getAttribute('data-id');
            const name = btn.getAttribute('data-name');

            Swal.fire({
                title: `¿Eliminar el estado "${name}"?`,
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
                form.action = `/Status/Delete/${id}`;

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
