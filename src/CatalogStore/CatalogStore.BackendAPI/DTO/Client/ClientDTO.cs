using CatalogStore.BackendAPI.Models.Client;
using System.ComponentModel.DataAnnotations;

namespace CatalogStore.BackendAPI.DTO.Client
{
    public class ClientDTO
    {
        [Display(Name = "#")]
        public int Id { get; set; }
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
        public credit? Credito { get; set; } = credit.D0;
        [Display(Name = "Actividad Economica")]
        public string? cabys { get; set; }
        [Display(Name = "Encomienda de Entrega")]
        public string? DeliveryPartner { get; set; }
        [Display(Name = "Estado")]
        public int StatusID { get; set; }
        [Display(Name = "Creado por")]
        public string CreatedBy { get; set; } = string.Empty;
        [Display(Name = "Creado el")]
        public DateTime CreatedOn { get; set; }
        [Display(Name = "Modificado por")]
        public string? ModifiedBy { get; set; } 
        [Display(Name = "Modificado el")]
        public DateTime? ModifiedOn { get; set; }
    }
}
