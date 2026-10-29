using System.ComponentModel.DataAnnotations;

namespace CatalogStore.BackendAPI.DTO.Goals
{
    // Dónde va el avance respecto a la meta. Past = mes cerrado, Current = mes en curso, Future = mes que no empezó.
    public enum GoalPhase { Past = 0, Current = 1, Future = 2 }

    public enum GoalStatus
    {
        NoGoal = 0,    // meta en 0: no se mide
        Met = 1,       // llegó a la meta
        OnTrack = 2,   // mes en curso y va al ritmo o mejor
        Behind = 3,    // mes en curso y va por debajo del ritmo
        NotMet = 4,    // mes cerrado sin llegar
        Upcoming = 5   // mes que no empezó
    }

    // Una de las tres metas (vendido, cobrado, cantidad de ventas).
    public class MetricProgressDTO
    {
        public decimal Actual { get; set; }
        public decimal Target { get; set; }
        // null si no hay meta. Puede pasar de 100.
        public decimal? Percent { get; set; }
        // Cuánto debería llevar a hoy para cumplir al ritmo del mes (Target × días transcurridos / días del mes).
        public decimal ExpectedAtPace { get; set; }
        public decimal Remaining { get; set; }
        // Lo que falta dividido entre los días que quedan (incluido hoy). Solo mes en curso y meta sin cumplir.
        public decimal? PerDayNeeded { get; set; }
        public GoalStatus Status { get; set; }
    }

    public class PeriodDTO
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public int DaysInMonth { get; set; }
        // Día del mes en hora local; 0 si el mes no empezó y DaysInMonth si ya cerró.
        public int DayOfMonth { get; set; }
        public int DaysRemaining { get; set; }
        public GoalPhase Phase { get; set; }
        // Editar metas solo se permite del mes en curso en adelante.
        public bool Editable { get; set; }
    }

    public class SellerProgressDTO
    {
        public string Seller { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public bool HasGoal { get; set; }
        public MetricProgressDTO Sold { get; set; } = new();
        public MetricProgressDTO Collected { get; set; } = new();
        public MetricProgressDTO SalesCount { get; set; } = new();
        // Para guardar con control de concurrencia (base64). null si todavía no tiene meta.
        public string? RowVersion { get; set; }
    }

    public class MyProgressDTO
    {
        public PeriodDTO Period { get; set; } = new();
        public SellerProgressDTO Progress { get; set; } = new();
    }

    public class TeamProgressDTO
    {
        public PeriodDTO Period { get; set; } = new();
        public List<SellerProgressDTO> Sellers { get; set; } = new();
    }

    // Un mes del historial del vendedor, con el % de cada meta (null si ese mes no tenía meta de esa métrica).
    public class GoalHistoryDTO
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public SellerProgressDTO Progress { get; set; } = new();
    }

    // Una venta que cuenta en el "vendido" y en la "cantidad de ventas" del mes. Montos sin IVA salvo Total.
    public class GoalSaleDTO
    {
        public int SalesHeaderID { get; set; }
        public string SaleNumber { get; set; } = string.Empty;
        public DateTime SaleDate { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public bool ApplyIva { get; set; }
        // Lo que suma a la meta de vendido (subtotal sin IVA, ya con descuentos).
        public decimal CountedAmount { get; set; }
        public decimal Total { get; set; }
        public int PaymentStatus { get; set; }
        public string PaymentStatusName { get; set; } = string.Empty;
    }

    // Un abono que cuenta en el "cobrado" del mes. CountedAmount = la parte sin IVA del abono.
    public class GoalPaymentDTO
    {
        public int SalesHeaderID { get; set; }
        public string SaleNumber { get; set; } = string.Empty;
        public DateTime PaymentDate { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public string Method { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public decimal CountedAmount { get; set; }
    }

    // El detalle detrás de las tres tarjetas de un mes: qué ventas y qué abonos suman. Los totales coinciden con Progress.
    public class MyGoalDetailDTO
    {
        public PeriodDTO Period { get; set; } = new();
        public decimal SoldTotal { get; set; }
        public decimal CollectedTotal { get; set; }
        public List<GoalSaleDTO> Sales { get; set; } = new();
        public List<GoalPaymentDTO> Payments { get; set; } = new();
    }

    public class SetGoalDTO
    {
        [Required(ErrorMessage = "El vendedor es obligatorio.")]
        [StringLength(140)]
        public string Seller { get; set; } = string.Empty;
        public int Year { get; set; }
        public int Month { get; set; }
        public decimal SalesAmountTarget { get; set; }
        public decimal CollectedAmountTarget { get; set; }
        public int SalesCountTarget { get; set; }
        // El valor que la pantalla recibió al abrir la meta: si otra persona la cambió antes, no se pisa.
        public string? RowVersion { get; set; }
    }

    public class CopyGoalsDTO
    {
        public int Year { get; set; }
        public int Month { get; set; }
    }
}
