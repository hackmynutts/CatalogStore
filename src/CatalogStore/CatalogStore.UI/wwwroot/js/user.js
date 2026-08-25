document.addEventListener('DOMContentLoaded', function () {
    const editModalEl = document.getElementById('editUserModal');
    if (!editModalEl) return;

    const editModal = new bootstrap.Modal(editModalEl);
    const modalContent = document.getElementById('editUserModalContent');

    document.querySelectorAll('.btn-edit-user').forEach(function (btn) {
        btn.addEventListener('click', async function () {
            const id = btn.getAttribute('data-id');
            const response = await fetch(`/User/EditPartial/${id}`);

            if (!response.ok) {
                Swal.fire('Error', 'No se pudo cargar el usuario.', 'error');
                return;
            }

            modalContent.innerHTML = await response.text();
            editModal.show();
            wireEditForm();
        });
    });

    function wireEditForm() {
        const form = modalContent.querySelector('#editUserForm');
        form.addEventListener('submit', async function (e) {
            e.preventDefault();
            const id = form.getAttribute('data-id');
            const formData = new FormData(form);

            const response = await fetch(`/User/Edit/${id}`, {
                method: 'POST',
                body: formData
            });

            const result = await response.json();

            if (result.success) {
                editModal.hide();
                Swal.fire({ icon: 'success', title: 'Usuario actualizado', timer: 1500, showConfirmButton: false })
                    .then(() => location.reload());
            } else {
                Swal.fire('Error', result.message || 'No se pudo actualizar el usuario.', 'error');
            }
        });
    }

    const createModalEl = document.getElementById('createUserModal');
    const createModal = createModalEl ? new bootstrap.Modal(createModalEl) : null;
    const createModalContent = document.getElementById('createUserModalContent');
    const newUserBtn = document.getElementById('btn-new-user');

    if (newUserBtn && createModal) {
        newUserBtn.addEventListener('click', async function () {
            const response = await fetch('/User/CreatePartial');

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
        const form = createModalContent.querySelector('#createUserForm');
        form.addEventListener('submit', async function (e) {
            e.preventDefault();
            const formData = new FormData(form);

            const response = await fetch('/User/Create', {
                method: 'POST',
                body: formData
            });

            const result = await response.json();

            if (result.success) {
                createModal.hide();
                Swal.fire({
                    icon: 'success',
                    title: 'Usuario creado',
                    html: `Contraseña temporal:<br><strong style="font-size:1.3em;">${result.temporaryPassword}</strong><br><small>Compartila con el usuario — no se vuelve a mostrar.</small>`,
                    confirmButtonText: 'Entendido'
                }).then(() => location.reload());
            } else {
                Swal.fire('Error', result.message || 'No se pudo crear el usuario.', 'error');
            }
        });
    }

    document.querySelectorAll('.btn-reset-password').forEach(function (btn) {
        btn.addEventListener('click', function () {
            const id = btn.getAttribute('data-id');
            const username = btn.getAttribute('data-username');

            Swal.fire({
                title: `¿Restablecer la contraseña de ${username}?`,
                text: 'Se va a generar una contraseña temporal nueva; la actual deja de servir.',
                icon: 'warning',
                showCancelButton: true,
                confirmButtonText: 'Restablecer',
                cancelButtonText: 'Cancelar',
                confirmButtonColor: '#dc3545'
            }).then(async function (result) {
                if (!result.isConfirmed) return;

                const token = document.querySelector('input[name="__RequestVerificationToken"]');
                const formData = new FormData();
                if (token) formData.append('__RequestVerificationToken', token.value);

                const response = await fetch(`/User/ResetPassword/${id}`, {
                    method: 'POST',
                    body: formData
                });

                const data = await response.json();

                if (data.success) {
                    Swal.fire({
                        icon: 'success',
                        title: 'Contraseña restablecida',
                        html: `Nueva contraseña temporal:<br><strong style="font-size:1.3em;">${data.temporaryPassword}</strong><br><small>Compartila con el usuario — no se vuelve a mostrar.</small>`,
                        confirmButtonText: 'Entendido'
                    });
                } else {
                    Swal.fire('Error', data.message || 'No se pudo restablecer la contraseña.', 'error');
                }
            });
        });
    });

    document.querySelectorAll('.btn-delete-user').forEach(function (btn) {
        btn.addEventListener('click', function () {
            const id = btn.getAttribute('data-id');
            const username = btn.getAttribute('data-username');

            Swal.fire({
                title: `¿Eliminar a ${username}?`,
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
                form.action = `/User/Delete/${id}`;

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
