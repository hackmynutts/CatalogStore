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
