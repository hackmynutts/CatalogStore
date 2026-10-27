using System.Globalization;

namespace CatalogStore.UI.Models.Goals
{
    // Espejo de DTO/Goals de la API (api/Goal). Los números de las enumeraciones tienen que coincidir con los del backend.
    public enum GoalPhase { Past = 0, Current = 1, Future = 2 }

    public enum GoalStatus { NoGoal = 0, Met = 1, OnTrack = 2, Behind = 3, NotMet = 4, Upcoming = 5 }

    public class GoalMetric
    {
        public decimal Actual { get; set; }
        public decimal Target { get; set; }
        public decimal? Percent { get; set; }
        public decimal ExpectedAtPace { get; set; }
        public decimal Remaining { get; set; }
        public decimal? PerDayNeeded { get; set; }
        public GoalStatus Status { get; set; }
    }

    public class GoalPeriod
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public int DaysInMonth { get; set; }
        public int DayOfMonth { get; set; }
        public int DaysRemaining { get; set; }
        public GoalPhase Phase { get; set; }
        public bool Editable { get; set; }
    }

    public class SellerProgress
    {
        public string Seller { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public bool HasGoal { get; set; }
        public GoalMetric Sold { get; set; } = new();
        public GoalMetric Collected { get; set; } = new();
        public GoalMetric SalesCount { get; set; } = new();
        public string? RowVersion { get; set; }

        public string DisplayName => string.IsNullOrWhiteSpace(FullName) ? Seller : FullName;
    }

    public class MyGoalsViewModel
    {
        public GoalPeriod Period { get; set; } = new();
        public SellerProgress Progress { get; set; } = new();
        public List<GoalHistoryItem> History { get; set; } = new();
    }

    public class GoalHistoryItem
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public SellerProgress Progress { get; set; } = new();
    }

    public class TeamGoalsViewModel
    {
        public GoalPeriod Period { get; set; } = new();
        public List<SellerProgress> Sellers { get; set; } = new();
    }

    // Lo que necesita la tarjeta de una métrica (Views/Goals/_GoalMetric).
    public record GoalMetricView(string Label, GoalMetric Metric, bool IsMoney, GoalPeriod Period);

    public static class GoalUi
    {
        private static readonly CultureInfo Es = new("es-CR");

        public static string MonthName(int year, int month)
        {
            var text = new DateTime(year, month, 1).ToString("MMMM yyyy", Es);
            return char.ToUpper(text[0], Es) + text[1..];
        }

        public static (int Year, int Month) Shift(int year, int month, int delta)
        {
            var d = new DateTime(year, month, 1).AddMonths(delta);
            return (d.Year, d.Month);
        }

        public static string Money(decimal value) => "₡" + value.ToString("N2");

        public static string Number(decimal value, bool isMoney) => isMoney ? Money(value) : value.ToString("N0");

        // Las metas de monto se piden redondas; en pantalla los ceros de los centavos sobran.
        public static string Target(decimal value, bool isMoney) =>
            isMoney ? "₡" + value.ToString(value % 1 == 0 ? "N0" : "N2") : value.ToString("N0");

        public static string StatusText(GoalStatus status) => status switch
        {
            GoalStatus.Met => "Cumplida",
            GoalStatus.OnTrack => "Al día",
            GoalStatus.Behind => "Atrasada",
            GoalStatus.NotMet => "No cumplida",
            GoalStatus.Upcoming => "Por empezar",
            _ => "Sin meta"
        };

        public static string StatusClass(GoalStatus status) => status switch
        {
            GoalStatus.Met => "goal-s-met",
            GoalStatus.OnTrack => "goal-s-ok",
            GoalStatus.Behind => "goal-s-behind",
            GoalStatus.NotMet => "goal-s-miss",
            _ => "goal-s-none"
        };

        // Ancho de la barra: 0–100 aunque se haya superado la meta.
        public static string BarWidth(GoalMetric m) =>
            Math.Clamp(m.Percent ?? 0m, 0m, 100m).ToString("0.#", CultureInfo.InvariantCulture) + "%";

        // Dónde va la marca del ritmo esperado (0–100): lo transcurrido del mes.
        public static string PacePosition(GoalPeriod p) =>
            (p.DaysInMonth == 0 ? 0m : Math.Clamp((decimal)p.DayOfMonth / p.DaysInMonth * 100m, 0m, 100m))
                .ToString("0.#", CultureInfo.InvariantCulture) + "%";

        public static string Percent(decimal? value) => value is null ? "—" : value.Value.ToString("0.#", CultureInfo.InvariantCulture) + " %"; // mismo punto decimal que los montos (₡1,234.50)

        // Un valor para un campo <input type="number">: punto decimal siempre, sin separador de miles.
        public static string Input(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
