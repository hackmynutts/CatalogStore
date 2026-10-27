// Diálogo "Descuento a toda la orden", compartido por el detalle de la orden (order-details.js) y el panel del
// catálogo (order-catalog.js). Solo pide y valida el %; quien lo llama hace el POST a /Order/SetDiscount.
// El backend vuelve a validar todo (rango, dos decimales y el tope del vendedor): aquí es solo para avisar antes.
window.OrderDiscount = (function () {
    function esc(value) {
        return String(value ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    }

    const pct = v => `${Number(v).toLocaleString('en-US', { maximumFractionDigits: 2 })} %`;

    // lines: [{ name, discount }]. maxDiscount: 5 para vendedores, 99.99 para admins.
    // Devuelve el % elegido (número) o null si se canceló.
    async function ask({ lines, maxDiscount, confirmButtonColor }) {
        const discounts = [...new Set(lines.map(l => Number(l.discount)))];
        const current = discounts.length === 1 ? discounts[0] : null;
        const count = lines.length === 1 ? 'el producto' : `los ${lines.length} productos`;
        const isSeller = maxDiscount < 99.99;

        const result = await Swal.fire({
            title: 'Descuento a toda la orden',
            html: `<p class="order-discount-intro">Se aplica el mismo % a ${count} y reemplaza los descuentos por producto.
                   Los productos que agregues después entran sin descuento.</p>
                   ${isSeller ? `<p class="order-discount-cap">Como vendedor podés dar hasta ${pct(maxDiscount)}.</p>` : ''}
                   <p class="order-discount-cap">Poné 0 para quitar todos los descuentos.</p>`,
            input: 'number',
            inputLabel: 'Descuento (%)',
            inputValue: current !== null && current > 0 ? String(current) : '',
            inputAttributes: { min: '0', max: String(maxDiscount), step: '0.01', inputmode: 'decimal', 'aria-label': 'Descuento en porcentaje' },
            inputValidator: value => {
                const n = Number(value);
                if (value === '' || Number.isNaN(n)) return 'Escribí el porcentaje.';
                if (n < 0 || n > 99.99) return 'El descuento debe estar entre 0 y 99.99 %.';
                // Por texto y no con n * 100: en JS 5.55 * 100 da 554.999…
                if (!/^\d+(\.\d{1,2})?$/.test(String(value).trim())) return 'Usá como máximo dos decimales.';
                if (n > maxDiscount) return `El descuento máximo permitido para vendedores es ${pct(maxDiscount)}.`;
                return undefined;
            },
            showCancelButton: true,
            confirmButtonText: 'Aplicar',
            cancelButtonText: 'Volver',
            confirmButtonColor
        });
        if (!result.isConfirmed) return null;

        const value = Number(result.value);
        if (lines.every(l => Number(l.discount) === value)) return value;

        // Productos con un descuento propio que se va a perder: se listan antes de pisarlos.
        const replaced = lines.filter(l => Number(l.discount) > 0 && Number(l.discount) !== value);
        if (replaced.length === 0) return value;

        const list = replaced.slice(0, 6).map(l => `<li>${esc(l.name)} <strong>${pct(l.discount)}</strong></li>`).join('');
        const more = replaced.length > 6 ? `<li>y ${replaced.length - 6} más…</li>` : '';
        const confirm = await Swal.fire({
            icon: 'warning',
            title: value === 0 ? '¿Quitar todos los descuentos?' : `¿Reemplazar por ${pct(value)}?`,
            html: `<p>${replaced.length === 1 ? 'Este producto tiene' : 'Estos productos tienen'} un descuento propio que se reemplaza:</p>
                   <ul class="order-discount-list">${list}${more}</ul>`,
            showCancelButton: true,
            confirmButtonText: value === 0 ? 'Quitar descuentos' : 'Reemplazar',
            cancelButtonText: 'Volver',
            confirmButtonColor
        });
        return confirm.isConfirmed ? value : null;
    }

    return { ask };
})();
