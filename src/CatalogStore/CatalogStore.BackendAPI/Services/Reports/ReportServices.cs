using CatalogStore.BackendAPI.DTO.Goals;
using CatalogStore.BackendAPI.Models.Invoice;
using CatalogStore.BackendAPI.Models.SalesHeader;
using CatalogStore.BackendAPI.Repository.Reports;
using CatalogStore.BackendAPI.Services.Goals;
using Humanizer;
using System.Globalization;

namespace CatalogStore.BackendAPI.Services.Reports
{
    public static class ReportKeys
    {
        public const string Sales = "sales";
        public const string Receivables = "receivables";
        public const string Payments = "payments";
        public const string Products = "products";
        public const string Goals = "goals";
        public const string Stock = "stock";
        public const string Statement = "statement";

        // El vendedor solo puede pedir estos y siempre sobre sus propias ventas; el resto trae costos y datos de todos.
        public static readonly string[] ForSellers = { Sales, Receivables, Payments, Statement };
        public static readonly string[] All = { Sales, Receivables, Payments, Products, Goals, Stock, Statement };
    }

    public record ReportRequest(string Key, DateOnly? From, DateOnly? To, string? Seller, bool IncludeVoided, int? Year, int? Month, int? ClientId = null);
    public record ReportFile(string FileName, byte[] Bytes);
    public record ReportResult(ReportFile? File, string? Error)
    {
        public bool Ok => File is not null;
        public static ReportResult Fail(string error) => new(null, error);
    }

    public interface IReportServices
    {
        // Arma el reporte como libro de Excel (.xlsx). Los errores de petición vuelven en Error, no como excepciones.
        Task<ReportResult> BuildAsync(ReportRequest request, string user, bool isAdmin);
    }

    public class ReportServices : IReportServices
    {
        private const int MaxDays = 366;
        private static readonly CultureInfo Es = new("es-CR");

        private readonly IReportRepository _repository;
        private readonly IGoalServices _goalServices;
        private readonly TimeProvider _time;

        public ReportServices(IReportRepository repository, IGoalServices goalServices, TimeProvider time)
        {
            _repository = repository;
            _goalServices = goalServices;
            _time = time;
        }

        public async Task<ReportResult> BuildAsync(ReportRequest request, string user, bool isAdmin)
        {
            var key = request.Key?.Trim().ToLowerInvariant() ?? string.Empty;
            if (!ReportKeys.All.Contains(key)) return ReportResult.Fail("Ese reporte no existe.");
            if (!isAdmin && !ReportKeys.ForSellers.Contains(key)) return ReportResult.Fail("No tenés permiso para este reporte.");

            // El vendedor ve solo lo suyo; el administrador puede filtrar por vendedor o verlos todos.
            var seller = isAdmin ? (string.IsNullOrWhiteSpace(request.Seller) ? null : request.Seller.Trim()) : user;

            var today = DateOnly.FromDateTime(LocalNow);
            var from = request.From ?? new DateOnly(today.Year, today.Month, 1);
            var to = request.To ?? today;
            if (key is ReportKeys.Sales or ReportKeys.Payments or ReportKeys.Products || (key == ReportKeys.Statement && request.From is not null && request.To is not null))
            {
                if (from > to) return ReportResult.Fail("La fecha inicial no puede ser posterior a la final.");
                if (to.DayNumber - from.DayNumber >= MaxDays) return ReportResult.Fail($"El período no puede pasar de {MaxDays} días.");
            }

            var generated = $"Generado el {LocalNow:dd/MM/yyyy HH:mm} por {user}";
            var period = $"Del {from:dd/MM/yyyy} al {to:dd/MM/yyyy}";
            var who = seller is null ? "todos los vendedores" : $"vendedor {seller}";

            switch (key)
            {
                case ReportKeys.Sales: return await SalesAsync(from, to, seller, request.IncludeVoided, $"{period} · {who} · {generated}", user);
                case ReportKeys.Receivables: return await ReceivablesAsync(today, seller, $"Al {today:dd/MM/yyyy} · {who} · {generated}", user);
                case ReportKeys.Payments: return await PaymentsAsync(from, to, seller, $"{period} · {who} · {generated}", user);
                case ReportKeys.Products: return await ProductsAsync(from, to, $"{period} · {generated}", user);
                case ReportKeys.Statement: return await StatementAsync(request.ClientId, request.From, request.To, seller, today, generated, user);
                case ReportKeys.Goals: return await GoalsAsync(request.Year, request.Month, generated, user);
                default: return await StockAsync(today, $"Al {today:dd/MM/yyyy} · {generated}", user);
            }
        }

