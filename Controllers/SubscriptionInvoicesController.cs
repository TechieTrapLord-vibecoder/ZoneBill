using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using ZoneBill_Lloren.Data;
using ZoneBill_Lloren.Helpers;
using ZoneBill_Lloren.Models;

namespace ZoneBill_Lloren.Controllers
{
    [Authorize(Roles = "SuperAdmin")]
    public class SubscriptionInvoicesController : Controller
    {
        private const string DateFormat = "yyyy-MM-dd";
        private readonly ApplicationDbContext _context;

        public SubscriptionInvoicesController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string? status, int? businessId, int? planId, DateTime? fromDate, DateTime? toDate, string? q, int page = 1)
        {
            var query = _context.SubscriptionInvoices
                .Include(i => i.Business)
                .Include(i => i.Plan)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "All", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(i => i.Status == status);
            }

            if (businessId.HasValue)
            {
                query = query.Where(i => i.BusinessId == businessId.Value);
            }

            if (planId.HasValue)
            {
                query = query.Where(i => i.PlanId == planId.Value);
            }

            if (fromDate.HasValue)
            {
                query = query.Where(i => i.IssuedAt >= fromDate.Value.Date);
            }

            if (toDate.HasValue)
            {
                var end = toDate.Value.Date.AddDays(1);
                query = query.Where(i => i.IssuedAt < end);
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                query = query.Where(i =>
                    i.Business.BusinessName.Contains(term) ||
                    i.Plan.PlanName.Contains(term) ||
                    (i.ExternalReference != null && i.ExternalReference.Contains(term)) ||
                    i.Status.Contains(term));
            }

            var allInvoices = await query
                .OrderByDescending(i => i.IssuedAt)
                .ToListAsync();

            const int pageSize = 15;
            var totalCount = allInvoices.Count;
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
            page = Math.Clamp(page, 1, totalPages);

            ViewBag.TotalCount = totalCount;
            ViewBag.KpiPaid = allInvoices.Count(i => i.Status == "Paid");
            ViewBag.KpiPending = allInvoices.Count(i => i.Status == "Pending");
            ViewBag.KpiFailedOverdue = allInvoices.Count(i => i.Status == "Failed" || i.Status == "Overdue");
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;

            var invoices = allInvoices.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            var businesses = await _context.Businesses
                .OrderBy(b => b.BusinessName)
                .Select(b => new SelectListItem { Value = b.BusinessId.ToString(), Text = b.BusinessName })
                .ToListAsync();

            var plans = await _context.SubscriptionPlans
                .OrderBy(p => p.PlanName)
                .Select(p => new SelectListItem { Value = p.PlanId.ToString(), Text = p.PlanName })
                .ToListAsync();

            ViewBag.BusinessOptions = businesses;
            ViewBag.PlanOptions = plans;
            ViewBag.FilterStatus = status ?? "All";
            ViewBag.FilterBusinessId = businessId;
            ViewBag.FilterPlanId = planId;
            ViewBag.FilterFromDate = fromDate?.ToString(DateFormat);
            ViewBag.FilterToDate = toDate?.ToString(DateFormat);
            ViewBag.FilterQ = q;

