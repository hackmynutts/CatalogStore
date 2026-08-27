document.addEventListener('DOMContentLoaded', function () {
    if (typeof $ === 'undefined' || !$.fn.tablesorter) return;

    // Cada tabla .app-table busca su propio buscador por [data-table-search="<id-de-la-tabla>"]
    // y su propio paginador por id="<id-de-la-tabla>-pager". Si no los encuentra, sigue
    // funcionando igual (ordenable, sin filtro/paginación).
    $('.app-table').each(function () {
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
            $table.tablesorterPager({
                container: $pager,
                size: 10,
                output: '{startRow} – {endRow} de {totalRows}',
                storageKey: tableId + '-pager-state',
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
});
