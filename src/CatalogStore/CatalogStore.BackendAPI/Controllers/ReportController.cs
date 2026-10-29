using CatalogStore.BackendAPI.Services.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;

namespace CatalogStore.BackendAPI.Controllers
{
    // Reportes en Excel (.xlsx). Ventas, cuentas por cobrar y cobros los puede pedir el vendedor (solo sobre sus ventas);
    // ventas por producto, metas e inventario traen costos y datos de todos: solo administradores (lo valida el servicio).
    // Errores de petición: 400 { message }.
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin,AdminIT,Vendedor")]
    public class ReportController : ControllerBase
    {
        private const string XlsxMime = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        private readonly IReportServices _reportServices;
        public ReportController(IReportServices reportServices) { _reportServices = reportServices; }
        private string CurrentUser => User.FindFirst(JwtRegisteredClaimNames.UniqueName)?.Value ?? "Sistema";
        private bool IsAdmin => User.IsInRole("Admin") || User.IsInRole("AdminIT");

        // GET api/Report/sales?from=2026-10-01&to=2026-10-31&seller=correo&includeVoided=true
        // GET api/Report/statement?clientId=5[&from=2026-01-01&to=2026-10-31]   ·   GET api/Report/goals?year=2026&month=10   ·   receivables, payments, products y stock: ver ReportKeys
        [HttpGet("{key}")]
        public async Task<IActionResult> Get(string key, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] string? seller,
            [FromQuery] bool includeVoided = false, [FromQuery] int? year = null, [FromQuery] int? month = null, [FromQuery] int? clientId = null)
        {
            var result = await _reportServices.BuildAsync(new ReportRequest(key, from, to, seller, includeVoided, year, month, clientId), CurrentUser, IsAdmin);
            return result.Ok
                ? File(result.File!.Bytes, XlsxMime, result.File.FileName)
                : BadRequest(new { message = result.Error });
        }
    }
}
