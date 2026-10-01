namespace CatalogStore.BackendAPI.DTO.Common
{
    public record OperationResult(bool Success, string? Error = null, int Id = 0)
    {
        public static OperationResult Ok(int id = 0) => new(true, null, id);
        public static OperationResult Fail(string error) => new(false, error);
    }
}
