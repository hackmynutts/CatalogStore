using CatalogStore.BackendAPI.DTO.InventoryTransactions;
using CatalogStore.BackendAPI.Models.InventoryTransaction;
using CatalogStore.BackendAPI.Services.InventoryTransactions;
using CatalogStore.BackendAPI.Services.Product.CatalogExternal;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;

namespace CatalogStore.BackendAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin,AdminIT")]
    public class InventoryTransactionController : ControllerBase
    {
        private readonly IInventoryTransactionServices _inventoryTransactionServices;
        private readonly IStockImportServices _stockImportServices;
        public InventoryTransactionController(IInventoryTransactionServices inventoryTransactionServices, IStockImportServices stockImportServices)
        {
            _inventoryTransactionServices = inventoryTransactionServices;
            _stockImportServices = stockImportServices;
        }
        private string CurrentUser => User.FindFirst(JwtRegisteredClaimNames.UniqueName)?.Value ?? "Sistema";

        // GET api/InventoryTransaction/line/5 — historial de movimientos de una línea
        [HttpGet("line/{inventoryLineId}")]
        public async Task<IActionResult> GetByLine(int inventoryLineId) =>
            Ok(await _inventoryTransactionServices.GetByLineAsync(inventoryLineId));

        // POST api/InventoryTransaction/register — movimiento manual (incremento o ajuste)
        [HttpPost("register")]
        public async Task<IActionResult> Post([FromBody] RegisterInventoryTransactionDTO dto)
        {
            if (!IsManualType(dto.Type))
                return BadRequest(new { message = "Este tipo de movimiento no se puede registrar manualmente." });

            dto.ReferenceReason = ReferenceType.AjusteManual;
            var result = await _inventoryTransactionServices.RegisterAsync(dto, CurrentUser);
            if (!result.Success)
                return BadRequest(new { message = result.Error });

            return Ok(new { id = result.Id });
        }

        // POST api/InventoryTransaction/import-stock/1?dryRun=true — carga inicial de stock desde el proveedor a la bodega 1
        [HttpPost("import-stock/{inventoryId}")]
        public async Task<IActionResult> ImportStock(int inventoryId, [FromQuery] bool dryRun = false, CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await _stockImportServices.ImportAsync(inventoryId, dryRun, CurrentUser, cancellationToken);
                return Ok(result);
            }
            catch (StockImportValidationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (ProductImportInProgressException ex)
            {
                return Conflict(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
            }
            catch (HttpRequestException)
            {
                return StatusCode(StatusCodes.Status502BadGateway, new { message = "No se pudo obtener el inventario del proveedor externo. Revise la configuración o intente más tarde." });
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return StatusCode(StatusCodes.Status504GatewayTimeout, new { message = "El proveedor externo tardó demasiado en responder." });
            }
            catch (JsonException)
            {
                return StatusCode(StatusCodes.Status502BadGateway, new { message = "El proveedor externo devolvió una respuesta inválida." });
            }
        }

        private static bool IsManualType(TransactionType type) =>
            type is TransactionType.IncrementoStock or TransactionType.AjustePositivo or TransactionType.AjusteNegativo;
    }
}
