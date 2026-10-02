namespace CatalogStore.BackendAPI.DTO.InventoryTransactions
{
    /// <summary>
    /// Resumen de una carga de stock desde el proveedor. Los mensajes de las listas están limitados;
    /// WarningCount y ErrorCount llevan el total real.
    /// </summary>
    public class StockImportResultDTO
    {
        public bool DryRun { get; set; }
        public int InventoryID { get; set; }
        public string InventoryName { get; set; } = string.Empty;
        public int TotalInSource { get; set; }
        // Líneas que no existían en la bodega y se crearon.
        public int LinesCreated { get; set; }
        // Líneas que recibieron su movimiento de CargaInicial, y la suma de unidades cargadas.
        public int InitialLoads { get; set; }
        public int UnitsLoaded { get; set; }
        // Líneas que ya tenían movimientos: no se tocan (la carga inicial es una sola vez por línea).
        public int AlreadyLoaded { get; set; }
        // Productos con 0 en el proveedor: se crea la línea si falta, pero sin movimiento.
        public int WithoutStock { get; set; }
        // Productos del proveedor que todavía no existen en el sistema (falta importar el catálogo).
        public int ProductsNotFound { get; set; }
        // Filas con datos inválidos, productos inactivos o líneas inactivas.
        public int Skipped { get; set; }
        public int WarningCount { get; set; }
        public int ErrorCount { get; set; }
        public List<string> Warnings { get; set; } = new();
        public List<string> Errors { get; set; } = new();
    }
}
