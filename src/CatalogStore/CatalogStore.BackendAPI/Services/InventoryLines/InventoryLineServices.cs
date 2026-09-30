using CatalogStore.BackendAPI.Repository.InventoryLines;

namespace CatalogStore.BackendAPI.Services.InventoryLines
{
    public class InventoryLineServices
    {
        private readonly IInventoryLineRepository _inventoryLineRepository;
        public InventoryLineServices(IInventoryLineRepository inventoryLineRepository)
        {
            _inventoryLineRepository = inventoryLineRepository;
        }
    }
}
