using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using ZoneBill_Lloren.Data;
using ZoneBill_Lloren.Helpers;

namespace ZoneBill_Lloren.Filters
{
    public class ActiveSubscriptionFilter : IAsyncActionFilter
    {
        private readonly ApplicationDbContext _context;

        public ActiveSubscriptionFilter(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var user = context.HttpContext.User;
            if (!user.Identity?.IsAuthenticated ?? true)
            {
                await next();
                return;
            }

            if (user.IsInRole("SuperAdmin"))
            {
                await next();
                return;
            }

            var businessIdClaim = user.FindFirst("BusinessId")?.Value;
            if (!int.TryParse(businessIdClaim, out var businessId))
            {
                await next();
                return;
            }

            var business = await _context.Businesses
                .AsNoTracking()
                .Include(b => b.Plan)
                .FirstOrDefaultAsync(b => b.BusinessId == businessId);

            if (business == null)
            {
                context.Result = new ForbidResult();
                return;
            }

            var isActive = business.SubscriptionStatus == "Active";
            var isCurrent = business.CurrentPeriodEnd.HasValue && business.CurrentPeriodEnd.Value > PhilippineTime.Now;

            if (!isActive || !isCurrent)
            {
                context.Result = new RedirectToActionResult("Index", "Billing", null);
                return;
            }

            var isFreeTier = business.Plan != null &&
                             (business.Plan.MonthlyPrice <= 0m ||
                              string.Equals(business.Plan.PlanName, "Basic Lounge", StringComparison.OrdinalIgnoreCase));

            if (isFreeTier)
            {
                var restrictedControllers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "POS",
                    "Inventory",
                    "Reports",
                    "Shifts"
                };

                var targetController = context.RouteData.Values["controller"]?.ToString();
                if (!string.IsNullOrWhiteSpace(targetController) && restrictedControllers.Contains(targetController))
                {
                    context.Result = new RedirectToActionResult("Index", "Billing", null);
                    return;
                }
            }

            await next();
        }
    }
}
