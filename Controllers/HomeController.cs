using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ZoneBill_Lloren.Models;
using ZoneBill_Lloren.Data;
using ZoneBill_Lloren.Helpers;

namespace ZoneBill_Lloren.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public IActionResult Index()
        {
            // If they are already logged in, skip the landing page and go straight to their dashboard
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                if (User.IsInRole("SuperAdmin")) return RedirectToAction(nameof(SuperAdminDashboard));
                if (User.IsInRole("MainAdmin")) return RedirectToAction(nameof(Dashboard));
                if (User.IsInRole("Manager")) return RedirectToAction("Index", "Reports");
                if (User.IsInRole("Cashier")) return RedirectToAction("Index", "POS");
                if (User.IsInRole("Staff")) return RedirectToAction("Index", "Bookings");
            }

            var plans = _context.SubscriptionPlans.Where(p => p.IsActive).ToList();
            return View(plans);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [Authorize(Roles = "MainAdmin")]
        public async Task<IActionResult> Dashboard()
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var today = PhilippineTime.Now.Date;
            var sevenDaysAgo = today.AddDays(-6);
            var tomorrow = today.AddDays(1);

            var dailyRows = await _context.Payments
                .Where(p => p.BusinessId == businessId.Value && p.PaymentDate >= sevenDaysAgo && p.PaymentDate < tomorrow)
                .GroupBy(p => p.PaymentDate.Date)
                .Select(g => new { Date = g.Key, Total = g.Sum(x => x.AmountPaid) })
                .ToListAsync();

            var dailyRevenueMap = dailyRows.ToDictionary(x => x.Date, x => x.Total);
            var dailyLabels = new List<string>();
            var dailySeries = new List<decimal>();

            for (var day = sevenDaysAgo; day <= today; day = day.AddDays(1))
            {
                dailyLabels.Add(day.ToString("MMM dd"));
                dailySeries.Add(dailyRevenueMap.TryGetValue(day, out var value) ? value : 0m);
            }

            var topSpaces = await _context.Bookings
                .Include(b => b.Space)
                .Where(b => b.BusinessId == businessId.Value && b.EndTime != null && b.EndTime >= sevenDaysAgo)
                .GroupBy(b => b.Space.SpaceName)
                .Select(g => new
                {
                    SpaceName = g.Key,
                    Revenue = g.Sum(x => (x.DurationHours ?? 0m) * x.LockedHourlyRate)
                })
                .OrderByDescending(x => x.Revenue)
                .Take(5)
                .ToListAsync();

            var topMenus = await _context.OrderDetails
                .Include(od => od.Order)
                .Include(od => od.MenuItem)
                .Where(od => od.Order.BusinessId == businessId.Value && od.Order.OrderTime >= sevenDaysAgo)
                .GroupBy(od => od.MenuItem.ItemName)
                .Select(g => new
                {
                    ItemName = g.Key,
                    Quantity = g.Sum(x => x.Quantity)
                })
                .OrderByDescending(x => x.Quantity)
                .Take(5)
                .ToListAsync();

            var lowStockItems = await _context.MenuItems
                .Where(m => m.BusinessId == businessId.Value && m.IsActive && m.StockAvailable <= m.LowStockThreshold)
                .OrderBy(m => m.StockAvailable)
                .ToListAsync();

            var activeShiftCount = await _context.PosShifts
                .CountAsync(s => s.BusinessId == businessId.Value && s.Status == "Open");

            var viewModel = new DashboardViewModel
            {
                TodayRevenue = dailyRevenueMap.TryGetValue(today, out var todayRevenue) ? todayRevenue : 0m,
                SevenDayRevenue = dailySeries.Sum(),
                UnpaidInvoices = await _context.Invoices.CountAsync(i => i.BusinessId == businessId.Value && i.PaymentStatus == "Unpaid"),
                LowStockCount = lowStockItems.Count,
                LowStockItems = lowStockItems.Select(m => m.ItemName).ToList(),
                ActiveShiftCount = activeShiftCount,
                DailyLabels = dailyLabels,
                DailyRevenueSeries = dailySeries,
                TopSpaceLabels = topSpaces.Select(x => x.SpaceName).ToList(),
                TopSpaceRevenueSeries = topSpaces.Select(x => Math.Round(x.Revenue, 2)).ToList(),
                TopMenuLabels = topMenus.Select(x => x.ItemName).ToList(),
                TopMenuQuantitySeries = topMenus.Select(x => x.Quantity).ToList()
            };

            return View(viewModel);
        }

        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> SuperAdminDashboard()
        {
            var totalBusinesses = await _context.Businesses.CountAsync();
            var activeBusinesses = await _context.Businesses.CountAsync(b => b.IsActive);
            var totalUsers = await _context.Users.CountAsync();
            var activeUsers = await _context.Users.CountAsync(u => u.IsActive);
            var currentPhilippineTime = PhilippineTime.Now;
            var activeSubscriptions = await _context.Businesses.CountAsync(b => b.SubscriptionStatus == "Active" && b.CurrentPeriodEnd != null && b.CurrentPeriodEnd > currentPhilippineTime);
            var pastDueSubscriptions = await _context.Businesses.CountAsync(b => b.SubscriptionStatus != "Active" || b.CurrentPeriodEnd == null || b.CurrentPeriodEnd <= currentPhilippineTime);

            var monthStart = new DateTime(currentPhilippineTime.Year, currentPhilippineTime.Month, 1);
            var nextMonthStart = monthStart.AddMonths(1);

            var mrr = await _context.SubscriptionInvoices
                .Where(i => i.Status == "Paid" && i.PaidAt != null && i.PaidAt >= monthStart && i.PaidAt < nextMonthStart)
                .SumAsync(i => i.Amount);

            var planDistribution = await _context.Businesses
                .Include(b => b.Plan)
                .GroupBy(b => b.Plan.PlanName)
                .Select(g => new
                {
                    PlanName = g.Key,
                    Count = g.Count()
                })
                .OrderByDescending(x => x.Count)
                .ToListAsync();

            var recentSignups = await _context.Businesses
                .Include(b => b.Plan)
                .OrderByDescending(b => b.CreatedAt)
                .Take(8)
                .Select(b => new BusinessSignupViewModel
                {
                    BusinessName = b.BusinessName,
                    PlanName = b.Plan.PlanName,
                    CreatedAt = b.CreatedAt
                })
                .ToListAsync();

            var viewModel = new SuperAdminDashboardViewModel
            {
                TotalBusinesses = totalBusinesses,
                ActiveBusinesses = activeBusinesses,
                TotalUsers = totalUsers,
                ActiveUsers = activeUsers,
                MonthlyRecurringRevenue = mrr,
                ActiveSubscriptions = activeSubscriptions,
                PastDueSubscriptions = pastDueSubscriptions,
                PlanLabels = planDistribution.Select(x => x.PlanName).ToList(),
                PlanBusinessCounts = planDistribution.Select(x => x.Count).ToList(),
                RecentSignups = recentSignups
            };

            return View(viewModel);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetNotificationCount()
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Json(new { count = 0 });

            var unpaidInvoices = await _context.Invoices
                .CountAsync(i => i.BusinessId == businessId.Value && i.PaymentStatus == "Unpaid");

            var checkoutRequests = await _context.Bookings
                .CountAsync(b => b.BusinessId == businessId.Value && b.BookingStatus == "Active" && b.CheckoutRequested);

            var lowStock = await _context.MenuItems
                .CountAsync(m => m.BusinessId == businessId.Value && m.IsActive && m.StockAvailable <= m.LowStockThreshold);

            return Json(new { count = unpaidInvoices + checkoutRequests + lowStock });
        }

        private int? GetBusinessId()
        {
            var value = User.FindFirstValue("BusinessId");
            return int.TryParse(value, out var businessId) ? businessId : null;
        }
    }
}
