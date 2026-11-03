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
