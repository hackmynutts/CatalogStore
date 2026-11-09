// Inicializa las tablas .app-table dentro de "root" (por defecto, toda la página).
// Se expone como window.initAppTables para las tablas que llegan después por AJAX
// (por ejemplo, las líneas de inventario en el detalle de una bodega).
//
// Paginación:
//  - Al cambiar de página con los botones del paginador, la vista sube al inicio de la tabla.
//  - Si la página se recarga después de modificar algo (location.reload()), o se vuelve atrás desde un detalle, la tabla vuelve a la misma página y no a la 1.
//    También se conserva cuando una tabla cargada por AJAX se vuelve a dibujar, si lleva el atributo data-keep-page
//    (solo las que se redibujan con los mismos datos; un modal que se reutiliza con otros datos debe empezar en la 1).
//
// Volver desde un detalle:
//  - Al abrir un enlace de una fila se guarda en qué fila iba, a qué altura de la pantalla estaba y cómo estaba la tabla
//    (búsqueda, orden, página y tamaño de página).
//  - Al volver atrás (botón "Volver", o el del navegador) se arma la tabla igual, se deja esa fila en el mismo lugar de la pantalla
//    y se resalta un momento para que se vea por dónde iba.
(function () {
    // Página actual de cada tabla mientras esta página siga abierta (1 = primera).
    var pages = {};
    // Tablas de esta página: sirven para guardar su estado al salir.
    var registry = {};
    var SCROLL_OFFSET = 90; // alto aproximado de la barra superior
    var ROW_MAX_AGE_MS = 3000; // una fila "abierta" hace más que esto no se considera el motivo de salir de la página

    function storageKey(tableId) {
        return 'app-table-page:' + location.pathname + location.search + '#' + tableId;
    }

    function viewKey(tableId) {
        return 'app-table-view:' + location.pathname + location.search + '#' + tableId;
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

    function readView(tableId) {
        try {
            var raw = sessionStorage.getItem(viewKey(tableId));
            return raw ? JSON.parse(raw) : null;
        } catch (e) {
            return null;
        }
    }

    function takeView(tableId) {
        var view = readView(tableId);
        try { sessionStorage.removeItem(viewKey(tableId)); } catch (e) { }
        return view;
    }

    // Identifica una fila por el primer enlace que tiene (casi siempre el del detalle) o por data-row-key.
    function rowKey(tr) {
        if (tr.dataset && tr.dataset.rowKey) return tr.dataset.rowKey;
        var a = tr.querySelector('a[href]');
        if (!a) return null;
        try { var url = new URL(a.getAttribute('href'), location.href); return url.pathname + url.search; } catch (e) { return a.getAttribute('href'); }
    }

    function findRow($table, key) {
        var rows = $table.find('tbody tr').toArray();
        for (var i = 0; i < rows.length; i++) if (rowKey(rows[i]) === key) return rows[i];
        return null;
    }

    // Foto del estado de la tabla. "row" solo cuando se sale por un enlace de esa fila.
    function captureView(tableId, row) {
        var t = registry[tableId];
        if (!t) return;
        var config = t.$table[0].config || {};
        var previous = readView(tableId);
        var view = {
            search: t.$search.length ? String(t.$search.val() || '') : '',
            sort: Array.isArray(config.sortList) ? config.sortList : [],
            scrollY: window.scrollY
        };
        if (row) view.row = row;
        else if (previous && previous.row && Date.now() - previous.row.t < ROW_MAX_AGE_MS) view.row = previous.row;
        try { sessionStorage.setItem(viewKey(tableId), JSON.stringify(view)); } catch (e) { }
    }

    function validSort(sort) {
        return Array.isArray(sort) && sort.every(function (s) { return Array.isArray(s) && Number.isInteger(s[0]) && (s[1] === 0 || s[1] === 1); }) ? sort : [];
    }

    var reloaded = wasReloaded();
    var savedOnLoad = {};
    var viewOnLoad = {};

    // Antes de salir (incluye la recarga) se guarda la página y el estado de cada tabla.
    window.addEventListener('pagehide', function () {
        try {
            Object.keys(pages).forEach(function (tableId) {
                if (pages[tableId] > 1) sessionStorage.setItem(storageKey(tableId), String(pages[tableId]));
            });
        } catch (e) { }
        Object.keys(registry).forEach(function (tableId) { captureView(tableId, null); });
    });

    // Al abrir el enlace de una fila: se guarda cuál fue y a qué altura de la pantalla estaba.
    document.addEventListener('click', function (e) {
        if (e.defaultPrevented || e.button !== 0 || e.ctrlKey || e.metaKey || e.shiftKey || e.altKey) return;
        var link = e.target instanceof Element ? e.target.closest('a[href]') : null;
        if (!link || link.target === '_blank') return;
        var tr = link.closest('.app-table tbody tr');
        var table = tr ? tr.closest('.app-table') : null;
        if (!tr || !table || !table.id || !registry[table.id]) return;
        var key = rowKey(tr);
        if (!key) return;
        captureView(table.id, { key: key, top: tr.getBoundingClientRect().top, t: Date.now() });
    }, true);

    // Si el navegador devuelve la lista desde su memoria, los datos pueden estar viejos (algo se editó en el detalle): se recarga
    // y el estado guardado vuelve a dejar la tabla donde estaba.
    window.addEventListener('pageshow', function (e) {
        if (e.persisted && document.querySelector('.app-table')) location.reload();
    });

    function scrollToTableStart($table, $search) {
        var target = ($search && $search.length ? $search.first().closest('.app-table-search-wrap') : $()).get(0) || $table.get(0);
        if (!target) return;
        target.style.scrollMarginTop = SCROLL_OFFSET + 'px';
        target.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }

    // Deja la fila a la misma altura de la pantalla que tenía al salir y la resalta un momento.
    function restorePosition($table, view) {
        if (!view) return;
        var target = view.row ? findRow($table, view.row.key) : null;
        if (target && target.offsetParent !== null) {
            var delta = target.getBoundingClientRect().top - view.row.top;
            window.scrollTo({ top: Math.max(0, window.scrollY + delta), left: 0, behavior: 'instant' });
            target.classList.add('row-returned');
            target.addEventListener('animationend', function () { target.classList.remove('row-returned'); }, { once: true });
        } else if (view.scrollY > 0) {
            window.scrollTo({ top: view.scrollY, left: 0, behavior: 'instant' });
        }
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

            // Estado guardado al salir (solo si se llega por recarga o volviendo atrás). Se lee una sola vez por tabla.
            var view = null;
            if (tableId) {
                if (!(tableId in viewOnLoad)) viewOnLoad[tableId] = takeView(tableId);
                view = reloaded ? viewOnLoad[tableId] : null;
                viewOnLoad[tableId] = null;
            }
            if (view && ('scrollRestoration' in history)) history.scrollRestoration = 'manual';

            var wantedSearch = view && view.search && $search.length ? view.search : '';
            var searchApplied = !wantedSearch;
            // La búsqueda guardada solo se puede aplicar cuando el filtro de tablesorter terminó de iniciar; antes no hace nada.
            function applySearch() {
                if (searchApplied) return;
                searchApplied = true;
                $search.val(wantedSearch).trigger('change');
            }
            $table.one('filterInit', applySearch);

            $table.tablesorter({
                theme: 'default',
                widthFixed: false,
                sortList: view ? validSort(view.sort) : [],
                widgets: ['filter'],
                widgetOptions: {
                    filter_columnFilters: false,
                    filter_liveSearch: true,
                    filter_external: $search.length ? $search : undefined,
                    filter_reset: '.js-table-filter-reset'
                }
            });
            if (tableId) registry[tableId] = { $table: $table, $search: $search };

            if ($pager.length && $.fn.tablesorterPager) {
                // Página a la que hay que volver: la de esta misma página si la tabla se redibujó por AJAX (data-keep-page),
                // o la guardada si la página se recargó. Se lee una sola vez por tabla.
                var wanted = $table.is('[data-keep-page]') && pages[tableId] > 1 ? pages[tableId] : 0;
                if (!wanted && tableId) {
                    if (!(tableId in savedOnLoad)) savedOnLoad[tableId] = takeSavedPage(tableId);
                    wanted = reloaded ? savedOnLoad[tableId] : 0;
                    savedOnLoad[tableId] = 0;
                }
                // Pasos de la restauración, en orden: búsqueda guardada (ya aplicada arriba) → página guardada → posición de la fila.
                var step = (wantedSearch || wanted > 1 || (view && (view.row || view.scrollY))) ? 'start' : 'done';
                // Si el filtro nunca avisa que está listo, la búsqueda se aplica igual pasado un momento.
                if (wantedSearch) setTimeout(function () { if (!searchApplied) applySearch(); }, 1500);
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

                function toPage(current) {
                    step = 'paging';
                    if (wanted > current) {
                        $table.trigger('pageSet', wanted);
                        return true;
                    }
                    return false;
                }

                function finish() {
                    step = 'done';
                    restorePosition($table, view);
                }

                $table.on('pagerComplete', function () {
                    var config = $table[0].config;
                    var current = config && config.pager ? config.pager.page + 1 : 1;

                    if (step === 'start') {
                        // Mientras la búsqueda guardada no se haya aplicado, esta tabla todavía no es la definitiva.
                        if (!searchApplied) return;
                        if (toPage(current)) return;
                        finish();
                    } else if (step === 'paging') {
                        finish();
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
            } else if (view) {
                // Tabla sin paginador: solo búsqueda y posición.
                setTimeout(function () { applySearch(); }, 600);
                setTimeout(function () { restorePosition($table, view); }, 1000);
            }
        });
    };

    document.addEventListener('DOMContentLoaded', function () {
        window.initAppTables(document);
    });
})();