        // ───────── Ventas ─────────

        private async Task<ReportResult> SalesAsync(DateOnly from, DateOnly to, string? seller, bool includeVoided, string subtitle, string user)
        {
            var (fromUtc, toUtc) = Range(from, to);
            var all = await _repository.GetSalesAsync(fromUtc, toUtc, seller, includeVoided);
            var sales = all.Where(s => s.Status != PaymentStatus.Voided).ToList();

            var detail = new ReportSheet
            {
                Name = "Ventas", Title = "Ventas", Subtitle = subtitle,
                Columns = new()
                {
                    new("Venta", ColumnKind.Text), new("Fecha", ColumnKind.Date), new("Cliente", ColumnKind.Text), new("Identificación", ColumnKind.Text),
                    new("Vendedor", ColumnKind.Text), new("IVA", ColumnKind.Text), new("Subtotal", ColumnKind.Money, Total: true),
                    new("Descuento", ColumnKind.Money, Total: true), new("IVA ₡", ColumnKind.Money, Total: true), new("Total", ColumnKind.Money, Total: true),
                    new("Cobrado", ColumnKind.Money, Total: true), new("Saldo", ColumnKind.Money, Total: true), new("Estado", ColumnKind.Text),
                    new("Vence", ColumnKind.Date), new("Comprobante", ColumnKind.Text)
                },
                Rows = sales.Select(s => new object?[]
                {
                    s.SaleNumber, Local(s.SaleDate).Date, s.Client, s.Identification, s.Seller, s.ApplyIva ? "Con IVA" : "Sin IVA",
                    s.Subtotal, s.Discount, s.Iva, s.Total, s.Paid, s.Balance, s.Status.Humanize(), s.DueDate, Voucher(s)
                }).ToList(),
                Notes = { "Subtotal y descuento sin IVA. Las ventas anuladas no se incluyen en esta hoja." }
            };

            var sheets = new List<ReportSheet> { detail };
            if (seller is null)
                sheets.Add(new ReportSheet
                {
                    Name = "Por vendedor", Title = "Ventas por vendedor", Subtitle = subtitle,
                    Columns = new()
                    {
                        new("Vendedor", ColumnKind.Text), new("Ventas", ColumnKind.Integer, Total: true), new("Subtotal", ColumnKind.Money, Total: true),
                        new("IVA ₡", ColumnKind.Money, Total: true), new("Total", ColumnKind.Money, Total: true), new("Cobrado", ColumnKind.Money, Total: true),
                        new("Saldo", ColumnKind.Money, Total: true)
                    },
                    Rows = sales.GroupBy(s => s.Seller).OrderByDescending(g => g.Sum(s => s.Total)).Select(g => new object?[]
                    {
                        g.Key, g.Count(), g.Sum(s => s.Subtotal), g.Sum(s => s.Iva), g.Sum(s => s.Total), g.Sum(s => s.Paid), g.Sum(s => s.Balance)
                    }).ToList()
                });
            sheets.Add(new ReportSheet
            {
                Name = "Por estado", Title = "Ventas por estado de pago", Subtitle = subtitle,
                Columns = new()
                {
                    new("Estado", ColumnKind.Text), new("Ventas", ColumnKind.Integer, Total: true), new("Total", ColumnKind.Money, Total: true),
                    new("Cobrado", ColumnKind.Money, Total: true), new("Saldo", ColumnKind.Money, Total: true)
                },
                Rows = sales.GroupBy(s => s.Status).OrderBy(g => g.Key).Select(g => new object?[]
                {
                    g.Key.Humanize(), g.Count(), g.Sum(s => s.Total), g.Sum(s => s.Paid), g.Sum(s => s.Balance)
                }).ToList()
            });

            if (includeVoided)
                sheets.Add(new ReportSheet
                {
                    Name = "Anuladas", Title = "Ventas anuladas", Subtitle = subtitle,
                    Columns = new()
                    {
                        new("Venta", ColumnKind.Text), new("Fecha", ColumnKind.Date), new("Cliente", ColumnKind.Text), new("Vendedor", ColumnKind.Text),
                        new("Total que tenía", ColumnKind.Money)
                    },
                    Rows = all.Where(s => s.Status == PaymentStatus.Voided).Select(s => new object?[] { s.SaleNumber, Local(s.SaleDate).Date, s.Client, s.Seller, s.Total }).ToList(),
                    Notes = { "Las anuladas devolvieron el stock y no suman en las otras hojas." }
                });

            return Done($"ventas_{from:yyyyMMdd}_{to:yyyyMMdd}", sheets, user);
        }

