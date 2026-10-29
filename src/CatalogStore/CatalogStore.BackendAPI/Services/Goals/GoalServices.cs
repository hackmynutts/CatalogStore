using CatalogStore.BackendAPI.DTO.Common;
using CatalogStore.BackendAPI.DTO.Goals;
using CatalogStore.BackendAPI.Models.EventLogs;
using CatalogStore.BackendAPI.Models.Goal;
using CatalogStore.BackendAPI.Repository.Goals;
using CatalogStore.BackendAPI.Services.EventLogs;
using Humanizer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CatalogStore.BackendAPI.Services.Goals
{
    // Metas mensuales del vendedor. Tres metas por mes: vendido sin IVA, cobrado sin IVA y cantidad de ventas.
    // El vendedor se identifica por su correo: es lo que guarda SalesHeader.Seller (claim unique_name del JWT).
    // Meta en 0 = esa métrica no se mide. El mes se corta en hora local (Costa Rica), no en UTC.
    public class GoalServices : IGoalServices
    {
        private const string ModuleName = "Ventas/Metas";
        private const string GoalTable = "Goal";
        private const string SellerRole = "Vendedor";
        private const int HistoryMonths = 6;
        private const decimal MaxAmount = 9_999_999_999.99m; // decimal(12,2)
        private const int MaxCount = 100_000;

        private readonly IGoalRepository _goalRepository;
        private readonly UserManager<Data.ApplicationUser> _userManager;
        private readonly IEventlogServices _eventlogServices;
        private readonly TimeProvider _time;

        public GoalServices(IGoalRepository goalRepository, UserManager<Data.ApplicationUser> userManager,
                            IEventlogServices eventlogServices, TimeProvider time)
        {
            _goalRepository = goalRepository;
            _userManager = userManager;
            _eventlogServices = eventlogServices;
            _time = time;
        }

        // ───────── Lectura ─────────

        public async Task<MyProgressDTO> GetMyProgressAsync(string user, int? year, int? month)
        {
            var (y, m) = Resolve(year, month);
            var period = BuildPeriod(y, m);
            var (fromUtc, toUtc) = MonthRange(y, m);

            var goal = await _goalRepository.GetAsyncNoTracking(user, y, m);
            var row = (await _goalRepository.GetProgressAsync(fromUtc, toUtc, user)).FirstOrDefault();
            var full = (await _userManager.FindByEmailAsync(user))?.FullName?.Trim();

            return new MyProgressDTO { Period = period, Progress = BuildSeller(user, full, goal, row, period) };
        }

        public async Task<MyGoalDetailDTO> GetMyDetailAsync(string user, int? year, int? month)
        {
            var (y, m) = Resolve(year, month);
            var (fromUtc, toUtc) = MonthRange(y, m);
            var sales = await _goalRepository.GetSalesDetailAsync(fromUtc, toUtc, user);
            var payments = await _goalRepository.GetPaymentsDetailAsync(fromUtc, toUtc, user);

            return new MyGoalDetailDTO
            {
                Period = BuildPeriod(y, m),
                // Se suma sin redondear y se redondea al final, igual que las tarjetas (Metric): así los totales coinciden.
                SoldTotal = Money(sales.Sum(s => s.Subtotal)),
                CollectedTotal = Money(payments.Sum(p => p.CountedAmount)),
                Sales = sales.Select(s => new GoalSaleDTO
                {
                    SalesHeaderID = s.SalesHeaderID, SaleNumber = s.SaleNumber, SaleDate = s.SaleDate, ClientName = s.ClientName,
                    ApplyIva = s.ApplyIva, CountedAmount = Money(s.Subtotal), Total = s.Total,
                    PaymentStatus = s.PaymentStatus, PaymentStatusName = ((Models.SalesHeader.PaymentStatus)s.PaymentStatus).Humanize()
                }).ToList(),
                Payments = payments.Select(p => new GoalPaymentDTO
                {
                    SalesHeaderID = p.SalesHeaderID, SaleNumber = p.SaleNumber, PaymentDate = p.PaymentDate, ClientName = p.ClientName,
                    Method = ((Models.SalesPayment.PaymentMethod)p.Method).Humanize(), Amount = p.Amount, CountedAmount = Money(p.CountedAmount)
                }).ToList()
            };
        }

        public async Task<List<GoalHistoryDTO>> GetMyHistoryAsync(string user)
        {
            var (cy, cm) = Resolve(null, null);
            var months = Enumerable.Range(0, HistoryMonths)
                .Select(i => new DateTime(cy, cm, 1).AddMonths(-i))
                .ToList();
            var goals = (await _goalRepository.GetBySellerRangeAsync(user, months.Last().Year * 100 + months.Last().Month, cy * 100 + cm))
                .ToDictionary(g => g.Year * 100 + g.Month);

            var result = new List<GoalHistoryDTO>();
            foreach (var d in months)
            {
                var period = BuildPeriod(d.Year, d.Month);
                var (fromUtc, toUtc) = MonthRange(d.Year, d.Month);
                var row = (await _goalRepository.GetProgressAsync(fromUtc, toUtc, user)).FirstOrDefault();
                goals.TryGetValue(d.Year * 100 + d.Month, out var goal);
                result.Add(new GoalHistoryDTO { Year = d.Year, Month = d.Month, Progress = BuildSeller(user, null, goal, row, period) });
            }
            return result;
        }

        public async Task<TeamProgressDTO> GetTeamProgressAsync(int? year, int? month)
        {
            var (y, m) = Resolve(year, month);
            var period = BuildPeriod(y, m);
            var (fromUtc, toUtc) = MonthRange(y, m);

            var goals = (await _goalRepository.GetByMonthAsync(y, m)).ToDictionary(g => g.Seller, StringComparer.OrdinalIgnoreCase);
            var rows = (await _goalRepository.GetProgressAsync(fromUtc, toUtc, null)).ToDictionary(r => r.Seller, StringComparer.OrdinalIgnoreCase);

            // Los vendedores aparecen siempre; otro usuario (ej. un admin que vende) aparece si tiene meta o movimiento.
            var sellers = await _userManager.GetUsersInRoleAsync(SellerRole);
            var names = sellers.Select(s => s.Email!).Union(goals.Keys).Union(rows.Keys).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var fullNames = (await _userManager.Users.Where(u => names.Contains(u.Email!)).ToListAsync())
                .ToDictionary(u => u.Email!, u => u.FullName?.Trim() ?? string.Empty, StringComparer.OrdinalIgnoreCase);

            var list = names
                .Select(n => BuildSeller(n, fullNames.GetValueOrDefault(n), goals.GetValueOrDefault(n), rows.GetValueOrDefault(n), period))
                .OrderBy(s => string.IsNullOrEmpty(s.FullName) ? s.Seller : s.FullName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            return new TeamProgressDTO { Period = period, Sellers = list };
        }

        // ───────── Escritura ─────────

        public async Task<OperationResult> SetGoalAsync(SetGoalDTO dto, string user)
        {
            var seller = dto.Seller?.Trim() ?? string.Empty;
            var error = PeriodError(dto.Year, dto.Month) ?? EditablePeriodError(dto.Year, dto.Month) ?? TargetsError(dto);
            if (error is null && seller.Length == 0)
                error = "El vendedor es obligatorio.";
            if (error is not null)
                return OperationResult.Fail(error);

            var target = await _userManager.FindByEmailAsync(seller);
            if (target is null)
                return OperationResult.Fail($"El usuario '{seller}' no existe.");
            seller = target.Email!; // tal como está guardado: SalesHeader.Seller es el correo (claim unique_name del JWT)

            byte[]? expected = null;
            if (!string.IsNullOrWhiteSpace(dto.RowVersion))
            {
                try { expected = Convert.FromBase64String(dto.RowVersion); }
                catch (FormatException) { return OperationResult.Fail("La versión de la meta no es válida. Recargá la pantalla."); }
            }

            var now = _time.GetUtcNow().UtcDateTime;
            var isNew = false;
            Goal? goal = null;
            object? before = null;
            try
            {
                goal = await _goalRepository.GetAsync(seller, dto.Year, dto.Month);
                if (goal is null)
                {
                    isNew = true;
                    goal = new Goal { Seller = seller, Year = dto.Year, Month = dto.Month, CreatedBy = user, CreatedOn = now };
                    _goalRepository.Add(goal);
                }
                else
                {
                    before = Snapshot(goal);
                    if (expected is not null)
                        _goalRepository.SetExpectedRowVersion(goal, expected);
                    if (goal.SalesAmountTarget == dto.SalesAmountTarget && goal.CollectedAmountTarget == dto.CollectedAmountTarget
                        && goal.SalesCountTarget == dto.SalesCountTarget)
                        return OperationResult.Ok(goal.GoalID);
                    goal.ModifiedBy = user;
                    goal.ModifiedOn = now;
                }
                goal.SalesAmountTarget = dto.SalesAmountTarget;
                goal.CollectedAmountTarget = dto.CollectedAmountTarget;
                goal.SalesCountTarget = dto.SalesCountTarget;

                await _goalRepository.SaveChangesAsync();

                await _eventlogServices.LogAsync(isNew ? typeEvent.Add : typeEvent.Edit, ModuleName, GoalTable, goal.GoalID.ToString(),
                    isNew ? $"Meta de {seller} para {PeriodText(dto.Year, dto.Month)} creada."
                          : $"Meta de {seller} para {PeriodText(dto.Year, dto.Month)} actualizada.",
                    preData: before, postData: Snapshot(goal));
                return OperationResult.Ok(goal.GoalID);
            }
            catch (DbUpdateConcurrencyException)
            {
                _goalRepository.DiscardChanges();
                return OperationResult.Fail($"La meta de {seller} cambió mientras la editabas. Recargá la pantalla y volvé a intentar.");
            }
            catch (DbUpdateException) when (isNew)
            {
                // Dos administradores crearon la meta a la vez: el índice único deja pasar solo a uno.
                _goalRepository.DiscardChanges();
                return OperationResult.Fail($"Otra persona acaba de crear la meta de {seller} para ese mes. Recargá la pantalla.");
            }
            catch (Exception ex)
            {
                _goalRepository.DiscardChanges();
                await _eventlogServices.LogAsync(isNew ? typeEvent.AddFail : typeEvent.EditFail, ModuleName, GoalTable,
                    goal?.GoalID.ToString() ?? seller, "Excepción no controlada al guardar la meta.", stackTrace: ex.ToString());
                throw;
            }
        }

        public async Task<OperationResult> CopyPreviousMonthAsync(CopyGoalsDTO dto, string user)
        {
            var error = PeriodError(dto.Year, dto.Month) ?? EditablePeriodError(dto.Year, dto.Month);
            if (error is not null)
                return OperationResult.Fail(error);

            var prev = new DateTime(dto.Year, dto.Month, 1).AddMonths(-1);
            try
            {
                var source = (await _goalRepository.GetByMonthAsync(prev.Year, prev.Month))
                    .Where(g => g.SalesAmountTarget > 0 || g.CollectedAmountTarget > 0 || g.SalesCountTarget > 0)
                    .ToList();
                if (source.Count == 0)
                    return OperationResult.Fail($"No hay metas en {PeriodText(prev.Year, prev.Month)} para copiar.");

                var existing = (await _goalRepository.GetByMonthAsync(dto.Year, dto.Month)).Select(g => g.Seller).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var toCopy = source.Where(g => !existing.Contains(g.Seller)).ToList();
                if (toCopy.Count == 0)
                    return OperationResult.Fail($"Todos los vendedores con meta en {PeriodText(prev.Year, prev.Month)} ya tienen meta en {PeriodText(dto.Year, dto.Month)}.");

                var now = _time.GetUtcNow().UtcDateTime;
                foreach (var g in toCopy)
                    _goalRepository.Add(new Goal
                    {
                        Seller = g.Seller, Year = dto.Year, Month = dto.Month,
                        SalesAmountTarget = g.SalesAmountTarget, CollectedAmountTarget = g.CollectedAmountTarget, SalesCountTarget = g.SalesCountTarget,
                        CreatedBy = user, CreatedOn = now
                    });
                await _goalRepository.SaveChangesAsync();

                await _eventlogServices.LogAsync(typeEvent.Add, ModuleName, GoalTable, $"{dto.Year}-{dto.Month:D2}",
                    $"Metas de {PeriodText(prev.Year, prev.Month)} copiadas a {PeriodText(dto.Year, dto.Month)} ({toCopy.Count}).",
                    postData: toCopy.Select(g => g.Seller));
                return OperationResult.Ok(toCopy.Count);
            }
            catch (DbUpdateException)
            {
                _goalRepository.DiscardChanges();
                return OperationResult.Fail("Otra persona cambió las metas de ese mes mientras copiabas. Recargá la pantalla.");
            }
            catch (Exception ex)
            {
                _goalRepository.DiscardChanges();
                await _eventlogServices.LogAsync(typeEvent.AddFail, ModuleName, GoalTable, $"{dto.Year}-{dto.Month:D2}",
                    "Excepción no controlada al copiar las metas.", stackTrace: ex.ToString());
                throw;
            }
        }

        // ───────── Validación ─────────

        public static string? PeriodError(int? year, int? month)
        {
            if ((year is null) != (month is null))
                return "Indicá el año y el mes.";
            if (year is null)
                return null;
            if (year < 2024 || year > 2200 || month < 1 || month > 12)
                return "El mes o el año no son válidos.";
            return null;
        }

        // Editar el pasado cambiaría el historial de cumplimiento: solo del mes en curso en adelante.
        private string? EditablePeriodError(int year, int month)
        {
            var (cy, cm) = Resolve(null, null);
            return year * 100 + month < cy * 100 + cm
                ? "No se pueden cambiar las metas de un mes que ya cerró."
                : null;
        }

        private static string? TargetsError(SetGoalDTO dto)
        {
            if (dto.SalesAmountTarget < 0 || dto.CollectedAmountTarget < 0 || dto.SalesCountTarget < 0)
                return "Las metas no pueden ser negativas.";
            if (decimal.Round(dto.SalesAmountTarget, 2) != dto.SalesAmountTarget || decimal.Round(dto.CollectedAmountTarget, 2) != dto.CollectedAmountTarget)
                return "Los montos admiten como máximo dos decimales.";
            if (dto.SalesAmountTarget > MaxAmount || dto.CollectedAmountTarget > MaxAmount)
                return "El monto de la meta es demasiado grande.";
            if (dto.SalesCountTarget > MaxCount)
                return "La cantidad de ventas de la meta es demasiado grande.";
            return null;
        }

        // ───────── Cálculo ─────────

        private DateTime LocalNow => TimeZoneInfo.ConvertTime(_time.GetUtcNow(), _time.LocalTimeZone).DateTime;

        private (int Year, int Month) Resolve(int? year, int? month) =>
            year is null || month is null ? (LocalNow.Year, LocalNow.Month) : (year.Value, month.Value);

        // [1.º del mes, 1.º del mes siguiente) en hora local, convertido a UTC (SaleDate y PaymentDate se guardan en UTC).
        private (DateTime FromUtc, DateTime ToUtc) MonthRange(int year, int month)
        {
            var zone = _time.LocalTimeZone;
            var first = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Unspecified);
            return (TimeZoneInfo.ConvertTimeToUtc(first, zone), TimeZoneInfo.ConvertTimeToUtc(first.AddMonths(1), zone));
        }

        private PeriodDTO BuildPeriod(int year, int month)
        {
            var now = LocalNow;
            var days = DateTime.DaysInMonth(year, month);
            var key = year * 100 + month;
            var currentKey = now.Year * 100 + now.Month;
            var phase = key < currentKey ? GoalPhase.Past : key > currentKey ? GoalPhase.Future : GoalPhase.Current;
            var day = phase switch { GoalPhase.Past => days, GoalPhase.Future => 0, _ => now.Day };
            return new PeriodDTO
            {
                Year = year, Month = month, DaysInMonth = days, DayOfMonth = day, Phase = phase,
                DaysRemaining = phase switch { GoalPhase.Past => 0, GoalPhase.Future => days, _ => days - now.Day + 1 },
                Editable = phase != GoalPhase.Past
            };
        }

        private static SellerProgressDTO BuildSeller(string seller, string? fullName, Goal? goal, SellerProgressRow? row, PeriodDTO period) => new()
        {
            Seller = seller,
            FullName = fullName ?? string.Empty,
            HasGoal = goal is not null,
            RowVersion = goal is null ? null : Convert.ToBase64String(goal.RowVersion),
            Sold = Metric(row?.SoldAmount ?? 0m, goal?.SalesAmountTarget ?? 0m, period),
            Collected = Metric(row?.CollectedAmount ?? 0m, goal?.CollectedAmountTarget ?? 0m, period),
            SalesCount = Metric(row?.SalesCount ?? 0, goal?.SalesCountTarget ?? 0, period)
        };

        private static MetricProgressDTO Metric(decimal actual, decimal target, PeriodDTO period)
        {
            actual = Money(actual);
            var fraction = period.DaysInMonth == 0 ? 0m : (decimal)period.DayOfMonth / period.DaysInMonth;
            var remaining = Math.Max(target - actual, 0m);

            GoalStatus status;
            if (target <= 0) status = GoalStatus.NoGoal;
            else if (actual >= target) status = GoalStatus.Met;
            else status = period.Phase switch
            {
                GoalPhase.Past => GoalStatus.NotMet,
                GoalPhase.Future => GoalStatus.Upcoming,
                _ => actual >= target * fraction ? GoalStatus.OnTrack : GoalStatus.Behind
            };

            return new MetricProgressDTO
            {
                Actual = actual,
                Target = target,
                Percent = target > 0 ? Math.Round(actual / target * 100m, 1, MidpointRounding.AwayFromZero) : null,
                ExpectedAtPace = Money(target * fraction),
                Remaining = Money(remaining),
                PerDayNeeded = period.Phase == GoalPhase.Current && remaining > 0 && period.DaysRemaining > 0
                    ? Money(remaining / period.DaysRemaining) : null,
                Status = status
            };
        }

        private static decimal Money(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

        private static object Snapshot(Goal g) => new
        {
            g.Seller, g.Year, g.Month, g.SalesAmountTarget, g.CollectedAmountTarget, g.SalesCountTarget
        };

        private static string PeriodText(int year, int month) =>
            new DateTime(year, month, 1).ToString("MMMM yyyy", System.Globalization.CultureInfo.CurrentCulture);
    }
}
