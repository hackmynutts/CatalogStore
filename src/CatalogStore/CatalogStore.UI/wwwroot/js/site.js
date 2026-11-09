// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Submenús anidados del navbar (.dropend dentro de otro .dropdown-menu):
// 1) Al hacer clic en el toggle hijo, el listener global de Bootstrap para "cerrar otros
//    dropdowns abiertos" interpreta al padre como "otro dropdown" y lo cierra, aunque el hijo
//    está adentro suyo. Frenar la propagación en fase de captura evita que ese listener global
//    vea el clic, así el ancestro queda abierto.
// 2) Pero eso también apaga el cierre automático entre HERMANOS al mismo nivel (ej: abrís
//    "Administración" y después "Ventas", los dos quedan abiertos y se superponen en desktop).
//    Por eso cerramos los hermanos a mano antes de que el propio toggle abra el suyo.
// SweetAlert encima de un offcanvas o modal de Bootstrap: el "focus trap" de Bootstrap escucha focusin en document
// y devuelve el foco al panel apenas sale de él, así que los inputs del Swal no se pueden usar (solo las flechas del
// número funcionaban). Se corta el evento en captura cuando el foco va a un Swal, antes de que llegue a Bootstrap.
window.addEventListener('focusin', function (e) {
    if (e.target instanceof Element && e.target.closest('.swal2-container')) {
        e.stopImmediatePropagation();
    }
}, true);

document.addEventListener('DOMContentLoaded', function () {
    document.querySelectorAll('.dropend > .dropdown-toggle').forEach(function (toggle) {
        toggle.addEventListener('click', function (e) {
            e.stopPropagation();

            var parentUl = toggle.closest('li').parentElement;
            parentUl.querySelectorAll(':scope > li.dropend > .dropdown-toggle').forEach(function (sibling) {
                if (sibling !== toggle) {
                    var instance = bootstrap.Dropdown.getInstance(sibling);
                    if (instance) instance.hide();
                }
            });
        }, true);
    });
});

// Muestra en un SweetAlert por qué falló una acción. result = { message, errors: [..] } como lo arman los controladores de la UI:
// message es el título y errors cada motivo (si son varios validaciones, salen en una lista).
window.showApiError = function (result, fallbackTitle) {
    const title = (result && result.message) || fallbackTitle || 'No se pudo completar la acción';
    const errors = result && Array.isArray(result.errors) ? result.errors.filter(Boolean) : [];
    const options = { icon: 'error', title, confirmButtonText: 'Entendido' };
    if (errors.length === 1) {
        options.text = errors[0];
    } else if (errors.length > 1) {
        const list = document.createElement('ul');
        list.className = 'text-start mb-0';
        list.style.fontSize = '0.95rem';
        errors.forEach(function (message) {
            const item = document.createElement('li');
            item.textContent = message;
            list.appendChild(item);
        });
        options.html = list;
    }
    return Swal.fire(options);
};

// "Volver" de las páginas de detalle (enlaces con class="js-back"): si se llegó desde otra página del sitio
// (catálogo, lista, venta...), vuelve exactamente a esa página con el historial del navegador, no a la lista fija.
// Así se conserva la página de la tabla, el filtro, la fila y la posición. Sin historial útil (enlace directo) sigue el href.
// Con class="js-back-exact" (botones con destino concreto, como "Volver a la venta") solo se usa el historial si la página
// anterior es justo ese destino; si no, se va al enlace.
document.addEventListener('click', function (e) {
    if (e.defaultPrevented || e.button !== 0 || e.ctrlKey || e.metaKey || e.shiftKey || e.altKey) return;
    const link = e.target instanceof Element ? e.target.closest('a.js-back, a.js-back-exact') : null;
    if (!link) return;
    try {
        if (!document.referrer || history.length < 2) return;
        const from = new URL(document.referrer);
        if (from.origin !== location.origin || from.pathname === location.pathname) return;
        if (/^\/(Account|Home\/(Error|StatusCode))/i.test(from.pathname)) return;
        if (link.classList.contains('js-back-exact') && from.pathname.toLowerCase() !== new URL(link.href, location.href).pathname.toLowerCase()) return;
        e.preventDefault();
        history.back();
    } catch (err) { }
});

// Modales largos: si el formulario no cabe en la altura de la pantalla (habría scroll), el modal se ensancha y los campos
// pasan a dos columnas (tres si hace falta) en vez de alargarse. Si cabe, el modal queda exactamente como está.
// Solo aplica en pantallas anchas y a formularios con varios campos; las tablas y listas no se tocan.
(function () {
    const MIN_WIDTH = 992;   // por debajo no hay espacio para columnas
    const MIN_FIELDS = 4;    // menos campos que esto no es un "formulario largo"
    const THREE_COLUMNS_FROM = 8;

    function overflows(modalEl) {
        return modalEl.scrollHeight > modalEl.clientHeight + 1;
    }

    // Recalcula la clase del modal. Se llama con el modal ya medible (display:block).
    function fit(modalEl) {
        const dialog = modalEl.querySelector('.modal-dialog');
        if (!dialog || dialog.classList.contains('modal-dialog-scrollable')) return;
        dialog.classList.remove('modal-fit-2', 'modal-fit-3');
        if (window.innerWidth < MIN_WIDTH) return;

        // La animación de entrada desplaza el diálogo 50px y eso cuenta como alto: se mide sin ella.
        const previous = { transform: dialog.style.transform, transition: dialog.style.transition };
        dialog.style.transition = 'none';
        dialog.style.transform = 'none';
        try {
            if (!overflows(modalEl)) return;
            const body = dialog.querySelector('.modal-body');
            const fields = body ? body.querySelectorAll(':scope > .mb-3').length : 0;
            if (fields < MIN_FIELDS) return;

            dialog.classList.add('modal-fit-2');
            if (overflows(modalEl) && fields >= THREE_COLUMNS_FROM) {
                dialog.classList.remove('modal-fit-2');
                dialog.classList.add('modal-fit-3');
            }
        } finally {
            dialog.style.transform = previous.transform;
            dialog.style.transition = previous.transition;
        }
    }

    function measure(modalEl) {
        if (getComputedStyle(modalEl).display !== 'none') { fit(modalEl); return; }
        // Todavía oculto (evento "show"): se muestra sin verse el instante que dura la medición.
        modalEl.style.visibility = 'hidden';
        modalEl.style.display = 'block';
        try { fit(modalEl); }
        finally { modalEl.style.display = ''; modalEl.style.visibility = ''; }
    }

    document.addEventListener('show.bs.modal', function (e) {
        const modalEl = e.target;
        if (!(modalEl instanceof Element) || !modalEl.classList.contains('modal')) return;
        measure(modalEl);

        // Si el contenido cambia con el modal abierto (por ejemplo un paso nuevo del formulario), se vuelve a ajustar.
        const content = modalEl.querySelector('.modal-content');
        if (content && !modalEl.dataset.fitObserved) {
            modalEl.dataset.fitObserved = '1';
            let queued = false;
            new MutationObserver(function () {
                if (queued) return;
                queued = true;
                requestAnimationFrame(function () { queued = false; measure(modalEl); });
            }).observe(content, { childList: true });
        }
    });
})();
