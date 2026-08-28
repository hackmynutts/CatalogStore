using CatalogStore.BackendAPI.Models.Client;
using System.ComponentModel.DataAnnotations;

namespace CatalogStore.BackendAPI.DTO.Client
{
    public class AddClientDTO
    {
        [Display(Name = "Cedula")]
        [Required(ErrorMessage = "El campo {0} es obligatorio.")]
        public string Identification { get; set; } = string.Empty;
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
        [Display(Name = "Creado por")]
        public string CreatedBy { get; set; } = string.Empty;
        [Display(Name = "Creado el")]
        public DateTime CreatedOn { get; set; }
    }
}
