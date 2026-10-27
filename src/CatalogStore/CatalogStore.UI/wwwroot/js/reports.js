// Reportes (Views/Reports/Index.cshtml): atajos de fechas y estado "Generando…" al descargar.
document.addEventListener('DOMContentLoaded', function () {
    const iso = d => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;

    function range(kind) {
        const now = new Date();
        const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
        switch (kind) {
            case 'month': return [new Date(today.getFullYear(), today.getMonth(), 1), today];
            case 'prev-month': return [new Date(today.getFullYear(), today.getMonth() - 1, 1), new Date(today.getFullYear(), today.getMonth(), 0)];
            case '30': return [new Date(today.getFullYear(), today.getMonth(), today.getDate() - 29), today];
            case 'year': return [new Date(today.getFullYear(), 0, 1), today];
        }
        return null;
    }

    document.querySelectorAll('.report-chip').forEach(chip => chip.addEventListener('click', function () {
        const r = range(chip.dataset.range);
        const form = chip.closest('form');
        if (!r || !form) return;
        form.querySelector('[name="from"]').value = iso(r[0]);
        form.querySelector('[name="to"]').value = iso(r[1]);
        chip.closest('.report-chips').querySelectorAll('.report-chip').forEach(c => c.classList.toggle('is-active', c === chip));
    }));

    // La descarga no navega la página, así que no hay evento de "terminó": el botón se libera pasados unos segundos.
    document.querySelectorAll('form[data-report]').forEach(form => form.addEventListener('submit', function () {
        const button = form.querySelector('button[type="submit"]');
        if (!button || button.disabled) return;
        const label = button.innerHTML;
        button.disabled = true;
        button.innerHTML = '<span class="spinner-border spinner-border-sm" aria-hidden="true"></span> Generando…';
        setTimeout(() => { button.disabled = false; button.innerHTML = label; }, 3500);
    }));
});
