using System.Text.Json.Serialization;

namespace CatalogStore.BackendAPI.DTO.CatalogExternal
{
    public class ExternalCatalogPageDTO
    {
        [JsonPropertyName("ok")]
        public bool Ok { get; set; }
        [JsonPropertyName("page")]
        public int Page { get; set; }
        [JsonPropertyName("total")]
        public int Total { get; set; }
        [JsonPropertyName("total_pages")]
        public int TotalPages { get; set; }
        [JsonPropertyName("data")]
        public List<ExternalProductItemDTO> Data { get; set; } = new();
        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }
    public class ExternalProductItemDTO
    {
        [JsonPropertyName("producto")]
        public ExternalProductDTO Producto { get; set; } = new();
    }
    public class ExternalProductDTO
    {
        [JsonPropertyName("producto_id")]
        public int ProductoId { get; set; }
        [JsonPropertyName("codigo_producto")]
        public string CodigoProducto { get; set; } = string.Empty;
        [JsonPropertyName("estado_producto")]
        public string EstadoProducto { get; set; } = string.Empty;
        [JsonPropertyName("descripcion_espanol")]
        public string DescripcionEspanol { get; set; } = string.Empty;
        [JsonPropertyName("precio_venta_normal_SIN_IVA")]
        public decimal PrecioSinIva { get; set; }
        [JsonPropertyName("precio_venta_normal_CON_IVA")]
        public decimal PrecioConIva { get; set; }
        [JsonPropertyName("unidad_medida_nombre")]
        public string UnidadMedidaNombre { get; set; } = string.Empty;
    }
}