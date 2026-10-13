using System.ComponentModel.DataAnnotations;

namespace CatalogStore.BackendAPI.DTO.Order
{
    public class SetApplyIvaDTO
    {
        [Display(Name = "Aplica IVA")]
        public bool ApplyIva { get; set; } = true;
    }
}
