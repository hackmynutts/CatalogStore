using System.Net;
using System.Text.RegularExpressions;
using CatalogStore.BackendAPI.DTO.CatalogExternal;
using CatalogStore.BackendAPI.Models.Product;

namespace CatalogStore.BackendAPI.Services.Product.CatalogExternal
{
    public static class ExternalProductMapper
    {
        private const int MaxCodeLength = 20;
        private const int MaxNameLength = 150;
        private const int MaxDescLength = 250;
        private const decimal MaxPrice = 99_999_999.99m;
        private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

        // Unidades del proveedor con equivalente en nuestro enum.
        // Las que no estén aquí se importan como Unidad con una advertencia.
        private static readonly Dictionary<string, Unit> KnownUnits = new()
        {
            ["unidad"] = Unit.Unidad
        };

        public static ProductImportItem Map(ExternalProductDTO src)
        {
            var item = new ProductImportItem();
            var code = src.CodigoProducto?.Trim() ?? string.Empty;
            var tag = $"[{src.ProductoId}] {code}";

            // 1. ID externo: es la clave del upsert.
            item.ExternalProductID = src.ProductoId;
            if (src.ProductoId <= 0)
                item.Errors.Add($"{tag}: producto_id inválido.");

            // 2. Código: se recorta y se valida, nunca se trunca.
            item.ProductCode = code;
            if (code.Length == 0)
                item.Errors.Add($"{tag}: el código está vacío.");
            else if (code.Length > MaxCodeLength)
                item.Errors.Add($"{tag}: el código excede {MaxCodeLength} caracteres ({code.Length}).");

            // 3. Nombre y descripción: primero decodificar entidades HTML, luego normalizar espacios.
            var name = Whitespace.Replace(WebUtility.HtmlDecode(src.DescripcionEspanol ?? string.Empty), " ").Trim();
            item.ProductName = name;
            item.ProductDesc = name;
            if (name.Length == 0)
                item.Errors.Add($"{tag}: la descripción está vacía.");
            if (name.Length > MaxNameLength)
                item.Errors.Add($"{tag}: el nombre excede {MaxNameLength} caracteres ({name.Length}).");
            if (name.Length > MaxDescLength)
                item.Errors.Add($"{tag}: la descripción excede {MaxDescLength} caracteres ({name.Length}).");

            // 4. Precios: se usan los valores de la API (no se recalcula el IVA) y se redondean a 2 decimales.
            if (src.PrecioSinIva < 0 || src.PrecioConIva < 0)
            {
                item.Errors.Add($"{tag}: precio negativo.");
            }
            else
            {
                var price = RoundMoney(src.PrecioSinIva);
                var priceWithIva = RoundMoney(src.PrecioConIva);

                if (price == 0)
                {
                    item.Price = null;
                    item.PriceCalcIVA = 0;
                    item.Warnings.Add($"{tag}: sin precio, se importa con precio vacío.");
                }
                else if (price > MaxPrice || priceWithIva > MaxPrice)
                {
                    item.Errors.Add($"{tag}: el precio excede el máximo permitido ({MaxPrice:N2}).");
                }
                else
                {
                    item.Price = price;
                    item.PriceCalcIVA = priceWithIva;
                    if (priceWithIva < price)
                        item.Warnings.Add($"{tag}: el precio con IVA ({priceWithIva:N2}) es menor que el precio sin IVA ({price:N2}).");
                }
            }

            // 5. Unidad de medida.
            var unitName = (src.UnidadMedidaNombre ?? string.Empty).Trim().ToLowerInvariant();
            if (KnownUnits.TryGetValue(unitName, out var unit))
            {
                item.UnidadMedida = unit;
            }
            else
            {
                item.UnidadMedida = Unit.Unidad;
                var shown = unitName.Length == 0 ? "(vacía)" : src.UnidadMedidaNombre!.Trim();
                item.Warnings.Add($"{tag}: unidad de medida '{shown}' sin equivalente, se importó como Unidad.");
            }

            // 6. Estado: 1 = activo, 2 = inactivo.
            if (string.Equals(src.EstadoProducto?.Trim(), "ACTIVO", StringComparison.OrdinalIgnoreCase))
            {
                item.StatusID = 1;
            }
            else
            {
                item.StatusID = 2;
                item.Warnings.Add($"{tag}: estado '{src.EstadoProducto?.Trim()}' distinto de ACTIVO, se importó como inactivo.");
            }

            return item;
        }

        private static decimal RoundMoney(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}