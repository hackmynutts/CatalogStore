namespace CatalogStore.BackendAPI.Models.Client
{
    // El tipo de identificación se deduce del número, porque el sistema nunca lo pedía (todos los clientes quedaban como "Cédula física" y Hacienda
    // rechazaba la factura de una empresa). Formatos del catálogo de Hacienda, solo dígitos y sin guiones:
    //   Cédula física 9 · Cédula jurídica 10 (empieza con 3) · DIMEX 11 o 12 · NITE 10 (el resto de los de 10).
    // Un extranjero no se deduce: si está marcado así, se respeta.
    public static class IdentificationTypes
    {
        public static string Normalize(string? identification) =>
            new((identification ?? string.Empty).Where(char.IsLetterOrDigit).ToArray());

        public static IdentificationType Infer(string? identification, IdentificationType stored)
        {
            if (stored == IdentificationType.Extranjero) return stored;
            var id = Normalize(identification);
            if (id.Length == 0 || !id.All(char.IsDigit)) return stored;
            return id.Length switch
            {
                9 => IdentificationType.Fisica,
                10 => id[0] == '3' ? IdentificationType.Juridica : IdentificationType.Nite,
                11 or 12 => IdentificationType.Dimex,
                _ => stored
            };
        }

        // null = el número tiene un formato válido para ese tipo.
        public static string? FormatError(string? identification, IdentificationType type)
        {
            var id = Normalize(identification);
            if (type == IdentificationType.Extranjero) return null;
            if (id.Length > 0 && !id.All(char.IsDigit))
                return $"La identificación del cliente ({id}) debe llevar solo números, sin guiones ni espacios.";
            bool ok = type switch
            {
                IdentificationType.Fisica => id.Length == 9,
                IdentificationType.Juridica or IdentificationType.Nite => id.Length == 10,
                IdentificationType.Dimex => id.Length is 11 or 12,
                _ => false
            };
            return ok ? null : $"La identificación del cliente tiene {id.Length} dígitos y no corresponde a ningún tipo válido (cédula física 9, jurídica 10, DIMEX 11 o 12). Corregila en el cliente.";
        }
    }
}
