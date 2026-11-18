// Botón "×" para borrar el texto de los campos de escritura de todo el sistema (búsquedas, formularios, modales).
// Es un único botón flotante que se coloca sobre el campo enfocado: no cambia el HTML de las páginas ni se rompe con los grupos de campos.
// Quedan fuera los campos de solo lectura o desactivados, los de contraseña y número (cantidades) y los que lleven data-no-clear.
(function () {
    const SELECTOR = 'input:not([type]), input[type="text"], input[type="search"], input[type="email"], input[type="tel"], input[type="url"], textarea';
    const SIZE = 22;   // mismo tamaño que en .input-clear (theme.css)
    const GAP = 8;     // distancia al borde derecho del campo
    let button = null, target = null, frame = 0;

    function eligible(el) {
        return el instanceof HTMLElement && el.matches(SELECTOR) && !el.readOnly && !el.disabled
            && !el.hasAttribute('data-no-clear') && !el.closest('[data-no-clear]') && el.getClientRects().length > 0;
    }

    function ensureButton() {
        if (button) return button;
        button = document.createElement('button');
        button.type = 'button';
        button.className = 'input-clear';
        button.tabIndex = -1; // el teclado ya puede borrar; así no estorba al tabular
        button.title = 'Borrar texto';
        button.setAttribute('aria-label', 'Borrar texto');
        button.textContent = '×';
        button.hidden = true;
        // El clic no le quita el foco al campo (ni cierra el modal que lo contiene).
        button.addEventListener('mousedown', (e) => e.preventDefault());
        button.addEventListener('click', clearText);
        document.body.appendChild(button);
        return button;
    }

    function hide() {
        if (button) button.hidden = true;
    }

    function place() {
        frame = 0;
        if (!target || !target.isConnected || !target.value || !eligible(target)) { hide(); return; }
        const rect = target.getBoundingClientRect();
        if (rect.bottom < 0 || rect.top > window.innerHeight) { hide(); return; }
        const b = ensureButton();
        b.hidden = false;
        b.style.left = Math.round(rect.right - SIZE - GAP) + 'px';
        b.style.top = Math.round(target.tagName === 'TEXTAREA' ? rect.top + 6 : rect.top + (rect.height - SIZE) / 2) + 'px';
    }

    function schedule() {
        if (!frame) frame = requestAnimationFrame(place);
    }

    function clearText() {
        if (!target) return;
        const el = target;
        el.value = '';
        // Los buscadores de las tablas y los filtros escuchan estos eventos.
        el.dispatchEvent(new Event('input', { bubbles: true }));
        el.dispatchEvent(new Event('search', { bubbles: true }));
        el.dispatchEvent(new Event('change', { bubbles: true }));
        el.focus();
        place();
    }

    document.addEventListener('focusin', (e) => {
        const el = e.target;
        if (!eligible(el)) { target = null; hide(); return; }
        target = el;
        el.classList.add('has-clear'); // deja lugar al botón a la derecha del texto
        schedule();
    });
    document.addEventListener('focusout', (e) => {
        if (e.target === target) { target = null; hide(); }
    });
    document.addEventListener('input', (e) => { if (e.target === target) schedule(); });
    window.addEventListener('resize', schedule);
    window.addEventListener('scroll', schedule, true);
})();