        // Qué comprobante respalda la venta: factura (con su estado) o tiquete de caja (sin IVA, no fiscal).
        private static string Voucher(SaleReportRow s)
        {
            if (!s.ApplyIva) return "Tiquete de caja (no fiscal)";
            if (s.InvoiceStatus is null) return "Sin facturar";
            var number = s.InvoiceNumber is null ? "Factura" : $"Factura {s.InvoiceNumber}";
            return s.InvoiceStatus == InvoiceStatus.Accepted ? number : $"{number} ({s.InvoiceStatus.Value.Humanize().ToLowerInvariant()})";
        }

        // ───────── Cuentas por cobrar ─────────

        private async Task<ReportResult> ReceivablesAsync(DateOnly today, string? seller, string subtitle, string user)
        {
            var rows = await _repository.GetReceivablesAsync(seller);
            var items = rows.Where(r => r.Balance > 0).Select(r => new
            {
                Row = r,
                Days = Math.Max(0, today.DayNumber - r.DueDate.DayNumber)
            }).ToList();

            string Bucket(int days) => days switch { 0 => "Al día", <= 30 => "1 a 30 días", <= 60 => "31 a 60 días", <= 90 => "61 a 90 días", _ => "Más de 90 días" };

            var detail = new ReportSheet
            {
                Name = "Cuentas por cobrar", Title = "Cuentas por cobrar", Subtitle = subtitle,
                Columns = new()
                {
                    new("Cliente", ColumnKind.Text), new("Venta", ColumnKind.Text), new("Fecha", ColumnKind.Date), new("Vence", ColumnKind.Date),
                    new("Días vencida", ColumnKind.Integer), new("Tramo", ColumnKind.Text), new("Total", ColumnKind.Money, Total: true),
                    new("Cobrado", ColumnKind.Money, Total: true), new("Saldo", ColumnKind.Money, Total: true), new("Vendedor", ColumnKind.Text)
                },
                Rows = items.OrderByDescending(i => i.Days).ThenBy(i => i.Row.Client).Select(i => new object?[]
                {
                    i.Row.Client, i.Row.SaleNumber, Local(i.Row.SaleDate).Date, i.Row.DueDate, i.Days, Bucket(i.Days),
                    i.Row.Total, i.Row.Paid, i.Row.Balance, i.Row.Seller
                }).ToList(),
                Notes = { "Saldo = total de la venta menos los abonos vigentes. Días vencida = hoy menos la fecha de vencimiento (0 si todavía no vence)." }
            };

            var byClient = new ReportSheet
            {
                Name = "Por cliente", Title = "Saldo por cliente y antigüedad", Subtitle = subtitle,
                Columns = new()
                {
                    new("Cliente", ColumnKind.Text), new("Ventas", ColumnKind.Integer, Total: true), new("Saldo", ColumnKind.Money, Total: true),
                    new("Al día", ColumnKind.Money, Total: true), new("1 a 30 días", ColumnKind.Money, Total: true), new("31 a 60 días", ColumnKind.Money, Total: true),
                    new("61 a 90 días", ColumnKind.Money, Total: true), new("Más de 90 días", ColumnKind.Money, Total: true)
                },
                Rows = items.GroupBy(i => i.Row.Client).OrderByDescending(g => g.Sum(i => i.Row.Balance)).Select(g => new object?[]
                {
                    g.Key, g.Count(), g.Sum(i => i.Row.Balance),
                    g.Where(i => Bucket(i.Days) == "Al día").Sum(i => i.Row.Balance), g.Where(i => Bucket(i.Days) == "1 a 30 días").Sum(i => i.Row.Balance),
                    g.Where(i => Bucket(i.Days) == "31 a 60 días").Sum(i => i.Row.Balance), g.Where(i => Bucket(i.Days) == "61 a 90 días").Sum(i => i.Row.Balance),
                    g.Where(i => Bucket(i.Days) == "Más de 90 días").Sum(i => i.Row.Balance)
                }).ToList()
            };

            return Done($"cuentas_por_cobrar_{today:yyyyMMdd}", new[] { detail, byClient }, user);
        }

