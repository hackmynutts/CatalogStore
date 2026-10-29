namespace CatalogStore.BackendAPI.DTO.Product
{
    // Lo único que ve el público del catálogo: sin precios, costos, existencias ni CABYS.
    public record PublicCatalogItemDTO(int Id, string? Code, string Name, int Category, string? ImageUrl);
}
