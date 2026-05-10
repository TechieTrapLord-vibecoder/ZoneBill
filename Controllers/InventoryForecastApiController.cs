using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ZoneBill_Lloren.Data;
using ZoneBill_Lloren.Filters;
using ZoneBill_Lloren.Helpers;
using ZoneBill_Lloren.Models;

namespace ZoneBill_Lloren.Controllers
{
    [ApiController]
    [Authorize(Roles = "MainAdmin,Manager")]
    [ServiceFilter(typeof(ActiveSubscriptionFilter))]
    [Route("api/inventory/forecast")]
    public class InventoryForecastApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IDemandForecastService _demandForecastService;

        public InventoryForecastApiController(
            ApplicationDbContext context,
            IDemandForecastService demandForecastService)
        {
            _context = context;
            _demandForecastService = demandForecastService;
        }

        [HttpGet]
        [ProducesResponseType(typeof(InventoryForecastApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetForecast([FromQuery] int? lookbackDays = null, [FromQuery] int? horizonDays = null, CancellationToken cancellationToken = default)
        {
            var businessId = GetBusinessId();
            if (businessId == null)
            {
                return Forbid();
            }

            InventoryDemandForecastSummaryViewModel forecast;
            if (!lookbackDays.HasValue && !horizonDays.HasValue)
            {
                forecast = await _demandForecastService.BuildDemandForecastSummaryAsync(businessId.Value, cancellationToken);
            }
            else
            {
                var business = await _context.Businesses
                    .AsNoTracking()
                    .FirstOrDefaultAsync(b => b.BusinessId == businessId.Value, cancellationToken);

                if (business == null)
                {
                    return NotFound();
                }

                var effectiveLookbackDays = lookbackDays ?? business.InventoryForecastLookbackDays;
                var effectiveHorizonDays = horizonDays ?? business.InventoryForecastHorizonDays;

                forecast = await _demandForecastService.BuildDemandForecastSummaryAsync(
                    businessId.Value,
                    effectiveLookbackDays,
                    effectiveHorizonDays,
                    cancellationToken);
            }

            return Ok(new InventoryForecastApiResponse
            {
                BusinessId = businessId.Value,
                GeneratedAtPh = PhilippineTime.Now,
                Forecast = forecast
            });
        }

        private int? GetBusinessId()
        {
            var value = User.FindFirstValue("BusinessId");
            return int.TryParse(value, out var businessId) ? businessId : null;
        }
    }

    public class InventoryForecastApiResponse
    {
        public int BusinessId { get; set; }
        public DateTime GeneratedAtPh { get; set; }
        public InventoryDemandForecastSummaryViewModel Forecast { get; set; } = new();
    }
}