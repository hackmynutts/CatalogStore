using CatalogStore.BackendAPI.Models.Client;
using System.ComponentModel.DataAnnotations;

namespace CatalogStore.BackendAPI.DTO.Client
{
    public class UpdateClientDTO
    {
        [Display(Name = "Nombre")]
        [Required(ErrorMessage = "El campo {0} es obligatorio.")]
        public string ClientName { get; set; } = string.Empty;
        [Display(Name = "Telefono")]
        [Required(ErrorMessage = "El campo {0} es obligatorio.")]
        public string ClientPhone { get; set; } = string.Empty;
        [Display(Name = "Correo")]
        [Required(ErrorMessage = "El campo {0} es obligatorio.")]
        public string ClientEmail { get; set; } = string.Empty;
        [Display(Name = "Dirección")]
        public string ClientAddress { get; set; } = string.Empty;
        [Display(Name = "Credito")]
        public credit? Credito { get; set; } = credit.D0;
        public string? cabys { get; set; }
        [Display(Name = "Estado")]
        public int StatusID { get; set; }
        [Display(Name = "Modificado por")]
        public string? ModifiedBy { get; set; }
        [Display(Name = "Modificado el")]
        public DateTime? ModifiedOn { get; set; }
    }
}
