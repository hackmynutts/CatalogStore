document.addEventListener('DOMContentLoaded', function () {
    const grid = document.getElementById('catalogGrid');
    if (!grid) return;

    const cards = Array.from(grid.querySelectorAll('.catalog-card'));
    const pills = document.querySelectorAll('.catalog-pill');
    const searchInput = document.getElementById('catalogSearch');
    const noResults = document.getElementById('catalogNoResults');

    let activeCategoria = 'all';

    // Fotos verticales: se muestran completas (contain) en vez de recortarlas; las demás llenan el recuadro (cover).
    function fitImage(img) {
        if (img.naturalWidth && img.naturalHeight / img.naturalWidth > 1.2) img.classList.add('is-portrait');
    }
    grid.querySelectorAll('.catalog-card-img').forEach(function (img) {
        if (img.complete) fitImage(img);
        else img.addEventListener('load', function () { fitImage(img); }, { once: true });
    });

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

    // Tarjeta cuyo "Ver detalle" se abrió: se recuerda cuál y a qué altura de la pantalla estaba para devolverla al mismo lugar.
    let openedCard = null;
    grid.addEventListener('click', function (e) {
        if (e.defaultPrevented || e.button !== 0 || e.ctrlKey || e.metaKey || e.shiftKey || e.altKey) return;
        const link = e.target instanceof Element ? e.target.closest('a[href]') : null;
        const card = link ? link.closest('.catalog-card') : null;
        if (!card || link.target === '_blank') return;
        openedCard = { id: card.getAttribute('data-product-id'), top: card.getBoundingClientRect().top, t: Date.now() };
        saveState();
    }, true);

    function saveState() {
        try {
            const state = {
                categoria: activeCategoria,
                term: searchInput ? searchInput.value : '',
                scrollY: window.scrollY
            };
            if (openedCard && Date.now() - openedCard.t < 3000) state.card = openedCard;
            sessionStorage.setItem(STATE_KEY, JSON.stringify(state));
        } catch (e) { }
    }
    window.addEventListener('pagehide', saveState);

    // Si el navegador devuelve el catálogo desde su memoria, el stock y los precios pueden estar viejos: se recarga y el estado guardado lo deja igual.
    window.addEventListener('pageshow', function (e) { if (e.persisted) location.reload(); });

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
        if ('scrollRestoration' in history) history.scrollRestoration = 'manual';

        // Deja la tarjeta que se abrió a la misma altura de la pantalla (o, sin ella, la posición guardada). Las imágenes cargan después
        // y mueven el diseño, por eso se repite cuando termina de cargar la página.
        const restoreScroll = function (highlight) {
            const card = returned.card ? grid.querySelector('.catalog-card[data-product-id="' + returned.card.id + '"]:not([hidden])') : null;
            if (card) {
                window.scrollTo({ top: Math.max(0, window.scrollY + card.getBoundingClientRect().top - returned.card.top), left: 0, behavior: 'instant' });
                if (highlight) {
                    card.classList.add('card-returned');
                    card.addEventListener('animationend', function () { card.classList.remove('card-returned'); }, { once: true });
                }
            } else if (returned.scrollY > 0) {
                window.scrollTo({ top: returned.scrollY, left: 0, behavior: 'instant' });
            }
        };
        requestAnimationFrame(function () { restoreScroll(true); });
        window.addEventListener('load', function () { restoreScroll(false); }, { once: true });
    }
});
