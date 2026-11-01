namespace CatalogStore.UI.Models.Client
{
    // Estado de mora del cliente tal como lo calcula la API (GET api/Client/{id}/credit-status).
    public class CreditStatusViewModel
    {
        public int ClientId { get; set; }
        public int OverdueCount { get; set; }
        // 0 = el bloqueo está apagado.
        public int Threshold { get; set; }
        public bool IsBlocked { get; set; }
        public bool OverrideActive { get; set; }
        public DateOnly? OverrideUntil { get; set; }
        public string? OverrideReason { get; set; }
        public string? OverrideBy { get; set; }
        public string? Message { get; set; }
    }
}