            return View(invoices);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkPaid(int id, string? returnUrl = null)
        {
            var invoice = await _context.SubscriptionInvoices.FindAsync(id);
            if (invoice == null)
            {
                TempData["Error"] = "Subscription invoice not found.";
                return RedirectToAction(nameof(Index));
            }

            invoice.Status = "Paid";
            invoice.PaidAt = PhilippineTime.Now;
            if (string.IsNullOrWhiteSpace(invoice.PaymentMethod))
            {
                invoice.PaymentMethod = "ManualOverride";
            }

            _context.SuperAdminAuditLogs.Add(new SuperAdminAuditLog
            {
                ActionType = "ManualMarkPaid",
                EntityType = "SubscriptionInvoice",
                EntityId = invoice.SubscriptionInvoiceId,
                BusinessId = invoice.BusinessId,
                Details = $"Invoice set to Paid manually. Amount={invoice.Amount:0.00}",
                Reason = "Manual mark as paid",
                ActorUserId = TryGetCurrentUserId(),
                ActorName = User.Identity?.Name ?? "SuperAdmin",
                CreatedAt = PhilippineTime.Now
            });

            await _context.SaveChangesAsync();
            TempData["Success"] = "Invoice marked as Paid.";

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Retry(int id, string? returnUrl = null)
        {
            var invoice = await _context.SubscriptionInvoices.FindAsync(id);
            if (invoice == null)
            {
                TempData["Error"] = "Subscription invoice not found.";
                return RedirectToAction(nameof(Index));
            }

            if (invoice.Status == "Failed" || invoice.Status == "Overdue")
            {
                invoice.Status = "Pending";
                invoice.PaidAt = null;

                _context.SuperAdminAuditLogs.Add(new SuperAdminAuditLog
                {
                    ActionType = "ManualRetry",
                    EntityType = "SubscriptionInvoice",
                    EntityId = invoice.SubscriptionInvoiceId,
                    BusinessId = invoice.BusinessId,
                    Details = "Invoice moved to Pending for retry.",
                    Reason = "Manual retry",
                    ActorUserId = TryGetCurrentUserId(),
                    ActorName = User.Identity?.Name ?? "SuperAdmin",
                    CreatedAt = PhilippineTime.Now
                });

                await _context.SaveChangesAsync();
                TempData["Success"] = "Invoice moved to Pending for retry.";
            }
            else
            {
                TempData["Error"] = "Only Failed or Overdue invoices can be retried.";
            }

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> ExportCsv(string? status, int? businessId, int? planId, DateTime? fromDate, DateTime? toDate, string? q)
        {
            var query = _context.SubscriptionInvoices
                .Include(i => i.Business)
                .Include(i => i.Plan)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "All", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(i => i.Status == status);
            }

            if (businessId.HasValue)
            {
                query = query.Where(i => i.BusinessId == businessId.Value);
            }

            if (planId.HasValue)
            {
                query = query.Where(i => i.PlanId == planId.Value);
            }

            if (fromDate.HasValue)
            {
                query = query.Where(i => i.IssuedAt >= fromDate.Value.Date);
            }

            if (toDate.HasValue)
            {
                var end = toDate.Value.Date.AddDays(1);
                query = query.Where(i => i.IssuedAt < end);
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                query = query.Where(i =>
                    i.Business.BusinessName.Contains(term) ||
                    i.Plan.PlanName.Contains(term) ||
                    (i.ExternalReference != null && i.ExternalReference.Contains(term)) ||
                    i.Status.Contains(term));
            }

            var rows = await query
                .OrderByDescending(i => i.IssuedAt)
                .ToListAsync();

            static string Esc(string value) => "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";

            var lines = new List<string>
            {
                "IssuedAtPH,Business,Plan,Amount,Status,PeriodStartPH,PeriodEndPH,PaymentMethod,ExternalReference"
            };

            foreach (var row in rows)
            {
                lines.Add(string.Join(",",
                    Esc(PhilippineTime.ToDateTime(row.IssuedAt).ToString("yyyy-MM-dd HH:mm:ss")),
                    Esc(row.Business.BusinessName),
                    Esc(row.Plan.PlanName),
                    Esc(row.Amount.ToString("0.00")),
                    Esc(row.Status),
                    Esc(PhilippineTime.ToDateTime(row.PeriodStart).ToString(DateFormat)),
                    Esc(PhilippineTime.ToDateTime(row.PeriodEnd).ToString(DateFormat)),
                    Esc(row.PaymentMethod),
                    Esc(row.ExternalReference ?? string.Empty)));
            }

            var csv = string.Join(Environment.NewLine, lines);
            var fileName = $"subscription-invoices-{PhilippineTime.Now:yyyyMMdd-HHmmss}.csv";
            return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", fileName);
        }

        private int? TryGetCurrentUserId()
        {
            var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(raw, out var id) ? id : null;
        }
    }
}