        // ───────── Cobros ─────────

        private async Task<ReportResult> PaymentsAsync(DateOnly from, DateOnly to, string? seller, string subtitle, string user)
        {
            var (fromUtc, toUtc) = Range(from, to);
            var payments = await _repository.GetPaymentsAsync(fromUtc, toUtc, seller);

            var detail = new ReportSheet
            {
                Name = "Cobros", Title = "Cobros (abonos recibidos)", Subtitle = subtitle,
                Columns = new()
                {
                    new("Fecha de pago", ColumnKind.Date), new("Venta", ColumnKind.Text), new("Cliente", ColumnKind.Text), new("Vendedor", ColumnKind.Text),
                    new("Método", ColumnKind.Text), new("Monto", ColumnKind.Money, Total: true), new("Referencia", ColumnKind.Text), new("Registrado por", ColumnKind.Text)
                },
                Rows = payments.Select(p => new object?[]
                {
                    Local(p.PaymentDate).Date, p.SaleNumber, p.Client, p.Seller, p.Method.Humanize(), p.Amount, p.Reference ?? string.Empty, p.CreatedBy
                }).ToList(),
                Notes = { "Montos con IVA, tal como los pagó el cliente. No incluye abonos anulados ni de ventas anuladas." }
            };
            var byMethod = new ReportSheet
            {
                Name = "Por método", Title = "Cobros por método de pago", Subtitle = subtitle,
                Columns = new() { new("Método", ColumnKind.Text), new("Abonos", ColumnKind.Integer, Total: true), new("Monto", ColumnKind.Money, Total: true) },
                Rows = payments.GroupBy(p => p.Method).OrderByDescending(g => g.Sum(p => p.Amount))
                    .Select(g => new object?[] { g.Key.Humanize(), g.Count(), g.Sum(p => p.Amount) }).ToList()
            };
            return Done($"cobros_{from:yyyyMMdd}_{to:yyyyMMdd}", new[] { detail, byMethod }, user);
        }

        // ───────── Estado de cuenta del cliente ─────────

