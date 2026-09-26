namespace CatalogStore.BackendAPI.Services.Product.CatalogExternal
{
    public class ProductImportInProgressException : InvalidOperationException
    {
        public ProductImportInProgressException() : base("Ya hay una importación en curso.") { }
    }
}
