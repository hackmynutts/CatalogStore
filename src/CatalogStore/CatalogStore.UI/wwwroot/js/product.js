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
                Swal.fire('Error', result.message || 'No se pudo actualizar el producto.', 'error');
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
                createModal.hide();
                Swal.fire({ icon: 'success', title: 'Producto creado', timer: 1500, showConfirmButton: false })
                    .then(() => location.reload());
            } else {
                Swal.fire('Error', result.message || 'No se pudo crear el producto.', 'error');
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
                    Swal.fire('Error', data.message || 'No se pudo inactivar el producto.', 'error');
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
            }).then(function (result) {
                if (!result.isConfirmed) return;

                const form = document.createElement('form');
                form.method = 'post';
                form.action = `/Product/Delete/${id}`;

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
