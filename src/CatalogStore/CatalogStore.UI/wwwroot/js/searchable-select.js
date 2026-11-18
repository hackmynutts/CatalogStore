// Listas desplegables con búsqueda: un <select data-searchable> se convierte en un campo donde se escribe y la lista se filtra
// con lo escrito (sin importar mayúsculas ni tildes; todas las palabras tienen que aparecer). El <select> original queda oculto
// y sigue guardando el valor, así que los formularios, el name y los eventos "change" funcionan igual que antes.
// Se usa para elegir clientes (por nombre o cédula) en las listas largas.
(function () {
    const MAX_RESULTS = 100;
    const norm = (text) => (text || '').toLowerCase().normalize('NFD').replace(/[̀-ͯ]/g, '');
    let counter = 0;

    function enhance(select) {
        if (select.dataset.ssReady) return;
        select.dataset.ssReady = '1';

        const options = Array.from(select.options)
            .filter((o) => o.value !== '')
            .map((o) => ({ value: o.value, label: o.textContent.trim(), key: norm(o.textContent) }));
        const first = select.options[0];
        const placeholder = first && first.value === '' ? first.textContent.trim() : (select.dataset.placeholder || 'Escribí para buscar…');
        const required = select.required;
        const uid = 'ss' + (++counter);

        const wrap = document.createElement('div');
        wrap.className = 'ss';
        const input = document.createElement('input');
        input.type = 'text';
        input.className = 'form-control ss-input';
        input.id = (select.id || uid) + '-search';
        input.placeholder = placeholder;
        input.autocomplete = 'off';
        input.setAttribute('role', 'combobox');
        input.setAttribute('aria-autocomplete', 'list');
        input.setAttribute('aria-expanded', 'false');
        input.setAttribute('aria-controls', uid + '-list');
        const list = document.createElement('ul');
        list.className = 'ss-list';
        list.id = uid + '-list';
        list.setAttribute('role', 'listbox');
        list.hidden = true;

        // La etiqueta del <select> pasa a apuntar al campo de escritura.
        if (select.id) {
            const label = document.querySelector('label[for="' + select.id + '"]');
            if (label) label.htmlFor = input.id;
        }
        select.parentNode.insertBefore(wrap, select);
        wrap.append(input, list, select);
        select.hidden = true;
        // El <select> oculto no puede validar ("required"): la validación pasa al campo visible.
        select.required = false;

        let shown = [];
        let active = -1;

        const selectedLabel = () => {
            const o = options.find((x) => x.value === select.value);
            return o ? o.label : '';
        };
        function validate() {
            input.setCustomValidity(required && !select.value ? 'Elegí una opción de la lista.' : '');
        }
        function sync() {
            input.value = selectedLabel();
            validate();
        }
        function setActive(index) {
            const items = list.querySelectorAll('[role="option"]');
            items.forEach((li, i) => li.classList.toggle('is-active', i === index));
            active = index;
            if (items[index]) {
                input.setAttribute('aria-activedescendant', items[index].id);
                items[index].scrollIntoView({ block: 'nearest' });
            } else {
                input.removeAttribute('aria-activedescendant');
            }
        }
        function render(term) {
            const words = norm(term).split(/\s+/).filter(Boolean);
            shown = options.filter((o) => words.every((w) => o.key.includes(w))).slice(0, MAX_RESULTS);
            list.innerHTML = '';
            if (shown.length === 0) {
                const empty = document.createElement('li');
                empty.className = 'ss-empty';
                empty.textContent = 'Ningún resultado coincide.';
                list.appendChild(empty);
            }
            shown.forEach((o, i) => {
                const li = document.createElement('li');
                li.id = uid + '-opt' + i;
                li.setAttribute('role', 'option');
                li.dataset.value = o.value;
                li.textContent = o.label;
                if (o.value === select.value) li.setAttribute('aria-selected', 'true');
                list.appendChild(li);
            });
            setActive(shown.length ? 0 : -1);
        }
        function open() {
            if (!list.hidden) return;
            list.hidden = false;
            input.setAttribute('aria-expanded', 'true');
        }
        function close() {
            list.hidden = true;
            input.setAttribute('aria-expanded', 'false');
            input.removeAttribute('aria-activedescendant');
        }
        function choose(value) {
            const changed = select.value !== value;
            select.value = value;
            sync();
            close();
            if (changed) select.dispatchEvent(new Event('change', { bubbles: true }));
        }

        input.addEventListener('focus', () => { input.select(); render(''); open(); });
        input.addEventListener('click', () => { if (list.hidden) { render(''); open(); } });
        input.addEventListener('input', () => {
            // Borrar todo el texto (o usar la "×") quita la elección. Mientras haya texto se conserva la anterior hasta elegir otra;
            // si se sale del campo sin elegir, vuelve a mostrarse el cliente elegido.
            if (input.value === '' && select.value !== '') {
                select.value = '';
                select.dispatchEvent(new Event('change', { bubbles: true }));
            }
            validate();
            render(input.value);
            open();
        });
        input.addEventListener('keydown', (e) => {
            if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
                e.preventDefault();
                if (list.hidden) { render(input.value); open(); return; }
                if (shown.length) setActive((active + (e.key === 'ArrowDown' ? 1 : shown.length - 1)) % shown.length);
            } else if (e.key === 'Enter') {
                if (!list.hidden) {
                    e.preventDefault(); // con la lista abierta, Enter elige y no envía el formulario
                    if (shown[active]) choose(shown[active].value);
                }
            } else if (e.key === 'Escape') {
                if (!list.hidden) { e.stopPropagation(); sync(); close(); }
            }
        });
        input.addEventListener('blur', () => { sync(); close(); });
        // Elegir con el mouse sin que el campo pierda el foco antes de tiempo.
        list.addEventListener('mousedown', (e) => e.preventDefault());
        list.addEventListener('click', (e) => {
            const li = e.target.closest('[role="option"]');
            if (li) choose(li.dataset.value);
        });
        const form = select.closest('form');
        if (form) form.addEventListener('reset', () => setTimeout(sync, 0));

        sync();
    }

    window.initSearchableSelects = function (root) {
        (root || document).querySelectorAll('select[data-searchable]').forEach(enhance);
    };
    document.addEventListener('DOMContentLoaded', () => window.initSearchableSelects(document));
})();
