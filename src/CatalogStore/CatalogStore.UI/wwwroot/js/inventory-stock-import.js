// Carga inicial de stock desde el proveedor (Views/Inventory/Details.cshtml).
// Flujo igual al de la importación de catálogo: primero una simulación (dryRun) que no guarda nada;
// si hay algo para cargar, el admin confirma y recién ahí se guarda.
document.addEventListener('DOMContentLoaded', function () {
    const importBtn = document.getElementById('btn-import-stock');
    if (!importBtn) return;

    const inventoryId = importBtn.getAttribute('data-inventory-id');
    importBtn.addEventListener('click', runStockImport);

    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    function messageList(title, messages, total) {
        if (!messages || !messages.length) return '';
        const extra = total > messages.length ? `<li>… y ${total - messages.length} más</li>` : '';
        const items = messages.map(m => `<li>${escapeHtml(m)}</li>`).join('');
        return `<details class="text-start mt-2"><summary>${title} (${total})</summary>`
            + `<ul style="max-height:180px; overflow:auto; font-size:.85rem;">${items}${extra}</ul></details>`;
    }

    function summaryHtml(r) {
        return `<div class="text-start">`
            + `<p class="mb-1"><strong>En el proveedor:</strong> ${r.totalInSource} productos</p>`
            + `<ul class="mb-0">`
            + `<li>Cargas iniciales: <strong>${r.initialLoads}</strong> (${r.unitsLoaded} unidades)</li>`
            + `<li>Líneas nuevas en ${escapeHtml(r.inventoryName)}: <strong>${r.linesCreated}</strong></li>`
            + `<li>Sin stock en el proveedor: <strong>${r.withoutStock}</strong></li>`
            + `<li>Ya tenían movimientos (no se tocan): <strong>${r.alreadyLoaded}</strong></li>`
            + `<li>No existen en el sistema: <strong>${r.productsNotFound}</strong></li>`
            + `<li>Omitidos: <strong>${r.skipped}</strong></li>`
            + `</ul></div>`
            + messageList('Errores', r.errors, r.errorCount)
            + messageList('Advertencias', r.warnings, r.warningCount);
    }

    // Devuelve el resumen del backend, o null si algo falló (el error ya se mostró al usuario).
    async function callImport(dryRun) {
        Swal.fire({
            title: dryRun ? 'Consultando el inventario del proveedor…' : 'Cargando stock…',
            allowOutsideClick: false,
            allowEscapeKey: false,
            showConfirmButton: false,
            didOpen: () => Swal.showLoading()
        });

        try {
            const token = document.querySelector('input[name="__RequestVerificationToken"]');
            const formData = new FormData();
            if (token) formData.append('__RequestVerificationToken', token.value);

            const response = await fetch(`/InventoryTransaction/ImportStock/${inventoryId}?dryRun=${dryRun}`, { method: 'POST', body: formData });
            const data = await response.json();
            if (data.success) return data.result;

            let text = data.message || 'No se pudo cargar el stock del proveedor.';
            try {
                const parsed = JSON.parse(data.details);
                if (parsed && parsed.message) text = parsed.message;
            } catch (e) { }
            Swal.fire('Error', text, 'error');
        } catch (e) {
            Swal.fire('Error', 'No se pudo conectar con el servidor.', 'error');
        }
        return null;
    }

    async function runStockImport() {
        const preview = await callImport(true);
        if (!preview) return;

        if (preview.initialLoads === 0 && preview.linesCreated === 0) {
            await Swal.fire({ icon: 'info', title: 'No hay stock nuevo para cargar', html: summaryHtml(preview) });
            return;
        }

        const confirmation = await Swal.fire({
            icon: 'question',
            title: 'Simulación completada',
            html: summaryHtml(preview) + '<p class="mt-3 mb-0">¿Cargar este stock en la bodega?</p>',
            showCancelButton: true,
            confirmButtonText: 'Cargar ahora',
            cancelButtonText: 'Cancelar'
        });
        if (!confirmation.isConfirmed) return;

        const result = await callImport(false);
        if (!result) return;

        await Swal.fire({ icon: 'success', title: 'Stock cargado', html: summaryHtml(result) });
        location.reload();
    }
});