        // Libro del cliente: cada venta es un cargo y cada abono vigente un pago, en orden de fecha, con el saldo corrido.
        // Con período, arranca con una fila de saldo inicial (todo lo anterior a "desde"). El vendedor solo ve lo de sus ventas.
        private async Task<ReportResult> StatementAsync(int? clientId, DateOnly? from, DateOnly? to, string? seller, DateOnly today, string generated, string user)
        {
            if (clientId is null or <= 0) return ReportResult.Fail("Elegí un cliente.");
            if ((from is null) != (to is null)) return ReportResult.Fail("Poné las dos fechas del período o ninguna.");
            if (from > to) return ReportResult.Fail("La fecha inicial no puede ser posterior a la final.");

            var client = await _repository.GetClientAsync(clientId.Value);
            if (client is null) return ReportResult.Fail("El cliente no existe.");

            var sales = await _repository.GetClientSalesAsync(client.Id, seller);
            var payments = await _repository.GetClientPaymentsAsync(client.Id, seller);

            // Cargo antes que abono dentro del mismo día; así una venta de contado muestra venta y luego pago.
            var events = sales.Select(s => (Date: Local(s.SaleDate).Date, Order: 0, Type: "Venta", Doc: s.SaleNumber,
                    Detail: s.ApplyIva ? "Con IVA" : "Sin IVA", Due: (DateOnly?)s.DueDate, Charge: s.Total, Credit: 0m))
                .Concat(payments.Select(p => (Date: Local(p.PaymentDate).Date, Order: 1, Type: "Abono", Doc: p.SaleNumber,
                    Detail: string.IsNullOrWhiteSpace(p.Reference) ? p.Method.Humanize() : $"{p.Method.Humanize()} · {p.Reference}",
                    Due: (DateOnly?)null, Charge: 0m, Credit: p.Amount)))
                .OrderBy(e => e.Date).ThenBy(e => e.Order).ThenBy(e => e.Doc).ToList();

            var start = from?.ToDateTime(TimeOnly.MinValue);
            var end = to?.ToDateTime(TimeOnly.MinValue);
            var opening = start is null ? 0m : events.Where(e => e.Date < start).Sum(e => e.Charge - e.Credit);
            var inPeriod = events.Where(e => (start is null || e.Date >= start) && (end is null || e.Date <= end)).ToList();

            var rows = new List<object?[]>();
            var running = opening;
            if (start is not null) rows.Add(new object?[] { start.Value, "Saldo inicial", string.Empty, "Saldo anterior al período", null, null, null, running });
            foreach (var e in inPeriod)
            {
                running += e.Charge - e.Credit;
                rows.Add(new object?[] { e.Date, e.Type, e.Doc, e.Detail, e.Due, e.Charge == 0 ? null : e.Charge, e.Credit == 0 ? null : e.Credit, running });
            }

            var period = from is null ? "Todo el historial" : $"Del {from:dd/MM/yyyy} al {to:dd/MM/yyyy}";
            var ident = string.IsNullOrWhiteSpace(client.Identification) ? string.Empty : $" · {client.Identification}";
            var statement = new ReportSheet
            {
                Name = "Estado de cuenta", Title = $"Estado de cuenta · {client.Name}", Subtitle = $"{client.Name}{ident} · {period} · {generated}",
                Columns = new()
                {
                    new("Fecha", ColumnKind.Date), new("Movimiento", ColumnKind.Text), new("Venta", ColumnKind.Text), new("Detalle", ColumnKind.Text),
                    new("Vence", ColumnKind.Date), new("Cargos", ColumnKind.Money, Total: true), new("Abonos", ColumnKind.Money, Total: true), new("Saldo", ColumnKind.Money)
                },
                Rows = rows,
                Notes =
                {
                    "Cargo = total de la venta con IVA. Abono = pago vigente (los anulados no aparecen). Saldo = saldo corrido; la última fila es lo que el cliente debe al cierre.",
                    "No incluye ventas anuladas." + (seller is null ? string.Empty : " Solo incluye las ventas de este vendedor.")
                }
            };

            var open = sales.Where(s => s.Balance > 0).OrderBy(s => s.DueDate).ThenBy(s => s.SaleDate).ToList();
            var pending = new ReportSheet
            {
                Name = "Pendiente", Title = $"Ventas con saldo · {client.Name}", Subtitle = $"Al {today:dd/MM/yyyy} · {generated}",
                Columns = new()
                {
                    new("Venta", ColumnKind.Text), new("Fecha", ColumnKind.Date), new("Vence", ColumnKind.Date), new("Días vencida", ColumnKind.Integer),
                    new("Total", ColumnKind.Money, Total: true), new("Cobrado", ColumnKind.Money, Total: true), new("Saldo", ColumnKind.Money, Total: true)
                },
                Rows = open.Select(s => new object?[]
                {
                    s.SaleNumber, Local(s.SaleDate).Date, s.DueDate, Math.Max(0, today.DayNumber - s.DueDate.DayNumber), s.Total, s.Paid, s.Balance
                }).ToList(),
                Notes = { "Siempre al día de hoy y sobre todo el historial, sin importar el período del estado de cuenta." }
            };

            var safeName = new string(client.Name.Where(char.IsLetterOrDigit).Take(24).ToArray());
            return Done($"estado_cuenta_{(safeName.Length == 0 ? client.Id.ToString() : safeName)}_{today:yyyyMMdd}", new[] { statement, pending }, user);
        }

