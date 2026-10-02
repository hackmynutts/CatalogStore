// Movimientos manuales de stock en el detalle de una línea (Views/InventoryLine/Details.cshtml).
// El historial se renderiza en el servidor; después de registrar un movimiento se recarga la página
// para que también se actualice la tarjeta de Existencias.
document.addEventListener('DOMContentLoaded', function () {
    const addBtn = document.getElementById('btn-add-transaction');
    const modalEl = document.getElementById('lineModal');
    if (!addBtn || !modalEl) return;

    // El modal es el mismo que usa inventory-line.js para Editar: se reusa su instancia en vez de crear otra.
    const modal = bootstrap.Modal.getOrCreateInstance(modalEl);
    const modalContent = document.getElementById('lineModalContent');
    const lineId = addBtn.getAttribute('data-line-id');

    const AJUSTE_POSITIVO = '6';
    const AJUSTE_NEGATIVO = '7';

    function showApiError(result, fallbackMessage) {
        let messages = null;
        let specificMessage = null;

        try {
            const parsed = JSON.parse(result.details);
            if (parsed && parsed.errors) {
                messages = Object.values(parsed.errors).flat();
            } else if (parsed && parsed.message) {
                specificMessage = parsed.message;
            }
        } catch (e) { }

        if (messages && messages.length) {
            Swal.fire({ icon: 'error', title: 'Revisá estos campos', html: messages.join('<br>') });
        } else {
            Swal.fire('Error', specificMessage || result.message || fallbackMessage, 'error');
        }
    }

    // Ajustes: la razón pasa a ser obligatoria. Ajuste negativo: tope = disponible.
    function wireTypeRules(form) {
        const type = form.querySelector('#Type');
        const quantity = form.querySelector('#TransactionQuantity');
        const reason = form.querySelector('#Reason');
        const reasonMark = form.querySelector('#transactionReasonRequired');
        const maxHint = form.querySelector('#transactionMaxHint');
        const available = form.getAttribute('data-available');

        function apply() {
            const isAdjustment = type.value === AJUSTE_POSITIVO || type.value === AJUSTE_NEGATIVO;
            const isNegative = type.value === AJUSTE_NEGATIVO;

            reason.required = isAdjustment;
            reasonMark.classList.toggle('d-none', !isAdjustment);

            quantity.max = isNegative ? available : '1000000';
            maxHint.classList.toggle('d-none', !isNegative);
        }

        type.addEventListener('change', apply);
        apply();
    }

    function wireForm(form) {
        form.addEventListener('submit', async function (e) {
            e.preventDefault();
            const response = await fetch('/InventoryTransaction/Create', { method: 'POST', body: new FormData(form) });
            const result = await response.json();

            if (result.success) {
                modal.hide();
                Swal.fire({ icon: 'success', title: 'Movimiento registrado', timer: 1500, showConfirmButton: false })
                    .then(() => location.reload());
            } else {
                showApiError(result, 'No se pudo registrar el movimiento.');
            }
        });
    }

    addBtn.addEventListener('click', async function () {
        const response = await fetch(`/InventoryTransaction/CreatePartial/${lineId}`);
        if (!response.ok) {
            Swal.fire('Error', 'No se pudo cargar el formulario.', 'error');
            return;
        }
        modalContent.innerHTML = await response.text();
        modal.show();

        const form = modalContent.querySelector('#createTransactionForm');
        if (!form) return;
        wireTypeRules(form);
        wireForm(form);
    });
});
