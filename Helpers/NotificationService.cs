using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ZoneBill_Lloren.Data;
using ZoneBill_Lloren.Models;

namespace ZoneBill_Lloren.Helpers
{
    public interface INotificationService
    {
        Task<NotificationSummaryViewModel> GetBusinessNotificationsAsync(ClaimsPrincipal user);
    }

    public class NotificationService : INotificationService
    {
        private const string SeverityDanger = "danger";
        private const string SeverityWarning = "warning";
        private const string SeverityInfo = "info";
        private const string SeveritySuccess = "success";

        private readonly ApplicationDbContext _context;

        public NotificationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<NotificationSummaryViewModel> GetBusinessNotificationsAsync(ClaimsPrincipal user)
        {
            var businessId = GetBusinessId(user);
            if (businessId == null)
            {
                return new NotificationSummaryViewModel();
            }

            var isMainAdmin = user.IsInRole("MainAdmin");
            var isManager = user.IsInRole("Manager");
            var userId = GetUserId(user);
            var now = PhilippineTime.Now;
            var recentActivityCutoff = now.AddHours(-2);
            var staleShiftCutoff = now.AddHours(-12);

            var summary = new NotificationSummaryViewModel();

            AddIfNotNull(summary, await GetCheckoutRequestNotificationAsync(businessId.Value));
            AddIfNotNull(summary, await GetRecentBookingsNotificationAsync(businessId.Value, recentActivityCutoff, now));
            AddIfNotNull(summary, await GetUnpaidInvoiceNotificationAsync(businessId.Value));
            AddIfNotNull(summary, await GetRecentPaymentsNotificationAsync(businessId.Value, recentActivityCutoff, now));

            if (isMainAdmin || isManager)
            {
                AddIfNotNull(summary, await GetLowStockNotificationAsync(businessId.Value));
            }

            AddIfNotNull(summary, await GetStaleShiftNotificationAsync(
                businessId.Value,
                isMainAdmin,
                isManager,
                userId,
                staleShiftCutoff));

            summary.Count = summary.Items.Sum(item => item.Count);
            summary.Items = summary.Items
                .OrderBy(item => GetSeverityRank(item.Severity))
                .ThenByDescending(item => item.Count)
                .ToList();

            return summary;
        }

        private static int? GetBusinessId(ClaimsPrincipal user)
        {
            var value = user.FindFirstValue("BusinessId");
            return int.TryParse(value, out var businessId) ? businessId : null;
        }

        private static int? GetUserId(ClaimsPrincipal user)
        {
            var value = user.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(value, out var userId) ? userId : null;
        }

        private static void AddIfNotNull(NotificationSummaryViewModel summary, NotificationItemViewModel? item)
        {
            if (item != null)
            {
                summary.Items.Add(item);
            }
        }

        private async Task<NotificationItemViewModel?> GetCheckoutRequestNotificationAsync(int businessId)
        {
            var checkoutRequests = await _context.Bookings
                .CountAsync(b =>
                    b.BusinessId == businessId &&
                    b.BookingStatus == "Active" &&
                    b.CheckoutRequested);

            if (checkoutRequests == 0)
            {
                return null;
            }

            return new NotificationItemViewModel
            {
                Key = "checkout-requests",
                Title = "Checkout Requests",
                Message = checkoutRequests == 1
                    ? "1 active table is asking to checkout."
                    : $"{checkoutRequests} active tables are asking to checkout.",
                Severity = SeverityWarning,
                Icon = "bi-bell-fill",
                Link = "/POS/Tables",
                Count = checkoutRequests
            };
        }

        private async Task<NotificationItemViewModel?> GetRecentBookingsNotificationAsync(int businessId, DateTime recentActivityCutoff, DateTime now)
        {
            var recentBookings = await _context.Bookings
                .CountAsync(b =>
                    b.BusinessId == businessId &&
                    b.BookingStatus == "Active" &&
                    b.StartTime >= recentActivityCutoff &&
                    b.StartTime <= now);

            if (recentBookings == 0)
            {
                return null;
            }

            return new NotificationItemViewModel
            {
                Key = "recent-bookings",
                Title = "Recent Bookings",
                Message = recentBookings == 1
                    ? "1 table session started in the last 2 hours."
                    : $"{recentBookings} table sessions started in the last 2 hours.",
                Severity = SeverityInfo,
                Icon = "bi-calendar-check",
                Link = "/Bookings",
                Count = recentBookings
            };
        }

