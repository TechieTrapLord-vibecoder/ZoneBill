using Microsoft.EntityFrameworkCore;
using ZoneBill_Lloren.Data;

namespace ZoneBill_Lloren.Helpers
{
    public class AutomationWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AutomationWorker> _logger;
        private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

        public AutomationWorker(IServiceScopeFactory scopeFactory, ILogger<AutomationWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("AutomationWorker started.");

            // Short initial delay to let the app finish startup
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunAllTasksAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "AutomationWorker: unhandled error in run loop.");
                }

                await Task.Delay(Interval, stoppingToken);
            }
        }

        private async Task RunAllTasksAsync(CancellationToken ct)
        {
            var now = PhilippineTime.Now;
            _logger.LogInformation("AutomationWorker tick at {Time}", now);

            await RunSafeAsync("SubscriptionAutoExpire", () => ExpireSubscriptionsAsync(ct));
            await RunSafeAsync("StaleShiftAlert", () => AlertStaleShiftsAsync(now, ct));
            await RunSafeAsync("LongRunningBookingLog", () => LogLongRunningBookingsAsync(now, ct));

            // Daily digest tasks — run only during the 8 AM hour (PH time)
            if (now.Hour == 8)
            {
                await RunSafeAsync("UnpaidInvoiceDigest", () => SendUnpaidInvoiceDigestAsync(ct));
                await RunSafeAsync("LowStockDigest", () => SendLowStockDigestAsync(ct));
            }
        }

        private async Task RunSafeAsync(string taskName, Func<Task> task)
        {
            try
            {
                await task();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AutomationWorker: {Task} failed.", taskName);
            }
        }

        // ── Task 1: Subscription Auto-Expire ─────────────────────────────────

        private async Task ExpireSubscriptionsAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var now = PhilippineTime.Now;
            var expired = await db.Businesses
                .Where(b => b.SubscriptionStatus == "Active" && b.CurrentPeriodEnd != null && b.CurrentPeriodEnd <= now)
                .ToListAsync(ct);

            if (!expired.Any()) return;

            foreach (var business in expired)
            {
                business.SubscriptionStatus = "Expired";
                _logger.LogInformation("AutomationWorker: Expired subscription for Business #{Id} ({Name}).",
                    business.BusinessId, business.BusinessName);
            }

            await db.SaveChangesAsync(ct);
        }

        // ── Task 2: Stale Shift Alert (>12h open) ────────────────────────────

        private async Task AlertStaleShiftsAsync(DateTime now, CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

            var cutoff = now.AddHours(-12);
            var staleShifts = await db.PosShifts
                .Include(s => s.Cashier)
                .Where(s => s.Status == "Open" && s.OpenedAt < cutoff)
                .ToListAsync(ct);

            foreach (var shift in staleShifts)
            {
                var admin = await db.Users.FirstOrDefaultAsync(
                    u => u.BusinessId == shift.BusinessId && u.UserRole == "MainAdmin" && u.IsActive, ct);
                if (admin == null) continue;

                var business = await db.Businesses.FindAsync(new object[] { shift.BusinessId }, ct);
                var cashierName = $"{shift.Cashier.FirstName} {shift.Cashier.LastName}";
                var businessName = business?.BusinessName ?? "Unknown";

                await emailService.SendStaleShiftAlertAsync(
                    admin.EmailAddress,
                    $"{admin.FirstName} {admin.LastName}",
                    cashierName,
                    shift.OpenedAt,
                    businessName);

                _logger.LogWarning("AutomationWorker: Stale shift #{ShiftId} ({Cashier}) open since {OpenedAt} at {Business}.",
                    shift.ShiftId, cashierName, shift.OpenedAt, businessName);
            }
        }

        // ── Task 3: Long-Running Booking Log (>8h, log only) ─────────────────

        private async Task LogLongRunningBookingsAsync(DateTime now, CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var cutoff = now.AddHours(-8);
            var staleBookings = await db.Bookings
                .Include(b => b.Space)
                .Where(b => b.BookingStatus == "Occupied" && b.EndTime == null && b.StartTime < cutoff)
                .ToListAsync(ct);

            foreach (var booking in staleBookings)
            {
                var hours = (int)(now - booking.StartTime).TotalHours;
                _logger.LogWarning("AutomationWorker: Booking #{BookingId} on {Space} has been occupied for {Hours}h (since {Start}).",
                    booking.BookingId, booking.Space?.SpaceName ?? "?", hours, booking.StartTime);
            }
        }

        // ── Task 4: Daily Unpaid Invoice Digest (8 AM) ───────────────────────

        private async Task SendUnpaidInvoiceDigestAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

            var businessIds = await db.Invoices
                .Where(i => i.PaymentStatus == "Unpaid")
                .Select(i => i.BusinessId)
                .Distinct()
                .ToListAsync(ct);

            foreach (var bid in businessIds)
            {
                var count = await db.Invoices.CountAsync(i => i.BusinessId == bid && i.PaymentStatus == "Unpaid", ct);
                if (count == 0) continue;

                var admin = await db.Users.FirstOrDefaultAsync(
                    u => u.BusinessId == bid && u.UserRole == "MainAdmin" && u.IsActive, ct);
                if (admin == null) continue;

                var business = await db.Businesses.FindAsync(new object[] { bid }, ct);
                var businessName = business?.BusinessName ?? "Unknown";

                await emailService.SendUnpaidInvoiceSummaryAsync(
                    admin.EmailAddress,
                    $"{admin.FirstName} {admin.LastName}",
                    count,
                    businessName);

                _logger.LogInformation("AutomationWorker: Sent unpaid invoice digest ({Count}) to {Email} for {Business}.",
                    count, admin.EmailAddress, businessName);
            }
        }

        // ── Task 5: Daily Low-Stock Digest (8 AM) ────────────────────────────

        private async Task SendLowStockDigestAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

            var lowStockByBusiness = await db.MenuItems
                .Where(m => m.IsActive && m.StockAvailable <= m.LowStockThreshold)
                .GroupBy(m => m.BusinessId)
                .Select(g => new { BusinessId = g.Key, Items = g.Select(m => m.ItemName).ToList() })
                .ToListAsync(ct);

            foreach (var group in lowStockByBusiness)
            {
                var admin = await db.Users.FirstOrDefaultAsync(
                    u => u.BusinessId == group.BusinessId && u.UserRole == "MainAdmin" && u.IsActive, ct);
                if (admin == null) continue;

                var business = await db.Businesses.FindAsync(new object[] { group.BusinessId }, ct);
                var businessName = business?.BusinessName ?? "Unknown";

                await emailService.SendLowStockDigestAsync(
                    admin.EmailAddress,
                    $"{admin.FirstName} {admin.LastName}",
                    group.Items,
                    businessName);

                _logger.LogInformation("AutomationWorker: Sent low-stock digest ({Count} items) to {Email} for {Business}.",
                    group.Items.Count, admin.EmailAddress, businessName);
            }
        }
    }
}
