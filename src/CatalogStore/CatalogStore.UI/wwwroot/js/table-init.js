document.addEventListener('DOMContentLoaded', function () {
    if (typeof $ === 'undefined' || !$.fn.tablesorter) return;

    // Cada tabla .app-table busca su propio buscador por [data-table-search="<id-de-la-tabla>"].
    // Si no lo encuentra, igual queda ordenable, solo sin filtro.
    $('.app-table').each(function () {
        var $table = $(this);
        var tableId = $table.attr('id');
        var $search = tableId ? $('[data-table-search="' + tableId + '"]') : $();

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
    });
});
