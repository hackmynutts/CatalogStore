using CatalogStore.UI.Models.Sales;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CatalogStore.UI.Documents
{
    // Comprobante de una venta SIN IVA, con el mismo formato que la factura y la proforma. Es un documento NO fiscal:
    // no va a Hacienda ni sustituye a una factura electrónica.
    public class SaleReceiptPdfDocument : IDocument
    {
        private const string Brand = "#2F5D50";
        private const string Gold = "#C8A24A";
        private const string Muted = "#6B7280";
        private const string Line = "#E5E7EB";

        private readonly SaleViewModel _sale;
        private readonly DateTime _saleLocal;
        private readonly string _contact;
        private readonly byte[]? _logo;

        public SaleReceiptPdfDocument(SaleViewModel sale, DateTime saleLocal, string contact, byte[]? logo)
        {
            _sale = sale;
            _saleLocal = saleLocal;
            _contact = contact;
            _logo = logo;
        }

        public static string Money(decimal v) => "₡" + v.ToString("N2", System.Globalization.CultureInfo.InvariantCulture);

        public DocumentMetadata GetMetadata() => new() { Title = $"Comprobante de venta {_sale.SaleNumber}", Author = "SAM DESIGN" };

        public void Compose(IDocumentContainer container) => container.Page(page =>
        {
            page.Size(PageSizes.Letter);
            page.Margin(36);
            page.DefaultTextStyle(t => t.FontSize(10).FontColor("#1F2937"));
            page.Header().Element(Header);
            page.Content().PaddingTop(14).Element(Content);
            page.Footer().AlignCenter().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(8).FontColor(Muted));
                t.Span($"Comprobante de venta {_sale.SaleNumber} · Página ");
                t.CurrentPageNumber();
                t.Span(" de ");
                t.TotalPages();
            });
        });

        private void Header(IContainer container) => container.BorderBottom(2.5f).BorderColor(Gold).PaddingBottom(12).Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                // El logo completo ya trae el nombre y el lema.
                if (_logo is not null) col.Item().Height(58).Image(_logo).FitHeight();
                else col.Item().Text("SAM DESIGN").FontSize(13).Bold().FontColor("#24473D");
                if (_contact.Length > 0) col.Item().Text(_contact).FontSize(8.5f).FontColor(Muted);
            });
            row.ConstantItem(210).AlignRight().Column(col =>
            {
                col.Item().AlignRight().Text("COMPROBANTE DE VENTA").FontSize(15).Bold().FontColor(Brand);
                col.Item().AlignRight().Text($"N.º {_sale.SaleNumber}").FontSize(12).Bold();
                col.Item().AlignRight().Text($"Fecha: {_saleLocal:dd/MM/yyyy HH:mm}").FontSize(9).FontColor("#4B5563");
            });
        });

        private void Content(IContainer container) => container.Column(col =>
        {
            col.Spacing(14);

            col.Item().Row(row =>
            {
                row.RelativeItem().Border(0.7f).BorderColor(Line).Padding(9).Column(c =>
                {
                    c.Item().Text("CLIENTE").FontSize(7.5f).Bold().FontColor(Muted).LetterSpacing(0.08f);
                    c.Item().PaddingTop(3).Text(t =>
                    {
                        t.Span(_sale.ClientName).Bold();
                        if (!string.IsNullOrWhiteSpace(_sale.ClientCC)) t.Span($" ({_sale.ClientCC})");
                    });
                    c.Item().Text($"Identificación: {_sale.ClientIdentification}");
                    if (!string.IsNullOrWhiteSpace(_sale.ClientPhone)) c.Item().Text($"Tel. {_sale.ClientPhone}");
                    if (!string.IsNullOrWhiteSpace(_sale.ClientEmail)) c.Item().Text(_sale.ClientEmail);
                    if (!string.IsNullOrWhiteSpace(_sale.ClientAddress)) c.Item().Text(_sale.ClientAddress);
                });
                row.ConstantItem(14);
                row.RelativeItem().Border(0.7f).BorderColor(Line).Padding(9).Column(c =>
                {
                    c.Item().Text("DETALLE").FontSize(7.5f).Bold().FontColor(Muted).LetterSpacing(0.08f);
                    c.Item().PaddingTop(3).Text($"Venta: {_sale.SaleNumber}");
                    c.Item().Text($"Vendedor: {_sale.Seller}");
                    c.Item().Text($"Condición: {_sale.PaymentTermName}");
                    c.Item().Text($"Vence: {_sale.DueDate:dd/MM/yyyy}");
                    c.Item().Text("Moneda: colones (₡)");
                });
            });

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(5);
                    c.RelativeColumn(1);
                    c.RelativeColumn(2.2f);
                    c.RelativeColumn(1.3f);
                    c.RelativeColumn(2.4f);
                });

                static IContainer HeaderCell(IContainer c) => c.Background("#F3F4F6").PaddingVertical(5).PaddingHorizontal(6);
                table.Header(h =>
                {
                    h.Cell().Element(HeaderCell).Text("PRODUCTO").FontSize(7.5f).Bold().FontColor("#4B5563");
                    h.Cell().Element(HeaderCell).AlignRight().Text("CANT.").FontSize(7.5f).Bold().FontColor("#4B5563");
                    h.Cell().Element(HeaderCell).AlignRight().Text("PRECIO").FontSize(7.5f).Bold().FontColor("#4B5563");
                    h.Cell().Element(HeaderCell).AlignRight().Text("DESC.").FontSize(7.5f).Bold().FontColor("#4B5563");
                    h.Cell().Element(HeaderCell).AlignRight().Text("TOTAL").FontSize(7.5f).Bold().FontColor("#4B5563");
                });

                static IContainer BodyCell(IContainer c) => c.ShowEntire().BorderBottom(0.6f).BorderColor(Line).PaddingVertical(6).PaddingHorizontal(6);
                foreach (var l in _sale.Lines)
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

            col.Item().ShowEntire().AlignRight().Width(230).Column(c =>
            {
                if (_sale.DiscountTotal > 0)
                {
                    c.Item().Row(r => { r.RelativeItem().Text("Subtotal"); r.AutoItem().Text(Money(_sale.Subtotal + _sale.DiscountTotal)); });
                    c.Item().PaddingTop(3).Row(r => { r.RelativeItem().Text("Descuento"); r.AutoItem().Text("− " + Money(_sale.DiscountTotal)); });
                }
                c.Item().PaddingTop(6).BorderTop(1.5f).BorderColor("#1F2937").PaddingTop(5)
                    .Row(r => { r.RelativeItem().Text("Total").FontSize(13).Bold(); r.AutoItem().Text(Money(_sale.TotalAmount)).FontSize(13).Bold(); });
            });

            if (!string.IsNullOrWhiteSpace(_sale.Notes))
                col.Item().Text(t => { t.Span("Notas: ").Bold(); t.Span(_sale.Notes); });

            col.Item().ShowEntire().Border(0.7f).BorderColor(Line).Padding(9)
                .Text(SaleDocumentTexts.ReceivedInFull).FontSize(9).FontColor("#1F2937");

            col.Item().ShowEntire().Text("Venta sin IVA. Este comprobante es un documento no fiscal: no es una factura electrónica ni la sustituye.")
                .FontSize(8.5f).FontColor(Muted);
        });
    }
}
