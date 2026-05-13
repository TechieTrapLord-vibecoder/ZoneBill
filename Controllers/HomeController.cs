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
        private readonly INotificationService _notificationService;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context, INotificationService notificationService)
        {
            _logger = logger;
            _context = context;
            _notificationService = notificationService;
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

            }

            var plans = _context.SubscriptionPlans.Where(p => p.IsActive).ToList();
            return View(plans);
        }

        public IActionResult Privacy()
        {
            return View();
        }



        [Authorize(Roles = "MainAdmin")]
        public async Task<IActionResult> Dashboard(string? range = "7d", DateTime? startDate = null, DateTime? endDate = null)
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var today = PhilippineTime.Now.Date;
            var tomorrow = today.AddDays(1);
            var monthStart = new DateTime(today.Year, today.Month, 1);
            var yearStart = new DateTime(today.Year, 1, 1);

            var normalizedRange = (range ?? "7d").Trim().ToLowerInvariant();
            var rangeStart = today.AddDays(-6);
            var rangeEnd = today;
            var rangeLabel = "Last 7 Days";

            switch (normalizedRange)
            {
                case "today":
                    rangeStart = today;
                    rangeEnd = today;
                    rangeLabel = "Today";
                    break;
                case "30d":
                    rangeStart = today.AddDays(-29);
                    rangeEnd = today;
                    rangeLabel = "Last 30 Days";
                    break;
                case "mtd":
                    rangeStart = monthStart;
                    rangeEnd = today;
                    rangeLabel = "Month to Date";
                    break;
                case "ytd":
                    rangeStart = yearStart;
                    rangeEnd = today;
                    rangeLabel = "Year to Date";
                    break;
                case "custom":
                    rangeStart = (startDate ?? today.AddDays(-6)).Date;
                    rangeEnd = (endDate ?? today).Date;
                    if (rangeEnd < rangeStart)
                    {
                        rangeEnd = rangeStart;
                    }
                    rangeLabel = $"{rangeStart:MMM d} - {rangeEnd:MMM d, yyyy}";
                    break;
                default:
                    normalizedRange = "7d";
                    break;
            }

            var rangeEndExclusive = rangeEnd.AddDays(1);

            var dailyRows = await _context.Payments
                .Where(p => p.BusinessId == businessId.Value && p.PaymentDate >= rangeStart && p.PaymentDate < rangeEndExclusive)
                .GroupBy(p => p.PaymentDate.Date)
                .Select(g => new { Date = g.Key, Total = g.Sum(x => x.AmountPaid) })
                .ToListAsync();

            var dailyRevenueMap = dailyRows.ToDictionary(x => x.Date, x => x.Total);
            var dailyLabels = new List<string>();
            var dailySeries = new List<decimal>();

            for (var day = rangeStart; day <= rangeEnd; day = day.AddDays(1))
            {
                dailyLabels.Add(day.ToString("MMM dd"));
                dailySeries.Add(dailyRevenueMap.TryGetValue(day, out var value) ? value : 0m);
            }

            var topSpaces = await _context.Bookings
                .Include(b => b.Space)
                .Where(b => b.BusinessId == businessId.Value && b.EndTime != null && b.EndTime >= rangeStart && b.EndTime < rangeEndExclusive)
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
                .Where(od => od.Order.BusinessId == businessId.Value && od.Order.OrderTime >= rangeStart && od.Order.OrderTime < rangeEndExclusive)
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

            var paymentMethods = await _context.Payments
                .Where(p => p.BusinessId == businessId.Value && p.PaymentDate >= rangeStart && p.PaymentDate < rangeEndExclusive)
                .GroupBy(p => p.PaymentMethod)
                .Select(g => new { Method = g.Key, Total = g.Sum(x => x.AmountPaid) })
                .OrderByDescending(x => x.Total)
                .ToListAsync();

            var monthToDateRevenue = await _context.Payments
                .Where(p => p.BusinessId == businessId.Value && p.PaymentDate >= monthStart && p.PaymentDate < tomorrow)
                .SumAsync(p => (decimal?)p.AmountPaid) ?? 0m;

            var yearToDateRevenue = await _context.Payments
                .Where(p => p.BusinessId == businessId.Value && p.PaymentDate >= yearStart && p.PaymentDate < tomorrow)
                .SumAsync(p => (decimal?)p.AmountPaid) ?? 0m;

            var viewModel = new DashboardViewModel
            {
                TodayRevenue = dailyRevenueMap.TryGetValue(today, out var todayRevenue) ? todayRevenue : 0m,
                RangeRevenue = dailySeries.Sum(),
                MonthToDateRevenue = monthToDateRevenue,
                YearToDateRevenue = yearToDateRevenue,
                UnpaidInvoices = await _context.Invoices.CountAsync(i => i.BusinessId == businessId.Value && i.PaymentStatus == "Unpaid"),
                LowStockCount = lowStockItems.Count,
                LowStockItems = lowStockItems.Select(m => m.ItemName).ToList(),
                ActiveShiftCount = activeShiftCount,
                RangePreset = normalizedRange,
                RangeLabel = rangeLabel,
                RangeStart = rangeStart,
                RangeEnd = rangeEnd,
                DailyLabels = dailyLabels,
                DailyRevenueSeries = dailySeries,
                TopSpaceLabels = topSpaces.Select(x => x.SpaceName).ToList(),
                TopSpaceRevenueSeries = topSpaces.Select(x => Math.Round(x.Revenue, 2)).ToList(),
                TopMenuLabels = topMenus.Select(x => x.ItemName).ToList(),
                TopMenuQuantitySeries = topMenus.Select(x => x.Quantity).ToList(),
                PaymentMethodLabels = paymentMethods.Select(x => x.Method).ToList(),
                PaymentMethodAmounts = paymentMethods.Select(x => x.Total).ToList(),
                TodayBookings = await _context.Bookings.CountAsync(b => b.BusinessId == businessId.Value && b.StartTime >= today && b.StartTime < tomorrow),
                ActiveBookings = await _context.Bookings.CountAsync(b => b.BusinessId == businessId.Value && b.BookingStatus == "Active")
            };

            return View(viewModel);
        }

        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> SuperAdminDashboard()
        {
            var businesses = await _context.Businesses
                .Include(b => b.Plan)
                .ToListAsync();

            var totalBusinesses = businesses.Count;
            var activeBusinesses = businesses.Count(b => b.IsActive);
            var totalUsers = await _context.Users.CountAsync();
            var activeUsers = await _context.Users.CountAsync(u => u.IsActive);
            var currentPhilippineTime = PhilippineTime.Now;
            var activeSubscriptions = businesses.Count(b => b.SubscriptionStatus == "Active" && b.CurrentPeriodEnd != null && b.CurrentPeriodEnd > currentPhilippineTime);
            var pastDueSubscriptions = businesses.Count(b => b.SubscriptionStatus != "Active" || b.CurrentPeriodEnd == null || b.CurrentPeriodEnd <= currentPhilippineTime);

            var monthStart = new DateTime(currentPhilippineTime.Year, currentPhilippineTime.Month, 1);
            var nextMonthStart = monthStart.AddMonths(1);

            // Current MRR is projected run-rate from currently active subscriptions.
            var mrr = businesses
                .Where(b => b.IsActive && b.SubscriptionStatus == "Active" && b.CurrentPeriodEnd != null && b.CurrentPeriodEnd > currentPhilippineTime)
                .Sum(b => b.Plan.MonthlyPrice);

            var collectedThisMonth = await _context.SubscriptionInvoices
                .Where(i => i.Status == "Paid" && i.PaidAt != null && i.PaidAt >= monthStart && i.PaidAt < nextMonthStart)
                .SumAsync(i => i.Amount);

            var failedThisMonth = await _context.SubscriptionInvoices
                .Where(i => i.Status == "Failed" && i.IssuedAt >= monthStart && i.IssuedAt < nextMonthStart)
                .SumAsync(i => i.Amount);

            var outstandingReceivables = await _context.SubscriptionInvoices
                .Where(i => i.Status == "Pending" || i.Status == "Overdue")
                .SumAsync(i => i.Amount);

            var latestInvoiceRows = await _context.SubscriptionInvoices
                .Select(i => new { i.BusinessId, i.Status, i.Amount, i.IssuedAt, i.SubscriptionInvoiceId })
                .OrderByDescending(i => i.IssuedAt)
                .ThenByDescending(i => i.SubscriptionInvoiceId)
                .ToListAsync();

            var latestByBusiness = latestInvoiceRows
                .GroupBy(i => i.BusinessId)
                .ToDictionary(g => g.Key, g => g.First());

            var attentionCandidates = new List<(AttentionBusinessViewModel Row, int Score)>();
            foreach (var b in businesses)
            {
                var reasons = new List<string>();
                var score = 0;

                if (!b.IsActive)
                {
                    reasons.Add("Business inactive");
                    score += 2;
                }

                if (b.SubscriptionStatus != "Active")
                {
                    reasons.Add($"Subscription status: {b.SubscriptionStatus}");
                    score += 2;
                }

                if (b.CurrentPeriodEnd == null || b.CurrentPeriodEnd <= currentPhilippineTime)
                {
                    reasons.Add("Subscription expired");
                    score += 3;
                }

                latestByBusiness.TryGetValue(b.BusinessId, out var latestInv);
                var latestStatus = latestInv?.Status ?? "—";
                var latestAmount = latestInv?.Amount ?? 0m;

                if (latestStatus == "Overdue" || latestStatus == "Failed")
                {
                    reasons.Add($"Latest invoice {latestStatus.ToLowerInvariant()}");
                    score += 3;
                }
                else if (latestStatus == "Pending")
                {
                    reasons.Add("Latest invoice pending");
                    score += 1;
                }

                if (reasons.Count == 0)
                {
                    continue;
                }

                attentionCandidates.Add((
                    new AttentionBusinessViewModel
                    {
                        BusinessId = b.BusinessId,
                        BusinessName = b.BusinessName,
                        PlanName = b.Plan.PlanName,
                        SubscriptionStatus = b.SubscriptionStatus,
                        CurrentPeriodEnd = b.CurrentPeriodEnd,
                        LatestInvoiceStatus = latestStatus,
                        LatestInvoiceAmount = latestAmount,
                        AttentionReason = string.Join("; ", reasons),
                        IsActive = b.IsActive
                    },
                    score));
            }

            var planDistribution = businesses
                .GroupBy(b => b.Plan.PlanName)
                .Select(g => new
                {
                    PlanName = g.Key,
                    Count = g.Count()
                })
                .OrderByDescending(x => x.Count)
                .ToList();

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

            var thirtyDaysAgo = currentPhilippineTime.Date.AddDays(-29);
            var ninetyDaysAgo = currentPhilippineTime.Date.AddDays(-89);

            var newBusinesses30Days = businesses.Count(b => b.CreatedAt >= thirtyDaysAgo);
            var newBusinesses90Days = businesses.Count(b => b.CreatedAt >= ninetyDaysAgo);

            var lifecycleEvents = await _context.BusinessLifecycleEvents
                .Where(e => e.CreatedAt >= ninetyDaysAgo)
                .ToListAsync();

            var churnedBusinesses30Days = lifecycleEvents.Count(e => e.EventType == "Suspended" && e.CreatedAt >= thirtyDaysAgo);
            var churnedBusinesses90Days = lifecycleEvents.Count(e => e.EventType == "Suspended" && e.CreatedAt >= ninetyDaysAgo);

            var failedRenewals30Days = await _context.SubscriptionInvoices
                .CountAsync(i => (i.Status == "Failed" || i.Status == "Overdue") && i.IssuedAt >= thirtyDaysAgo);

            var failedRenewals90Days = await _context.SubscriptionInvoices
                .CountAsync(i => (i.Status == "Failed" || i.Status == "Overdue") && i.IssuedAt >= ninetyDaysAgo);

            var trendLabels = new List<string>();
            var newBusinessTrend = new List<int>();
            var churnTrend = new List<int>();
            var failedRenewalTrend = new List<int>();

            var signupMap = businesses
                .Where(b => b.CreatedAt >= thirtyDaysAgo)
                .GroupBy(b => b.CreatedAt.Date)
                .ToDictionary(g => g.Key, g => g.Count());

            var churnMap = lifecycleEvents
                .Where(e => e.EventType == "Suspended" && e.CreatedAt >= thirtyDaysAgo)
                .GroupBy(e => e.CreatedAt.Date)
                .ToDictionary(g => g.Key, g => g.Count());

            var failedMapRows = await _context.SubscriptionInvoices
                .Where(i => (i.Status == "Failed" || i.Status == "Overdue") && i.IssuedAt >= thirtyDaysAgo)
                .GroupBy(i => i.IssuedAt.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .ToListAsync();

            var failedMap = failedMapRows.ToDictionary(x => x.Date, x => x.Count);

            for (var day = thirtyDaysAgo; day <= currentPhilippineTime.Date; day = day.AddDays(1))
            {
                trendLabels.Add(day.ToString("MMM d"));
                newBusinessTrend.Add(signupMap.TryGetValue(day, out var signupCount) ? signupCount : 0);
                churnTrend.Add(churnMap.TryGetValue(day, out var churnCount) ? churnCount : 0);
                failedRenewalTrend.Add(failedMap.TryGetValue(day, out var failedCount) ? failedCount : 0);
            }

            var recentAuditLogs = await _context.SuperAdminAuditLogs
                .OrderByDescending(a => a.CreatedAt)
                .Take(12)
                .Select(a => new SuperAdminAuditItemViewModel
                {
                    AuditLogId = a.AuditLogId,
                    ActionType = a.ActionType,
                    EntityType = a.EntityType,
                    EntityId = a.EntityId,
                    BusinessId = a.BusinessId,
                    BusinessName = a.BusinessName,
                    Details = a.Details ?? string.Empty,
                    Reason = a.Reason,
                    ActorName = string.IsNullOrWhiteSpace(a.ActorName) ? "System" : a.ActorName!,
                    CreatedAt = a.CreatedAt
                })
                .ToListAsync();

            var viewModel = new SuperAdminDashboardViewModel
            {
                TotalBusinesses = totalBusinesses,
                ActiveBusinesses = activeBusinesses,
                TotalUsers = totalUsers,
                ActiveUsers = activeUsers,
                MonthlyRecurringRevenue = mrr,
                CollectedThisMonth = collectedThisMonth,
                FailedThisMonth = failedThisMonth,
                OutstandingReceivables = outstandingReceivables,
                ActiveSubscriptions = activeSubscriptions,
                PastDueSubscriptions = pastDueSubscriptions,
                BusinessesNeedingAttention = attentionCandidates.Count,
                NewBusinesses30Days = newBusinesses30Days,
                NewBusinesses90Days = newBusinesses90Days,
                ChurnedBusinesses30Days = churnedBusinesses30Days,
                ChurnedBusinesses90Days = churnedBusinesses90Days,
                FailedRenewals30Days = failedRenewals30Days,
                FailedRenewals90Days = failedRenewals90Days,
                PlanLabels = planDistribution.Select(x => x.PlanName).ToList(),
                PlanBusinessCounts = planDistribution.Select(x => x.Count).ToList(),
                RecentSignups = recentSignups,
                TrendLabels = trendLabels,
                NewBusinessTrend = newBusinessTrend,
                ChurnTrend = churnTrend,
                FailedRenewalTrend = failedRenewalTrend,
                RecentAuditLogs = recentAuditLogs,
                AttentionBusinesses = attentionCandidates
                    .OrderByDescending(x => x.Score)
                    .ThenBy(x => x.Row.BusinessName)
                    .Take(10)
                    .Select(x => x.Row)
                    .ToList()
            };

            return View(viewModel);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult StatusCode(int code)
        {
            Response.StatusCode = code;
            ViewBag.StatusCode = code;
            ViewBag.Title = code switch
            {
                404 => "Page Not Found",
                429 => "Too Many Requests",
                403 => "Access Denied",
                500 => "Server Error",
                _   => "Something Went Wrong"
            };
            ViewBag.Message = code switch
            {
                404 => "The page you're looking for doesn't exist or may have been moved.",
                429 => "You've made too many requests in a short period. Please wait a moment and try again.",
                403 => "You don't have permission to access this page.",
                500 => "Something went wrong on our end. We're already working on fixing it.",
                _   => "An unexpected error occurred. Please try again."
            };
            ViewBag.Icon = code switch
            {
                404 => "bi-map",
                429 => "bi-hourglass-split",
                403 => "bi-shield-exclamation",
                500 => "bi-exclamation-triangle",
                _   => "bi-x-circle"
            };
            return View("StatusCode");
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetNotificationCount()
        {
            var summary = await _notificationService.GetBusinessNotificationsAsync(User);
            return Json(new { count = summary.Count });
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetNotifications()
        {
            var summary = await _notificationService.GetBusinessNotificationsAsync(User);
            return Json(summary);
        }

        private int? GetBusinessId()
        {
            var value = User.FindFirstValue("BusinessId");
            return int.TryParse(value, out var businessId) ? businessId : null;
        }

        // ── SuperAdmin: Dedicated Audit Log Page ──────────────────────────────────
        [Authorize(Roles = "SuperAdmin")]
        [HttpGet]
        public async Task<IActionResult> SuperAdminAuditLog(
            string? actionType, string? entityType, string? actor, string? business,
            DateTime? fromDate, DateTime? toDate, string? exportCsv, int page = 1)
        {
            const int pageSize = 25;
            var query = _context.SuperAdminAuditLogs.AsQueryable();

            if (!string.IsNullOrWhiteSpace(actionType)) query = query.Where(a => a.ActionType == actionType);
            if (!string.IsNullOrWhiteSpace(entityType)) query = query.Where(a => a.EntityType == entityType);
            if (!string.IsNullOrWhiteSpace(actor))      query = query.Where(a => a.ActorName != null && a.ActorName.Contains(actor));
            if (!string.IsNullOrWhiteSpace(business))   query = query.Where(a => a.BusinessName != null && a.BusinessName.Contains(business));
            if (fromDate.HasValue) query = query.Where(a => a.CreatedAt >= fromDate.Value);
            if (toDate.HasValue)   query = query.Where(a => a.CreatedAt < toDate.Value.AddDays(1));

            if (!string.IsNullOrEmpty(exportCsv))
            {
                var all = await query.OrderByDescending(a => a.CreatedAt).ToListAsync();
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("Timestamp (PH),ActionType,EntityType,EntityId,Business,Details,Reason,Actor");
                foreach (var log in all)
                {
                    var ts = PhilippineTime.ToDateTime(log.CreatedAt).ToString("yyyy-MM-dd HH:mm:ss");
                    static string Esc(string? v) => (v ?? "").Replace("\"", "'").Replace("\n", " ");
                    sb.AppendLine($"\"{ts}\",\"{log.ActionType}\",\"{log.EntityType}\",\"{log.EntityId}\",\"{Esc(log.BusinessName)}\",\"{Esc(log.Details)}\",\"{Esc(log.Reason)}\",\"{Esc(log.ActorName)}\"");
                }
                var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
                return File(bytes, "text/csv", $"audit-log-{PhilippineTime.Now:yyyyMMdd-HHmmss}.csv");
            }

            var totalCount = await query.CountAsync();
            var logs = await query
                .OrderByDescending(a => a.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new SuperAdminAuditItemViewModel
                {
                    AuditLogId  = a.AuditLogId,
                    ActionType  = a.ActionType,
                    EntityType  = a.EntityType,
                    EntityId    = a.EntityId,
                    BusinessId  = a.BusinessId,
                    BusinessName = a.BusinessName,
                    Details     = a.Details ?? string.Empty,
                    Reason      = a.Reason,
                    ActorName   = string.IsNullOrWhiteSpace(a.ActorName) ? "System" : a.ActorName!,
                    CreatedAt   = a.CreatedAt
                })
                .ToListAsync();

            var actionTypes = await _context.SuperAdminAuditLogs
                .Select(a => a.ActionType).Distinct().OrderBy(x => x).ToListAsync();
            var entityTypes = await _context.SuperAdminAuditLogs
                .Select(a => a.EntityType).Distinct().OrderBy(x => x).ToListAsync();

            return View(new AuditLogPageViewModel
            {
                Logs                 = logs,
                TotalCount           = totalCount,
                Page                 = page,
                PageSize             = pageSize,
                FilterActionType     = actionType,
                FilterEntityType     = entityType,
                FilterActor          = actor,
                FilterBusiness       = business,
                FilterFrom           = fromDate,
                FilterTo             = toDate,
                AvailableActionTypes = actionTypes,
                AvailableEntityTypes = entityTypes
            });
        }

        // ── SuperAdmin: All Flagged Tenants (paginated) ───────────────────────────
        [Authorize(Roles = "SuperAdmin")]
        [HttpGet]
        public async Task<IActionResult> AllAttentionBusinesses(
            string? filter, string? search, string? exportCsv, int page = 1)
        {
            const int pageSize = 20;
            var now = PhilippineTime.Now;

            var businesses = await _context.Businesses.Include(b => b.Plan).ToListAsync();

            var latestInvoiceRows = await _context.SubscriptionInvoices
                .Select(i => new { i.BusinessId, i.Status, i.Amount, i.IssuedAt, i.SubscriptionInvoiceId })
                .OrderByDescending(i => i.IssuedAt)
                .ThenByDescending(i => i.SubscriptionInvoiceId)
                .ToListAsync();

            var latestByBusiness = latestInvoiceRows
                .GroupBy(i => i.BusinessId)
                .ToDictionary(g => g.Key, g => g.First());

            var candidates = new List<(AttentionBusinessViewModel Row, int Score)>();
            foreach (var b in businesses)
            {
                var reasons = new List<string>();
                var score   = 0;

                if (!b.IsActive)                  { reasons.Add("Business inactive");              score += 2; }
                if (b.SubscriptionStatus != "Active") { reasons.Add($"Subscription: {b.SubscriptionStatus}"); score += 2; }
                if (b.CurrentPeriodEnd == null || b.CurrentPeriodEnd <= now) { reasons.Add("Subscription expired"); score += 3; }

                latestByBusiness.TryGetValue(b.BusinessId, out var latestInv);
                var latestStatus = latestInv?.Status ?? "—";
                var latestAmount = latestInv?.Amount ?? 0m;

                if (latestStatus == "Overdue" || latestStatus == "Failed")
                    { reasons.Add($"Invoice {latestStatus.ToLowerInvariant()}"); score += 3; }
                else if (latestStatus == "Pending")
                    { reasons.Add("Invoice pending"); score += 1; }

                if (reasons.Count == 0) continue;

                candidates.Add((new AttentionBusinessViewModel
                {
                    BusinessId           = b.BusinessId,
                    BusinessName         = b.BusinessName,
                    PlanName             = b.Plan.PlanName,
                    SubscriptionStatus   = b.SubscriptionStatus,
                    CurrentPeriodEnd     = b.CurrentPeriodEnd,
                    LatestInvoiceStatus  = latestStatus,
                    LatestInvoiceAmount  = latestAmount,
                    AttentionReason      = string.Join("; ", reasons),
                    IsActive             = b.IsActive
                }, score));
            }

            IEnumerable<(AttentionBusinessViewModel Row, int Score)> filtered = candidates;

            if (!string.IsNullOrWhiteSpace(search))
                filtered = filtered.Where(x => x.Row.BusinessName.Contains(search, StringComparison.OrdinalIgnoreCase));

            filtered = filter switch
            {
                "expired"  => filtered.Where(x => x.Row.AttentionReason.Contains("expired", StringComparison.OrdinalIgnoreCase)),
                "failed"   => filtered.Where(x => x.Row.LatestInvoiceStatus is "Failed" or "Overdue"),
                "pending"  => filtered.Where(x => x.Row.LatestInvoiceStatus == "Pending"),
                "inactive" => filtered.Where(x => !x.Row.IsActive),
                _          => filtered
            };

            var sorted = filtered.OrderByDescending(x => x.Score).ThenBy(x => x.Row.BusinessName).ToList();

            if (!string.IsNullOrEmpty(exportCsv))
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("Business,Plan,Status,PeriodEnd,LatestInvoice,InvoiceAmt,FlagReason,Active");
                foreach (var (row, _) in sorted)
                {
                    var end = row.CurrentPeriodEnd.HasValue
                        ? PhilippineTime.ToDateTime(row.CurrentPeriodEnd.Value).ToString("yyyy-MM-dd")
                        : "";
                    sb.AppendLine($"\"{row.BusinessName}\",\"{row.PlanName}\",\"{row.SubscriptionStatus}\",\"{end}\",\"{row.LatestInvoiceStatus}\",\"{row.LatestInvoiceAmount:F2}\",\"{row.AttentionReason.Replace("\"", "'")}\",\"{row.IsActive}\"");
                }
                var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
                return File(bytes, "text/csv", $"flagged-tenants-{PhilippineTime.Now:yyyyMMdd}.csv");
            }

            var totalCount = sorted.Count;
            var pageResults = sorted.Skip((page - 1) * pageSize).Take(pageSize).Select(x => x.Row).ToList();

            return View(new AttentionPageViewModel
            {
                Businesses = pageResults,
                TotalCount = totalCount,
                Page       = page,
                PageSize   = pageSize,
                Filter     = filter,
                Search     = search
            });
        }

        // ── SuperAdmin: Notification Bell — Count ─────────────────────────────────
        [Authorize(Roles = "SuperAdmin")]
        [HttpGet]
        public async Task<IActionResult> GetSuperAdminNotificationCount()
        {
            var now = PhilippineTime.Now;
            var failedInvoices  = await _context.SubscriptionInvoices.CountAsync(i => i.Status == "Failed");
            var expiredBiz      = await _context.Businesses.CountAsync(b =>
                b.IsActive && (b.SubscriptionStatus != "Active" ||
                               b.CurrentPeriodEnd == null || b.CurrentPeriodEnd <= now));
            var newSignupsToday = await _context.Businesses.CountAsync(b => b.CreatedAt >= now.Date);
            return Json(new { count = failedInvoices + expiredBiz, failedInvoices, expiredBiz, newSignupsToday });
        }

        // ── SuperAdmin: Notification Bell — Items for dropdown ────────────────────
        [Authorize(Roles = "SuperAdmin")]
        [HttpGet]
        public async Task<IActionResult> GetSuperAdminNotifications()
        {
            var now       = PhilippineTime.Now;
            var todayStart = now.Date;

            var items = new List<object>();

            var failedInvoices = await _context.SubscriptionInvoices
                .Include(i => i.Business)
                .Where(i => i.Status == "Failed")
                .OrderByDescending(i => i.IssuedAt)
                .Take(5)
                .ToListAsync();

            foreach (var inv in failedInvoices)
                items.Add(new { type = "danger", icon = "bi-x-circle-fill",
                    label = $"Failed invoice — {inv.Business?.BusinessName ?? "Unknown"} (₱{inv.Amount:N2})",
                    link  = "/SubscriptionInvoices" });

            var expiredBiz = await _context.Businesses
                .Where(b => b.IsActive && (b.SubscriptionStatus != "Active" ||
                                           b.CurrentPeriodEnd == null || b.CurrentPeriodEnd <= now))
                .OrderBy(b => b.CurrentPeriodEnd)
                .Take(4)
                .ToListAsync();

            foreach (var biz in expiredBiz)
                items.Add(new { type = "warning", icon = "bi-clock-history",
                    label = $"Expired — {biz.BusinessName}",
                    link  = $"/Businesses/Details/{biz.BusinessId}" });

            var signups = await _context.Businesses
                .Where(b => b.CreatedAt >= todayStart)
                .OrderByDescending(b => b.CreatedAt)
                .Take(3)
                .ToListAsync();

            foreach (var biz in signups)
                items.Add(new { type = "success", icon = "bi-shop-window",
                    label = $"New signup — {biz.BusinessName}",
                    link  = $"/Businesses/Details/{biz.BusinessId}" });

            return Json(new { items });
        }
    }
}