        // ───────── Ventas por producto (solo administradores: trae costos) ─────────

        private async Task<ReportResult> ProductsAsync(DateOnly from, DateOnly to, string subtitle, string user)
        {
            var (fromUtc, toUtc) = Range(from, to);
            var products = await _repository.GetProductSalesAsync(fromUtc, toUtc);

            var detail = new ReportSheet
            {
                Name = "Por producto", Title = "Ventas por producto", Subtitle = subtitle,
                Columns = new()
                {
                    new("Código", ColumnKind.Text), new("Producto", ColumnKind.Text), new("Categoría", ColumnKind.Text), new("Cantidad", ColumnKind.Integer, Total: true),
                    new("Vendido sin IVA", ColumnKind.Money, Total: true), new("Descuento", ColumnKind.Money, Total: true), new("Costo", ColumnKind.Money, Total: true),
                    new("Utilidad", ColumnKind.Money, Total: true), new("Margen", ColumnKind.Percent)
                },
                Rows = products.Select(p => new object?[]
                {
                    p.Code ?? string.Empty, p.Name, p.Category.Humanize(), p.Quantity, p.Sold, p.Discount, p.Cost,
                    p.Cost is null ? null : p.Sold - p.Cost, p.Cost is null || p.Sold == 0 ? null : (p.Sold - p.Cost) / p.Sold
                }).ToList(),
                Notes =
                {
                    "Costo = costo del producto al completar cada orden (histórico), no el costo actual.",
                    "Costo, utilidad y margen quedan vacíos si alguna venta del producto no tiene costo registrado; los totales de costo y utilidad suman solo los productos con costo.",
                    "Las ventas anuladas no se incluyen."
                }
            };
            var byCategory = new ReportSheet
            {
                Name = "Por categoría", Title = "Ventas por categoría", Subtitle = subtitle,
                Columns = new() { new("Categoría", ColumnKind.Text), new("Cantidad", ColumnKind.Integer, Total: true), new("Vendido sin IVA", ColumnKind.Money, Total: true), new("Descuento", ColumnKind.Money, Total: true) },
                Rows = products.GroupBy(p => p.Category).OrderByDescending(g => g.Sum(p => p.Sold))
                    .Select(g => new object?[] { g.Key.Humanize(), g.Sum(p => p.Quantity), g.Sum(p => p.Sold), g.Sum(p => p.Discount) }).ToList()
            };
            return Done($"ventas_por_producto_{from:yyyyMMdd}_{to:yyyyMMdd}", new[] { detail, byCategory }, user);
        }

        // ───────── Metas y desempeño ─────────

        private async Task<ReportResult> GoalsAsync(int? year, int? month, string generated, string user)
        {
            if (GoalServices.PeriodError(year, month) is { } error) return ReportResult.Fail(error);
            var team = await _goalServices.GetTeamProgressAsync(year, month);
            var p = team.Period;
            var monthLabel = new DateTime(p.Year, p.Month, 1).ToString("MMMM yyyy", Es);

            // Porcentaje como fracción (0.25 = 25 %); sin meta queda vacío.
            static object? Pct(MetricProgressDTO m) => m.Target > 0 ? m.Actual / m.Target : null;
            static object? Goal(MetricProgressDTO m) => m.Target > 0 ? m.Target : null;

            var sheet = new ReportSheet
            {
                Name = "Metas", Title = $"Metas y desempeño · {monthLabel}", Subtitle = $"{generated}",
                Columns = new()
                {
                    new("Vendedor", ColumnKind.Text), new("Correo", ColumnKind.Text),
                    new("Vendido sin IVA", ColumnKind.Money, Total: true), new("Meta vendido", ColumnKind.Money, Total: true), new("% vendido", ColumnKind.Percent),
                    new("Cobrado sin IVA", ColumnKind.Money, Total: true), new("Meta cobrado", ColumnKind.Money, Total: true), new("% cobrado", ColumnKind.Percent),
                    new("Ventas", ColumnKind.Integer, Total: true), new("Meta ventas", ColumnKind.Integer, Total: true), new("% ventas", ColumnKind.Percent)
                },
                Rows = team.Sellers.Select(s => new object?[]
                {
                    s.DisplayName(), s.Seller, s.Sold.Actual, Goal(s.Sold), Pct(s.Sold), s.Collected.Actual, Goal(s.Collected), Pct(s.Collected),
                    (int)s.SalesCount.Actual, s.SalesCount.Target > 0 ? (int)s.SalesCount.Target : null, Pct(s.SalesCount)
                }).ToList(),
                Notes =
                {
                    "Vendido: por fecha de la venta. Cobrado: abonos del mes sin IVA, por fecha del abono. Sin ventas anuladas.",
                    "El % puede pasar de 100. Sin meta en esa métrica queda vacío."
                }
            };
            return Done($"metas_{p.Year}-{p.Month:D2}", new[] { sheet }, user);
        }

