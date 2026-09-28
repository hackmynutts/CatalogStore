// Revela los bloques .lp-reveal al entrar en pantalla. Solo actúa si el <html> tiene .lp-motion
// (el usuario no pidió movimiento reducido); si no, los bloques ya están visibles por CSS.
document.addEventListener('DOMContentLoaded', function () {
    if (!document.documentElement.classList.contains('lp-motion')) return;

    const items = document.querySelectorAll('.lp-reveal');

    // Escalonado dentro de cada grupo (tiles del bento, pasos del flujo).
    document.querySelectorAll('.lp-bento, .lp-flow').forEach(function (group) {
        group.querySelectorAll('.lp-reveal').forEach(function (el, i) {
            el.style.setProperty('--lp-i', i);
        });
    });

    if (!('IntersectionObserver' in window)) {
        items.forEach(function (el) { el.classList.add('is-visible'); });
        return;
    }

    const observer = new IntersectionObserver(function (entries) {
        entries.forEach(function (entry) {
            if (entry.isIntersecting) {
                entry.target.classList.add('is-visible');
                observer.unobserve(entry.target);
            }
        });
    }, { threshold: 0.15, rootMargin: '0px 0px -40px 0px' });

    items.forEach(function (el) { observer.observe(el); });
});
