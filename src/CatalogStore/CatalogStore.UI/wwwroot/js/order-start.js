// Botón "Iniciar orden" del perfil del cliente (Views/Client/Details.cshtml).
// Abre la orden en proceso del cliente (o crea una vacía), la deja como orden activa y lleva al catálogo.
document.addEventListener('DOMContentLoaded', function () {
    const btn = document.querySelector('.btn-start-order');
    if (!btn) return;

    btn.addEventListener('click', async function () {
        // Administración elige a nombre de qué vendedor queda la orden; los demás roles no ven esta pregunta (la UI les responde 403).
        let seller = '';
        try {
            const response = await fetch('/Order/Sellers');
            const list = response.ok ? await response.json() : null;
            if (list && list.success) {
                const options = { '': 'A mi nombre' };
                list.sellers.forEach(s => { options[s.email] = s.name; });
                const ask = await Swal.fire({
                    title: 'Iniciar orden',
                    text: 'Elegí a nombre de qué vendedor queda. Será el mismo en toda la orden y en su venta.',
                    input: 'select',
                    inputOptions: options,
                    inputLabel: 'Vendedor',
                    showCancelButton: true,
                    confirmButtonText: 'Iniciar orden',
                    cancelButtonText: 'Cancelar'
                });
                if (!ask.isConfirmed) return;
                seller = ask.value || '';
            }
        } catch (e) { /* sin lista de vendedores: se sigue a nombre de quien la crea */ }

        const token = document.querySelector('#orderStartTokenForm input[name="__RequestVerificationToken"]').value;
        const body = new FormData();
        body.append('__RequestVerificationToken', token);
        if (seller) body.append('seller', seller);

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
