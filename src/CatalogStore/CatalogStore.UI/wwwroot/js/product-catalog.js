document.addEventListener('DOMContentLoaded', function () {
    const grid = document.getElementById('catalogGrid');
    if (!grid) return;

    const cards = Array.from(grid.querySelectorAll('.catalog-card'));
    const pills = document.querySelectorAll('.catalog-pill');
    const searchInput = document.getElementById('catalogSearch');
    const noResults = document.getElementById('catalogNoResults');

    let activeCategoria = 'all';

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

    pills.forEach(function (pill) {
        pill.addEventListener('click', function () {
            pills.forEach(p => p.classList.remove('active'));
            pill.classList.add('active');
            activeCategoria = pill.getAttribute('data-categoria');
            applyFilter();
        });
    });

    if (searchInput) {
        searchInput.addEventListener('input', applyFilter);
    }
});
