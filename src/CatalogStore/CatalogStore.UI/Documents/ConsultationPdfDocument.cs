using CatalogStore.UI.Models.Home;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CatalogStore.UI.Documents
{
    // La lista de consulta que arma un visitante desde la landing: solo productos, sin precios ni cantidades.
    // No es una cotización, no reserva nada y no garantiza disponibilidad: el cliente la consulta con un vendedor.
    public class ConsultationPdfDocument : IDocument
    {
        private const string Brand = "#2F5D50";
        private const string Gold = "#C8A24A";
        private const string Muted = "#6B7280";
        private const string Line = "#E5E7EB";

        private readonly IReadOnlyList<PublicCatalogItem> _lines;
        private readonly string? _customer;
        private readonly DateTime _issued;
        private readonly string _contact;
        private readonly byte[]? _logo;

        public ConsultationPdfDocument(IReadOnlyList<PublicCatalogItem> lines, string? customer, DateTime issued, string contact, byte[]? logo)
        {
            _lines = lines;
            _customer = customer;
            _issued = issued;
            _contact = contact;
            _logo = logo;
        }

        public DocumentMetadata GetMetadata() => new() { Title = "Lista de consulta", Author = "SAM DESIGN" };

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
                t.Span("Lista de consulta · Página ");
                t.CurrentPageNumber();
                t.Span(" de ");
                t.TotalPages();
            });
        });

        private void Header(IContainer container) => container.BorderBottom(2.5f).BorderColor(Gold).PaddingBottom(12).Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                if (_logo is not null) col.Item().Height(44).Image(_logo).FitHeight();
                col.Item().PaddingTop(4).Text("SAM DESIGN").FontSize(13).Bold().FontColor("#24473D");
                if (_contact.Length > 0) col.Item().Text(_contact).FontSize(8.5f).FontColor(Muted);
            });
            row.ConstantItem(210).AlignRight().Column(col =>
            {
                col.Item().AlignRight().Text("LISTA DE CONSULTA").FontSize(17).Bold().FontColor(Brand);
                col.Item().AlignRight().Text($"Fecha: {_issued:dd/MM/yyyy}").FontSize(9).FontColor("#4B5563");
                if (!string.IsNullOrWhiteSpace(_customer)) col.Item().AlignRight().Text($"Para: {_customer}").FontSize(9).FontColor("#4B5563");
            });
        });

        private void Content(IContainer container) => container.Column(col =>
        {
            col.Spacing(14);
            col.Item().Text("Estos son los productos que me interesan. ¿Me pueden confirmar disponibilidad y precio?");

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(6);
                    c.RelativeColumn(2.4f);
                });

                static IContainer HeaderCell(IContainer c) => c.Background("#F3F4F6").PaddingVertical(5).PaddingHorizontal(6);
                table.Header(h =>
                {
                    h.Cell().Element(HeaderCell).Text("PRODUCTO").FontSize(7.5f).Bold().FontColor("#4B5563");
                    h.Cell().Element(HeaderCell).Text("CÓDIGO").FontSize(7.5f).Bold().FontColor("#4B5563");
                });

                static IContainer BodyCell(IContainer c) => c.ShowEntire().BorderBottom(0.6f).BorderColor(Line).PaddingVertical(6).PaddingHorizontal(6);
                foreach (var item in _lines)
                {
                    table.Cell().Element(BodyCell).Text(item.Name);
                    table.Cell().Element(BodyCell).Text(string.IsNullOrWhiteSpace(item.Code) ? "—" : item.Code).FontColor(Muted);
                }
            });

            col.Item().AlignRight().Text(t =>
            {
                t.Span($"{_lines.Count} producto{(_lines.Count == 1 ? "" : "s")}").Bold();
            });

            col.Item().ShowEntire().BorderTop(0.7f).BorderColor(Line).PaddingTop(8)
                .Text("Esta lista es meramente de consulta: no es una cotización, no reserva mercadería y no garantiza disponibilidad. Debe consultarla con un vendedor, que le confirmará disponibilidad y precio.")
                .FontSize(9).FontColor(Muted);
        });
    }
}
