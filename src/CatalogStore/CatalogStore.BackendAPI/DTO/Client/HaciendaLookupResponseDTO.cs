namespace CatalogStore.BackendAPI.DTO.Client
{
    public class HaciendaLookupResponseDTO
    {
        public string Nombre { get; set; } = string.Empty;
        public List<HaciendaActividadDTO>? Actividades { get; set; } = new();
    }
    public class HaciendaActividadDTO
    {
        public string Codigo { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
        public string Tipo { get; set; } = string.Empty;
    }
}
