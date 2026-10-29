using CatalogStore.BackendAPI.DTO.Common;
using CatalogStore.BackendAPI.DTO.Goals;
using CatalogStore.BackendAPI.Services.Goals;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;

namespace CatalogStore.BackendAPI.Controllers
{
    // Metas mensuales de ventas. Cada usuario ve las suyas (me); solo admin ve al equipo y las configura.
    // year y month van juntos o ninguno (sin ellos = mes en curso). Errores: 400 { message }.
    [Route("api/[controller]")]
    [ApiController]
    public class GoalController : ControllerBase
    {
        private readonly IGoalServices _goalServices;
        public GoalController(IGoalServices goalServices)
        {
            _goalServices = goalServices;
        }
        private string CurrentUser => User.FindFirst(JwtRegisteredClaimNames.UniqueName)?.Value ?? "Sistema";

        // GET api/Goal/me?year=2026&month=10 — mi avance del mes
        [HttpGet("me")]
        [Authorize(Roles = "Admin,AdminIT,Vendedor")]
        public async Task<IActionResult> GetMine([FromQuery] int? year, [FromQuery] int? month)
        {
            if (GoalServices.PeriodError(year, month) is { } error)
                return BadRequest(new { message = error });
            return Ok(await _goalServices.GetMyProgressAsync(CurrentUser, year, month));
        }

        // GET api/Goal/me/detail?year=2026&month=10 — las ventas y abonos que suman en mi avance de ese mes
        [HttpGet("me/detail")]
        [Authorize(Roles = "Admin,AdminIT,Vendedor")]
        public async Task<IActionResult> GetMyDetail([FromQuery] int? year, [FromQuery] int? month)
        {
            if (GoalServices.PeriodError(year, month) is { } error)
                return BadRequest(new { message = error });
            return Ok(await _goalServices.GetMyDetailAsync(CurrentUser, year, month));
        }

        // GET api/Goal/me/history — mis últimos 6 meses
        [HttpGet("me/history")]
        [Authorize(Roles = "Admin,AdminIT,Vendedor")]
        public async Task<IActionResult> GetMyHistory() =>
            Ok(await _goalServices.GetMyHistoryAsync(CurrentUser));

        // GET api/Goal/team?year=2026&month=10 — el avance de todos los vendedores
        [HttpGet("team")]
        [Authorize(Roles = "Admin,AdminIT")]
        public async Task<IActionResult> GetTeam([FromQuery] int? year, [FromQuery] int? month)
        {
            if (GoalServices.PeriodError(year, month) is { } error)
                return BadRequest(new { message = error });
            return Ok(await _goalServices.GetTeamProgressAsync(year, month));
        }

        // PUT api/Goal — crear o actualizar la meta de un vendedor en un mes
        [HttpPut]
        [Authorize(Roles = "Admin,AdminIT")]
        public async Task<IActionResult> Set([FromBody] SetGoalDTO dto)
        {
            var result = await _goalServices.SetGoalAsync(dto, CurrentUser);
            return result.Success ? Ok(new { id = result.Id }) : BadRequest(new { message = result.Error });
        }

        // POST api/Goal/copy-previous — copiar las metas del mes anterior a quienes aún no tienen
        [HttpPost("copy-previous")]
        [Authorize(Roles = "Admin,AdminIT")]
        public async Task<IActionResult> CopyPrevious([FromBody] CopyGoalsDTO dto)
        {
            var result = await _goalServices.CopyPreviousMonthAsync(dto, CurrentUser);
            return result.Success ? Ok(new { copied = result.Id }) : BadRequest(new { message = result.Error });
        }
    }
}
