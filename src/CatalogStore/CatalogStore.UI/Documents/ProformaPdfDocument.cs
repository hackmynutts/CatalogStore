using CatalogStore.UI.Models.Client;
using CatalogStore.UI.Models.Order;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CatalogStore.UI.Documents
{
    // La proforma como archivo PDF (QuestPDF). Mismo contenido que la página imprimible Order/Proforma: la cotización
    // para el cliente, no fiscal, con los precios vigentes de la orden.
    public class ProformaPdfDocument : IDocument
    {
        private const string Brand = "#2F5D50";
        private const string Gold = "#C8A24A";
        private const string Muted = "#6B7280";
        private const string Line = "#E5E7EB";

        private readonly OrderViewModel _order;
        private readonly ClientViewModel? _client;
        private readonly DateTime _issued;
        private readonly string _contact;
        private readonly byte[]? _logo;

        public ProformaPdfDocument(OrderViewModel order, ClientViewModel? client, DateTime issued, string contact, byte[]? logo)
        {
            _order = order;
            _client = client;
            _issued = issued;
            _contact = contact;
            _logo = logo;
        }

        public static string Money(decimal v) => "₡" + v.ToString("N2", System.Globalization.CultureInfo.InvariantCulture);

        public DocumentMetadata GetMetadata() => new()
        {
            Title = $"Proforma {OrderDocs.ProformaNumber(_order.OrderNumber)}",
            Author = "SAM DESIGN"
        };

        public void Compose(IDocumentContainer container) => container.Page(page =>
        {
            page.Size(PageSizes.Letter);
            page.Margin(36);
            page.DefaultTextStyle(t => t.FontSize(10).FontColor("#1F2937"));

            page.Header().Element(Header);
            page.Content().PaddingTop(14).Element(Content);
            page.Footer().Element(Footer);
        });

        private void Header(IContainer container) => container.BorderBottom(2.5f).BorderColor(Gold).PaddingBottom(12).Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                if (_logo is not null) col.Item().Height(44).Image(_logo).FitHeight();
                col.Item().PaddingTop(4).Text("SAM DESIGN").FontSize(13).Bold().FontColor("#24473D");
                if (_contact.Length > 0) col.Item().Text(_contact).FontSize(8.5f).FontColor(Muted);
            });
            row.ConstantItem(200).AlignRight().Column(col =>
            {
                col.Item().AlignRight().Text("PROFORMA").FontSize(22).Bold().FontColor(Brand).LetterSpacing(0.08f);
                col.Item().AlignRight().Text(OrderDocs.ProformaNumber(_order.OrderNumber)).FontSize(12).Bold();
                col.Item().AlignRight().Text($"Fecha: {_issued:dd/MM/yyyy}").FontSize(9).FontColor("#4B5563");
                col.Item().AlignRight().Text($"Válida hasta: {_issued.Date.AddDays(OrderDocs.ProformaValidDays):dd/MM/yyyy}").FontSize(9).FontColor("#4B5563");
            });
        });

        private void Content(IContainer container) => container.Column(col =>
        {
            col.Spacing(14);

            // Cliente y detalle
            col.Item().Row(row =>
            {
                row.RelativeItem().Border(0.7f).BorderColor(Line).Padding(9).Column(c =>
                {
                    c.Item().Text("CLIENTE").FontSize(7.5f).Bold().FontColor(Muted).LetterSpacing(0.08f);
                    c.Item().PaddingTop(3).Text(t =>
                    {
                        t.Span(_order.ClientName).Bold();
                        if (!string.IsNullOrWhiteSpace(_order.ClientCC)) t.Span($" ({_order.ClientCC})");
                    });
                    c.Item().Text($"Identificación: {_order.ClientIdentification}");
                    if (!string.IsNullOrWhiteSpace(_client?.ClientPhone)) c.Item().Text($"Tel. {_client!.ClientPhone}");
                    if (!string.IsNullOrWhiteSpace(_client?.ClientEmail)) c.Item().Text(_client!.ClientEmail);
                    if (!string.IsNullOrWhiteSpace(_client?.ClientAddress)) c.Item().Text(_client!.ClientAddress);
                });
                row.ConstantItem(14);
                row.RelativeItem().Border(0.7f).BorderColor(Line).Padding(9).Column(c =>
                {
                    c.Item().Text("DETALLE").FontSize(7.5f).Bold().FontColor(Muted).LetterSpacing(0.08f);
                    c.Item().PaddingTop(3).Text($"Orden: {_order.OrderNumber}");
                    c.Item().Text($"Vendedor: {_order.CreatedBy}");
                    c.Item().Text("Moneda: colones (₡)");
                    c.Item().Text(_order.ApplyIva ? "Precios más IVA (13 %)" : "Venta sin IVA");
                });
            });

            // Productos
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(5);
                    c.RelativeColumn(1);
                    c.RelativeColumn(2);
                    c.RelativeColumn(1.2f);
                    c.RelativeColumn(2.2f);
                });

                static IContainer HeaderCell(IContainer c) => c.Background("#F3F4F6").PaddingVertical(5).PaddingHorizontal(6);
                table.Header(h =>
                {
                    h.Cell().Element(HeaderCell).Text("PRODUCTO").FontSize(7.5f).Bold().FontColor("#4B5563");
                    h.Cell().Element(HeaderCell).AlignRight().Text("CANT.").FontSize(7.5f).Bold().FontColor("#4B5563");
                    h.Cell().Element(HeaderCell).AlignRight().Text("PRECIO").FontSize(7.5f).Bold().FontColor("#4B5563");
                    h.Cell().Element(HeaderCell).AlignRight().Text("DESC.").FontSize(7.5f).Bold().FontColor("#4B5563");
                    h.Cell().Element(HeaderCell).AlignRight().Text("SUBTOTAL").FontSize(7.5f).Bold().FontColor("#4B5563");
                });

                // ShowEntire: una fila no se parte entre páginas (si no, el código del producto quedaba solo arriba de la página siguiente).
                static IContainer BodyCell(IContainer c) => c.ShowEntire().BorderBottom(0.6f).BorderColor(Line).PaddingVertical(6).PaddingHorizontal(6);
                foreach (var l in _order.Lines)
                {
                    table.Cell().Element(BodyCell).Column(c =>
                    {
                        c.Item().Text(l.ProductName);
                        if (!string.IsNullOrWhiteSpace(l.ProductCode)) c.Item().Text(l.ProductCode).FontSize(8).FontColor(Muted);
                    });
                    table.Cell().Element(BodyCell).AlignRight().Text(l.Quantity.ToString());
                    table.Cell().Element(BodyCell).AlignRight().Text(Money(l.UnitPrice));
                    table.Cell().Element(BodyCell).AlignRight().Text(l.Discount > 0 ? l.Discount.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " %" : "—");
                    table.Cell().Element(BodyCell).AlignRight().Text(Money(l.LineSubtotal));
                }
            });

            // Totales (no se parten entre páginas)
            var discount = _order.Lines.Sum(l => l.UnitPrice * l.Quantity) - _order.Subtotal;
            col.Item().ShowEntire().AlignRight().Width(230).Column(c =>
            {
                c.Item().Row(r => { r.RelativeItem().Text("Subtotal"); r.AutoItem().Text(Money(_order.Subtotal + discount)); });
                if (discount > 0)
                    c.Item().PaddingTop(3).Row(r => { r.RelativeItem().Text("Descuento"); r.AutoItem().Text("− " + Money(discount)); });
                c.Item().PaddingTop(3).Row(r => { r.RelativeItem().Text(_order.ApplyIva ? "IVA" : "IVA (no aplica)"); r.AutoItem().Text(Money(_order.IvaTotal)); });
                c.Item().PaddingTop(6).BorderTop(1.5f).BorderColor("#1F2937").PaddingTop(5)
                    .Row(r => { r.RelativeItem().Text("Total").FontSize(13).Bold(); r.AutoItem().Text(Money(_order.OrderTotalAmount)).FontSize(13).Bold(); });
            });

            if (!string.IsNullOrWhiteSpace(_order.Notes))
                col.Item().Text(t => { t.Span("Notas: ").Bold(); t.Span(_order.Notes); });

            col.Item().ShowEntire().BorderTop(0.7f).BorderColor(Line).PaddingTop(8)
                .Text($"Esta proforma es una cotización: los precios son válidos por {OrderDocs.ProformaValidDays} días desde su fecha y no reserva mercadería. No es un comprobante fiscal ni una factura.")
                .FontSize(8.5f).FontColor(Muted);
        });

        private void Footer(IContainer container) => container.AlignCenter().Text(t =>
        {
            t.DefaultTextStyle(s => s.FontSize(8).FontColor(Muted));
            t.Span($"{OrderDocs.ProformaNumber(_order.OrderNumber)} · Página ");
            t.CurrentPageNumber();
            t.Span(" de ");
            t.TotalPages();
        });
    }
}