        // ───────── Inventario valorizado (solo administradores) ─────────

        private async Task<ReportResult> StockAsync(DateOnly today, string subtitle, string user)
        {
            var stock = await _repository.GetStockAsync();
            var sheet = new ReportSheet
            {
                Name = "Inventario", Title = "Inventario valorizado", Subtitle = subtitle,
                Columns = new()
                {
                    new("Código", ColumnKind.Text), new("Producto", ColumnKind.Text), new("Categoría", ColumnKind.Text), new("Estado", ColumnKind.Text),
                    new("Existencias", ColumnKind.Integer, Total: true), new("Reservado", ColumnKind.Integer, Total: true), new("Disponible", ColumnKind.Integer, Total: true),
                    new("Precio sin IVA", ColumnKind.Money), new("Costo", ColumnKind.Money),
                    new("Valor al costo", ColumnKind.Money, Total: true), new("Valor a precio", ColumnKind.Money, Total: true)
                },
                Rows = stock.Select(s => new object?[]
                {
                    s.Code ?? string.Empty, s.Name, s.Category.Humanize(), s.Status == StatusKind.Active ? "Activo" : "Inactivo",
                    s.Quantity, s.OnHold, s.Available, s.Price, s.Cost,
                    s.Cost is null ? null : s.Cost * s.Quantity, s.Price is null ? null : s.Price * s.Quantity
                }).ToList(),
                Notes =
                {
                    "Existencias = todo lo que hay en bodega; Reservado = apartado por órdenes abiertas; Disponible = existencias menos reservado.",
                    "Valor al costo y a precio usan las existencias. Los productos sin costo o sin precio no suman en esa columna."
                }
            };
            return Done($"inventario_{today:yyyyMMdd}", new[] { sheet }, user);
        }

        // ───────── Ayudas ─────────

        private DateTime LocalNow => TimeZoneInfo.ConvertTime(_time.GetUtcNow(), _time.LocalTimeZone).DateTime;

        private DateTime Local(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _time.LocalTimeZone);

        // [medianoche local de "desde", medianoche local del día siguiente a "hasta") en UTC: SaleDate y PaymentDate se guardan en UTC.
        private (DateTime FromUtc, DateTime ToUtc) Range(DateOnly from, DateOnly to)
        {
            var zone = _time.LocalTimeZone;
            var start = DateTime.SpecifyKind(from.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
            var end = DateTime.SpecifyKind(to.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
            return (TimeZoneInfo.ConvertTimeToUtc(start, zone), TimeZoneInfo.ConvertTimeToUtc(end, zone));
        }

        private static ReportResult Done(string name, IReadOnlyList<ReportSheet> sheets, string user) =>
            new(new ReportFile(name + ".xlsx", ExcelWorkbookBuilder.Build(sheets, user)), null);
    }

    internal static class SellerProgressExtensions
    {
        public static string DisplayName(this SellerProgressDTO s) => string.IsNullOrWhiteSpace(s.FullName) ? s.Seller : s.FullName;
    }
}