        private async Task<NotificationItemViewModel?> GetUnpaidInvoiceNotificationAsync(int businessId)
        {
            var unpaidInvoices = await _context.Invoices
                .CountAsync(i =>
                    i.BusinessId == businessId &&
                    i.PaymentStatus == "Unpaid");

            if (unpaidInvoices == 0)
            {
                return null;
            }

            return new NotificationItemViewModel
            {
                Key = "unpaid-invoices",
                Title = "Unpaid Invoices",
                Message = unpaidInvoices == 1
                    ? "1 invoice is still unpaid."
                    : $"{unpaidInvoices} invoices are still unpaid.",
                Severity = SeverityDanger,
                Icon = "bi-receipt-cutoff",
                Link = "/Invoices",
                Count = unpaidInvoices
            };
        }

        private async Task<NotificationItemViewModel?> GetRecentPaymentsNotificationAsync(int businessId, DateTime recentActivityCutoff, DateTime now)
        {
            var recentPayments = await _context.Payments
                .CountAsync(p =>
                    p.BusinessId == businessId &&
                    p.PaymentDate >= recentActivityCutoff &&
                    p.PaymentDate <= now);

            if (recentPayments == 0)
            {
                return null;
            }

            return new NotificationItemViewModel
            {
                Key = "recent-payments",
                Title = "Recent Payments",
                Message = recentPayments == 1
                    ? "1 payment was recorded in the last 2 hours."
                    : $"{recentPayments} payments were recorded in the last 2 hours.",
                Severity = SeveritySuccess,
                Icon = "bi-cash-coin",
                Link = "/Payments",
                Count = recentPayments
            };
        }

        private async Task<NotificationItemViewModel?> GetLowStockNotificationAsync(int businessId)
        {
            var lowStockItems = await _context.MenuItems
                .CountAsync(m =>
                    m.BusinessId == businessId &&
                    m.IsActive &&
                    m.StockAvailable <= m.LowStockThreshold);

            if (lowStockItems == 0)
            {
                return null;
            }

            return new NotificationItemViewModel
            {
                Key = "low-stock",
                Title = "Low Stock",
                Message = lowStockItems == 1
                    ? "1 menu item is at or below its stock threshold."
                    : $"{lowStockItems} menu items are at or below their stock threshold.",
                Severity = SeverityWarning,
                Icon = "bi-box-seam",
                Link = "/Inventory",
                Count = lowStockItems
            };
        }

        private async Task<NotificationItemViewModel?> GetStaleShiftNotificationAsync(
            int businessId,
            bool isMainAdmin,
            bool isManager,
            int? userId,
            DateTime staleShiftCutoff)
        {
            var staleShiftQuery = _context.PosShifts
                .Where(s =>
                    s.BusinessId == businessId &&
                    s.Status == "Open" &&
                    s.OpenedAt < staleShiftCutoff);

            if (!isMainAdmin && !isManager && userId.HasValue)
            {
                staleShiftQuery = staleShiftQuery.Where(s => s.CashierId == userId.Value);
            }

            var staleShifts = await staleShiftQuery.CountAsync();

            if (staleShifts == 0)
            {
                return null;
            }

            return new NotificationItemViewModel
            {
                Key = "stale-shifts",
                Title = "Shift Issues",
                Message = staleShifts == 1
                    ? "1 open shift has been running for more than 12 hours."
                    : $"{staleShifts} open shifts have been running for more than 12 hours.",
                Severity = SeverityWarning,
                Icon = "bi-clock-history",
                Link = "/Shifts",
                Count = staleShifts
            };
        }

        private static int GetSeverityRank(string? severity)
        {
            return (severity ?? "info").ToLowerInvariant() switch
            {
                "danger" => 0,
                "error" => 0,
                "warning" => 1,
                "info" => 2,
                "success" => 3,
                _ => 4
            };
        }
    }
}