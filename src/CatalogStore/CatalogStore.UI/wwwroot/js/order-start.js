// Botón "Iniciar orden" del perfil del cliente (Views/Client/Details.cshtml).
// Abre la orden en proceso del cliente (o crea una vacía), la deja como orden activa y lleva al catálogo.
document.addEventListener('DOMContentLoaded', function () {
    const btn = document.querySelector('.btn-start-order');
    if (!btn) return;

    btn.addEventListener('click', async function () {
        const token = document.querySelector('#orderStartTokenForm input[name="__RequestVerificationToken"]').value;
        const body = new FormData();
        body.append('__RequestVerificationToken', token);

        const icon = btn.querySelector('i');
        btn.disabled = true;
        icon.className = 'fa-solid fa-spinner fa-spin';
        try {
            const response = await fetch(`/Order/Start/${btn.dataset.clientId}`, { method: 'POST', body });
            const result = response.ok ? await response.json() : { success: false };
            if (result.success) {
                location.href = result.redirectUrl;
                return;
            }
            Swal.fire({ icon: 'error', title: 'No se pudo iniciar la orden', text: result.message || 'Intentá de nuevo.' });
        } finally {
            btn.disabled = false;
            icon.className = 'fa-solid fa-cart-plus';
        }
    });
});
