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
                showLinkResult(result, 'Usuario creado', 'Se envió la invitación a',
                    'El usuario se creó, pero no se pudo enviar la invitación. Usá "Reenviar invitación" en la lista.')
                    .then(() => location.reload());
            } else {
                Swal.fire('Error', result.message || 'No se pudo crear el usuario.', 'error');
            }
        });
    }

    // Texto que viene de datos: se escapa antes de meterlo en el HTML del Swal.
    function escapeHtml(value) {
        const div = document.createElement('div');
        div.textContent = value ?? '';
        return div.innerHTML.replace(/"/g, '&quot;');
    }

    // Resultado de crear / restablecer / reenviar: a qué correo salió el enlace o, con el correo deshabilitado,
    // el enlace para copiarlo y compartirlo a mano.
    function showLinkResult(data, title, sentText, failedText) {
        if (data.emailSentTo) {
            return Swal.fire({
                icon: 'success',
                title: title,
                html: `${sentText} <strong>${escapeHtml(data.emailSentTo)}</strong>.<br><small>El correo puede tardar unos minutos en llegar.</small>`,
                confirmButtonText: 'Entendido'
            });
        }
        if (data.manualLink) {
            return Swal.fire({
                icon: 'info',
                title: title,
                html: `El envío de correos está deshabilitado. Compartí este enlace con el usuario (es personal y de un solo uso):
                       <input type="text" class="form-control mt-3" id="manual-link" readonly value="${escapeHtml(data.manualLink)}">`,
                confirmButtonText: 'Copiar y cerrar',
                didOpen: () => document.getElementById('manual-link')?.select(),
                preConfirm: () => navigator.clipboard?.writeText(data.manualLink).catch(() => { })
            });
        }
        return Swal.fire({ icon: 'warning', title: title, text: failedText, confirmButtonText: 'Entendido' });
    }

    function postAction(url) {
        const token = document.querySelector('input[name="__RequestVerificationToken"]');
        const formData = new FormData();
        if (token) formData.append('__RequestVerificationToken', token.value);
        return fetch(url, { method: 'POST', body: formData }).then(r => r.json());
    }

    document.querySelectorAll('.btn-reset-password').forEach(function (btn) {
        btn.addEventListener('click', function () {
            const id = btn.getAttribute('data-id');
            const username = btn.getAttribute('data-username');
            const email = btn.getAttribute('data-email');

            Swal.fire({
                title: `¿Restablecer la contraseña de ${escapeHtml(username)}?`,
                html: `Se le enviará a <strong>${escapeHtml(email)}</strong> un enlace para crear una contraseña nueva (vence en 60 minutos).<br><small>Su contraseña actual sigue funcionando hasta que use el enlace.</small>`,
                icon: 'warning',
                showCancelButton: true,
                confirmButtonText: 'Enviar enlace',
                cancelButtonText: 'Cancelar',
                confirmButtonColor: '#dc3545'
            }).then(async function (result) {
                if (!result.isConfirmed) return;

                const data = await postAction(`/User/ResetPassword/${id}`);
                if (data.success) {
                    showLinkResult(data, 'Enlace enviado', 'Se envió el enlace a', 'No se pudo enviar el enlace.');
                } else {
                    Swal.fire('Error', data.message || 'No se pudo restablecer la contraseña.', 'error');
                }
            });
        });
    });

    document.querySelectorAll('.btn-resend-invitation').forEach(function (btn) {
        btn.addEventListener('click', function () {
            const id = btn.getAttribute('data-id');
            const username = btn.getAttribute('data-username');
            const email = btn.getAttribute('data-email');

            Swal.fire({
                title: `¿Reenviar la invitación a ${escapeHtml(username)}?`,
                html: `Se le enviará una invitación nueva a <strong>${escapeHtml(email)}</strong> (vence en 48 horas).<br><small>Las invitaciones anteriores dejan de servir.</small>`,
                icon: 'question',
                showCancelButton: true,
                confirmButtonText: 'Reenviar',
                cancelButtonText: 'Cancelar'
            }).then(async function (result) {
                if (!result.isConfirmed) return;

                const data = await postAction(`/User/ResendInvitation/${id}`);
                if (data.success) {
                    showLinkResult(data, 'Invitación reenviada', 'Se envió la invitación a', 'No se pudo enviar la invitación.');
                } else {
                    Swal.fire('Error', data.message || 'No se pudo reenviar la invitación.', 'error');
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
