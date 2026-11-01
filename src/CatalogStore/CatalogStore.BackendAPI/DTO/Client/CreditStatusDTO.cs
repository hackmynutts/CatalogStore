using System.ComponentModel.DataAnnotations;

namespace CatalogStore.BackendAPI.DTO.Client
{
    // Estado de mora de un cliente, calculado al momento de pedirlo: no se guarda.
    public class CreditStatusDTO
    {
        public int ClientId { get; set; }
        // Ventas con saldo cuyo vencimiento ya pasó.
        public int OverdueCount { get; set; }
        // Orders:BlockOverdueSalesFrom. 0 = el bloqueo está apagado.
        public int Threshold { get; set; }
        // True si HOY no puede iniciar ni completar órdenes: alcanzó el límite y no tiene una excepción vigente.
        public bool IsBlocked { get; set; }
        // Alcanzó el límite pero un administrador le autorizó una excepción que todavía no vence.
        public bool OverrideActive { get; set; }
        public DateOnly? OverrideUntil { get; set; }
        public string? OverrideReason { get; set; }
        public string? OverrideBy { get; set; }
        // Texto listo para mostrar cuando IsBlocked.
        public string? Message { get; set; }
    }

    public class SetCreditOverrideDTO
    {
        [Display(Name = "Motivo")]
        [Required(ErrorMessage = "El campo {0} es obligatorio.")]
        [StringLength(250, ErrorMessage = "El {0} no puede pasar de {1} caracteres.")]
        public string Reason { get; set; } = string.Empty;
        // Último día en que la excepción vale (inclusive).
        [Display(Name = "Fecha límite")]
        [Required(ErrorMessage = "El campo {0} es obligatorio.")]
        public DateOnly? Until { get; set; }
    }
}
