namespace CatalogStore.UI.Models.Client
{
    public class HaciendaLookupResult
    {
        public string? Nombre { get; set; }
        public List<HaciendaActividadResult>? Actividades { get; set; }
    }

    public class HaciendaActividadResult
    {
        public string Codigo { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
        public string Tipo { get; set; } = string.Empty;
    }
}
