document.addEventListener('DOMContentLoaded', function () {
    function buildSection(label, value) {
        const wrap = document.createElement('div');
        wrap.className = 'mb-2 text-start';

        const title = document.createElement('div');
        title.className = 'fw-bold small text-muted';
        title.textContent = label;

        const pre = document.createElement('pre');
        pre.className = 'bg-light border rounded p-2 small mb-0';
        pre.style.maxHeight = '200px';
        pre.style.overflow = 'auto';
        pre.textContent = value && value.length ? value : '—';

        wrap.appendChild(title);
        wrap.appendChild(pre);
        return wrap;
    }

    document.querySelectorAll('.btn-view-eventlog').forEach(function (btn) {
        btn.addEventListener('click', function () {
            const container = document.createElement('div');
            container.appendChild(buildSection('Antes', btn.getAttribute('data-pre')));
            container.appendChild(buildSection('Después', btn.getAttribute('data-post')));
            container.appendChild(buildSection('Stack trace', btn.getAttribute('data-stack')));

            Swal.fire({
                title: btn.getAttribute('data-accion') || 'Detalle del evento',
                html: container,
                width: 640,
                confirmButtonText: 'Cerrar'
            });
        });
    });
});
