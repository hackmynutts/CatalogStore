// Inicializa las tablas .app-table dentro de "root" (por defecto, toda la página).
// Se expone como window.initAppTables para las tablas que llegan después por AJAX
// (por ejemplo, las líneas de inventario en el detalle de una bodega).
//
// Paginación:
//  - Al cambiar de página con los botones del paginador, la vista sube al inicio de la tabla.
//  - Si la página se recarga después de modificar algo (location.reload()), o se vuelve atrás desde un detalle, la tabla vuelve a la misma página y no a la 1.
//    También se conserva cuando una tabla cargada por AJAX se vuelve a dibujar, si lleva el atributo data-keep-page
//    (solo las que se redibujan con los mismos datos; un modal que se reutiliza con otros datos debe empezar en la 1).
(function () {
    // Página actual de cada tabla mientras esta página siga abierta (1 = primera).
    var pages = {};
    var SCROLL_OFFSET = 90; // alto aproximado de la barra superior

    function storageKey(tableId) {
        return 'app-table-page:' + location.pathname + location.search + '#' + tableId;
    }

    // Devuelve a la página guardada una recarga (F5 o location.reload()) o volver atrás en el historial (botón "Volver" de un detalle).
    // Entrar de nuevo a la lista con un enlace empieza en la 1.
    function wasReloaded() {
        try {
            var nav = performance.getEntriesByType('navigation')[0];
            return !!nav && (nav.type === 'reload' || nav.type === 'back_forward');
        } catch (e) {
            return false;
        }
    }

    // El tamaño de página elegido (10/25/50/100) se recuerda mientras dure la sesión, también al entrar de nuevo a la lista.
    function sizeKey(tableId) {
        return 'app-table-size:' + location.pathname + '#' + tableId;
    }

    function savedSize($pager, tableId) {
        try {
            var size = parseInt(sessionStorage.getItem(sizeKey(tableId)), 10);
            var valid = $pager.find('.pagesize option').toArray().some(function (o) { return parseInt(o.value, 10) === size; });
            return valid ? size : 0;
        } catch (e) {
            return 0;
        }
    }

    function takeSavedPage(tableId) {
        try {
            var key = storageKey(tableId);
            var saved = sessionStorage.getItem(key);
            sessionStorage.removeItem(key);
            return saved ? parseInt(saved, 10) : 0;
        } catch (e) {
            return 0;
        }
    }

    var reloaded = wasReloaded();
    var savedOnLoad = {};

    // Antes de salir (incluye la recarga) se guarda la página de cada tabla.
    window.addEventListener('pagehide', function () {
        try {
            Object.keys(pages).forEach(function (tableId) {
                if (pages[tableId] > 1) sessionStorage.setItem(storageKey(tableId), String(pages[tableId]));
            });
        } catch (e) { }
    });

    function scrollToTableStart($table, $search) {
        var target = ($search && $search.length ? $search.first().closest('.app-table-search-wrap') : $()).get(0) || $table.get(0);
        if (!target) return;
        target.style.scrollMarginTop = SCROLL_OFFSET + 'px';
        target.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }

    window.initAppTables = function (root) {
        if (typeof $ === 'undefined' || !$.fn.tablesorter) return;

        // Cada tabla .app-table busca su propio buscador por [data-table-search="<id-de-la-tabla>"]
        // y su propio paginador por id="<id-de-la-tabla>-pager". Si no los encuentra, sigue
        // funcionando igual (ordenable, sin filtro/paginación).
        $(root || document).find('.app-table').each(function () {
            var $table = $(this);
            var tableId = $table.attr('id');
            var $search = tableId ? $('[data-table-search="' + tableId + '"]') : $();
            var $pager = tableId ? $('#' + tableId + '-pager') : $();

            $table.tablesorter({
                theme: 'default',
                widthFixed: false,
                widgets: ['filter'],
                widgetOptions: {
                    filter_columnFilters: false,
                    filter_liveSearch: true,
                    filter_external: $search.length ? $search : undefined,
                    filter_reset: '.js-table-filter-reset'
                }
            });

            if ($pager.length && $.fn.tablesorterPager) {
                // Página a la que hay que volver: la de esta misma página si la tabla se redibujó por AJAX (data-keep-page),
                // o la guardada si la página se recargó. Se lee una sola vez por tabla.
                var wanted = $table.is('[data-keep-page]') && pages[tableId] > 1 ? pages[tableId] : 0;
                if (!wanted && tableId) {
                    if (!(tableId in savedOnLoad)) savedOnLoad[tableId] = takeSavedPage(tableId);
                    wanted = reloaded ? savedOnLoad[tableId] : 0;
                    savedOnLoad[tableId] = 0;
                }
                var restoring = wanted > 1;
                var pageSize = (tableId && savedSize($pager, tableId)) || 10;
                var userPaged = false;

                // Solo los botones del paginador suben la vista: escribir en el buscador también reinicia la página y no debe mover la pantalla.
                // En fase de captura: el plugin detiene el evento en sus propios botones y un manejador normal no lo vería.
                $pager.get(0).addEventListener('click', function (e) {
                    if (e.target instanceof Element && e.target.closest('.first, .prev, .next, .last')) userPaged = true;
                }, true);
                $pager.get(0).addEventListener('change', function (e) {
                    if (e.target instanceof Element && e.target.closest('.pagesize')) userPaged = true;
                }, true);

                $table.on('pagerComplete', function () {
                    var config = $table[0].config;
                    var current = config && config.pager ? config.pager.page + 1 : 1;

                    if (restoring) {
                        restoring = false;
                        if (wanted > current) {
                            $table.trigger('pageSet', wanted);
                            return;
                        }
                    }

                    if (tableId) {
                        pages[tableId] = current;
                        try { sessionStorage.setItem(sizeKey(tableId), String(config.pager.size)); } catch (e) { }
                    }
                    if (userPaged) {
                        userPaged = false;
                        scrollToTableStart($table, $search);
                    }
                });

                $table.tablesorterPager({
                    container: $pager,
                    size: pageSize,
                    output: '{startRow} – {endRow} de {totalRows}',
                    // La página se maneja arriba: la del plugin la restauraba también al entrar de nuevo a la lista.
                    savePages: false,
                    cssFirst: '.first',
                    cssPrev: '.prev',
                    cssNext: '.next',
                    cssLast: '.last',
                    cssPageDisplay: '.pagedisplay',
                    cssPageSize: '.pagesize',
                    cssDisabled: 'disabled'
                });
            }
        });
    };

    document.addEventListener('DOMContentLoaded', function () {
        window.initAppTables(document);
    });
})();
