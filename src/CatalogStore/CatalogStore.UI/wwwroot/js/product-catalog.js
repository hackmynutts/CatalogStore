document.addEventListener('DOMContentLoaded', function () {
    const grid = document.getElementById('catalogGrid');
    if (!grid) return;

    const cards = Array.from(grid.querySelectorAll('.catalog-card'));
    const pills = document.querySelectorAll('.catalog-pill');
    const searchInput = document.getElementById('catalogSearch');
    const noResults = document.getElementById('catalogNoResults');

    let activeCategoria = 'all';

    // Filtro, búsqueda y posición se guardan al salir (por ejemplo al abrir un detalle) y se devuelven
    // solo al volver atrás o recargar, no al entrar de nuevo al catálogo desde el menú.
    const STATE_KEY = 'catalog-state:' + location.pathname;

    function readReturnState() {
        try {
            const nav = performance.getEntriesByType('navigation')[0];
            const revisited = !!nav && (nav.type === 'reload' || nav.type === 'back_forward');
            const saved = sessionStorage.getItem(STATE_KEY);
            sessionStorage.removeItem(STATE_KEY);
            return revisited && saved ? JSON.parse(saved) : null;
        } catch (e) {
            return null;
        }
    }

    window.addEventListener('pagehide', function () {
        try {
            sessionStorage.setItem(STATE_KEY, JSON.stringify({
                categoria: activeCategoria,
                term: searchInput ? searchInput.value : '',
                scrollY: window.scrollY
            }));
        } catch (e) { }
    });

    function applyFilter() {
        const term = (searchInput.value || '').trim().toLowerCase();
        let visibleCount = 0;

        cards.forEach(function (card) {
            const matchesCategoria = activeCategoria === 'all' || card.getAttribute('data-categoria') === activeCategoria;
            const matchesTerm = !term ||
                card.getAttribute('data-name').includes(term) ||
                card.getAttribute('data-code').toLowerCase().includes(term);

            const visible = matchesCategoria && matchesTerm;
            card.hidden = !visible;
            if (visible) visibleCount++;
        });

        if (noResults) noResults.hidden = visibleCount > 0;
    }

    function selectPill(categoria) {
        activeCategoria = categoria;
        pills.forEach(function (p) {
            p.classList.toggle('active', p.getAttribute('data-categoria') === categoria);
        });
    }

    pills.forEach(function (pill) {
        pill.addEventListener('click', function () {
            selectPill(pill.getAttribute('data-categoria'));
            applyFilter();
        });
    });

    if (searchInput) {
        searchInput.addEventListener('input', applyFilter);
    }

    const returned = readReturnState();
    if (returned) {
        if (searchInput && returned.term) searchInput.value = returned.term;
        if (returned.categoria && Array.from(pills).some(function (p) { return p.getAttribute('data-categoria') === returned.categoria; })) {
            selectPill(returned.categoria);
        }
        applyFilter();
        if (returned.scrollY > 0) {
            // Los filtros cambian el alto de la página: se espera a que se pinte antes de devolver la posición.
            requestAnimationFrame(function () { window.scrollTo(0, returned.scrollY); });
        }
    }
});
