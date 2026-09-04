document.addEventListener('DOMContentLoaded', function () {
    wireManageImagesButton();
    wireImageClicks(document);
    wireAdminDeleteButtons();
});

function renderCarousel(images) {
    const carouselEl = document.getElementById('productImageCarousel');
    if (!carouselEl) return;

    const existing = bootstrap.Carousel.getInstance(carouselEl);
    if (existing) existing.dispose();

    const inner = images.length
        ? images.map((img, i) => `
            <div class="carousel-item ${i === 0 ? 'active' : ''}">
                <img src="${img.url}" class="d-block w-100 product-media-img" data-id="${img.productImageID}" role="button" />
            </div>`).join('')
        : `<div class="carousel-item active">
                <div class="product-media-placeholder">
                    <i class="fa-solid fa-image"></i>
                    <span>Sin imágenes todavía</span>
                </div>
           </div>`;

    const controls = images.length > 1
        ? `
            <button class="carousel-control-prev" type="button" data-bs-target="#productImageCarousel" data-bs-slide="prev">
                <span class="carousel-control-prev-icon" aria-hidden="true"></span>
            </button>
            <button class="carousel-control-next" type="button" data-bs-target="#productImageCarousel" data-bs-slide="next">
                <span class="carousel-control-next-icon" aria-hidden="true"></span>
            </button>
            <div class="carousel-indicators">
                ${images.map((_, i) => `<button type="button" data-bs-target="#productImageCarousel" data-bs-slide-to="${i}" class="${i === 0 ? 'active' : ''}"></button>`).join('')}
            </div>`
        : '';

    carouselEl.innerHTML = `<div class="carousel-inner" id="productCarouselInner">${inner}</div>${controls}`;

    if (images.length > 1) {
        new bootstrap.Carousel(carouselEl, { ride: false });
    }

    wireImageClicks(document);
}

function renderThumbs(images, container) {
    const thumbsEl = container.querySelector('#productImageThumbs');
    if (!thumbsEl) return;

    thumbsEl.innerHTML = images.length
        ? images.map(img => `
            <div class="product-image-thumb-wrap" data-id="${img.productImageID}">
                <img src="${img.url}" class="product-image-thumb" data-id="${img.productImageID}" role="button" />
                <button type="button" class="btn btn-sm btn-outline-danger btn-delete-image" data-id="${img.productImageID}" title="Eliminar"><i class="fa-solid fa-trash"></i></button>
            </div>`).join('')
        : '<p class="text-muted mb-0">Este producto todavía no tiene imágenes.</p>';
}

async function refreshAfterChange(productId, modalContent) {
    const response = await fetch(`/ProductImage/ByProduct/${productId}`);
    if (!response.ok) return;

    const images = await response.json();
    renderCarousel(images);
    renderThumbs(images, modalContent);
    wireThumbDeleteButtons(productId, modalContent);
    wireImageClicks(modalContent);
}

function wireManageImagesButton() {
    const btn = document.getElementById('btn-manage-images');
    const modalEl = document.getElementById('manageImagesModal');
    if (!btn || !modalEl) return;

    const modal = new bootstrap.Modal(modalEl);
    const modalContent = document.getElementById('manageImagesModalContent');
    const productId = btn.getAttribute('data-product-id');

    btn.addEventListener('click', async function () {
        const response = await fetch(`/ProductImage/ManagePartial/${productId}`);
        if (!response.ok) {
            Swal.fire('Error', 'No se pudo cargar el gestor de imágenes.', 'error');
            return;
        }

        modalContent.innerHTML = await response.text();
        modal.show();
        wireModalContent(productId, modalContent);
    });
}

function wireModalContent(productId, modalContent) {
    const uploadForm = modalContent.querySelector('#uploadImageForm');

    uploadForm.addEventListener('submit', async function (e) {
        e.preventDefault();
        const fileInput = modalContent.querySelector('#productImageFile');
        if (!fileInput.files.length) return;

        const formData = new FormData();
        formData.append('file', fileInput.files[0]);
        const token = uploadForm.querySelector('input[name="__RequestVerificationToken"]');
        if (token) formData.append('__RequestVerificationToken', token.value);

        const response = await fetch(`/ProductImage/Upload/${productId}`, {
            method: 'POST',
            body: formData
        });
        const result = await response.json();

        if (result.success) {
            fileInput.value = '';
            await refreshAfterChange(productId, modalContent);
            Swal.fire({ icon: 'success', title: 'Imagen subida', timer: 1200, showConfirmButton: false });
        } else {
            Swal.fire('Error', result.message || 'No se pudo subir la imagen.', 'error');
        }
    });

    wireThumbDeleteButtons(productId, modalContent);
    wireImageClicks(modalContent);
}

function wireThumbDeleteButtons(productId, container) {
    container.querySelectorAll('.btn-delete-image').forEach(function (btn) {
        btn.addEventListener('click', function () {
            const id = btn.getAttribute('data-id');

            Swal.fire({
                title: '¿Eliminar esta imagen?',
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

                const response = await fetch(`/ProductImage/Delete/${id}`, { method: 'POST', body: formData });
                const data = await response.json();

                if (data.success) {
                    await refreshAfterChange(productId, container);
                    Swal.fire({ icon: 'success', title: 'Imagen eliminada', timer: 1200, showConfirmButton: false });
                } else {
                    Swal.fire('Error', 'No se pudo eliminar la imagen.', 'error');
                }
            });
        });
    });
}

function wireImageClicks(container) {
    const modalEl = document.getElementById('imageFullscreenModal');
    if (!modalEl) return;
    const modal = new bootstrap.Modal(modalEl);
    const modalImg = document.getElementById('imageFullscreenImg');

    container.querySelectorAll('.product-media-img, .product-image-thumb').forEach(function (el) {
        el.addEventListener('click', async function () {
            const id = el.getAttribute('data-id');
            const response = await fetch(`/ProductImage/Get/${id}`);
            if (!response.ok) return;

            const data = await response.json();
            modalImg.src = data.url;
            modal.show();
        });
    });
}

function wireAdminDeleteButtons() {
    document.querySelectorAll('.btn-delete-product-image').forEach(function (btn) {
        btn.addEventListener('click', function () {
            const id = btn.getAttribute('data-id');

            Swal.fire({
                title: '¿Eliminar esta imagen?',
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

                const response = await fetch(`/ProductImage/Delete/${id}`, { method: 'POST', body: formData });
                const data = await response.json();

                if (data.success) {
                    Swal.fire({ icon: 'success', title: 'Imagen eliminada', timer: 1200, showConfirmButton: false })
                        .then(() => location.reload());
                } else {
                    Swal.fire('Error', 'No se pudo eliminar la imagen.', 'error');
                }
            });
        });
    });
}
