namespace CatalogStore.BackendAPI.DTO.CatalogExternal
{
    /// <summary>
    /// Resumen de una importación del catálogo externo. Los mensajes de las listas están limitados;
    /// WarningCount y ErrorCount llevan el total real.
    /// </summary>
    public class ProductImportResultDTO
    {
        public bool DryRun { get; set; }
        public int TotalPages { get; set; }
        public int TotalInSource { get; set; }
        public int Created { get; set; }
        public int Updated { get; set; }
        public int Unchanged { get; set; }
        public int Skipped { get; set; }
        public int WarningCount { get; set; }
        public int ErrorCount { get; set; }
        public List<string> Warnings { get; set; } = new();
        public List<string> Errors { get; set; } = new();
    }
}
