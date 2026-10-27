using CatalogStore.BackendAPI.Models.Product;
using System.ComponentModel.DataAnnotations;

namespace CatalogStore.BackendAPI.DTO.Product
{
    // Asigna (o quita, con null/vacío) el CABYS de un producto.
    public class SetCabysDTO
    {
        [RegularExpression(@"^\d{13}$", ErrorMessage = "El CABYS tiene 13 dígitos.")]
        public string? CabysCode { get; set; }
    }

    // Asigna el mismo CABYS a todos los productos de una categoría.
    public class BulkCabysDTO
    {
        public categoryType Category { get; set; }
        [Required(ErrorMessage = "Indique el CABYS.")]
        [RegularExpression(@"^\d{13}$", ErrorMessage = "El CABYS tiene 13 dígitos.")]
        public string CabysCode { get; set; } = string.Empty;
        // true (por defecto): solo los que todavía no tienen CABYS. false: también reemplaza los que ya tienen.
        public bool OnlyEmpty { get; set; } = true;
    }

    // Resultado de la búsqueda en el catálogo oficial, para el selector de la pantalla.
    public class CabysItemDTO
    {
        public string Code { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        // Porcentaje de IVA que el catálogo asigna a ese código (13, 4, 2, 1, 0).
        public decimal TaxRate { get; set; }
        public string Category { get; set; } = string.Empty;
    }
}
