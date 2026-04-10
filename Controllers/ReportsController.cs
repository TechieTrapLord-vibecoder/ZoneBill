using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using System.Globalization;
using System.Text;
using ZoneBill_Lloren.Data;
using ZoneBill_Lloren.Filters;
using ZoneBill_Lloren.Helpers;
using ZoneBill_Lloren.Models;

namespace ZoneBill_Lloren.Controllers
{
    [Authorize(Roles = "MainAdmin,Manager")]
    [ServiceFilter(typeof(ActiveSubscriptionFilter))]
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReportsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(DateTime? startDate, DateTime? endDate, int? cashierId, string? paymentMethod)
        {
            var businessId = GetBusinessId();
            if (businessId == null)
            {
                return Forbid();
            }

            var model = await BuildReportModelAsync(businessId.Value, startDate, endDate, cashierId, paymentMethod);
            return View(model);
        }

        public async Task<IActionResult> ExportCsv(DateTime? startDate, DateTime? endDate, int? cashierId, string? paymentMethod)
        {
            var businessId = GetBusinessId();
            if (businessId == null)
            {
                return Forbid();
            }

            var model = await BuildReportModelAsync(businessId.Value, startDate, endDate, cashierId, paymentMethod, topItemsTake: 200, shiftTake: 200);

            var sb = new StringBuilder();
            sb.AppendLine("Metric,Value");
            sb.AppendLine($"Date Range,{model.StartDate:yyyy-MM-dd} to {model.EndDate:yyyy-MM-dd}");
            sb.AppendLine($"Cashier,{ResolveFilterLabel(model.CashierOptions, model.SelectedCashierId?.ToString())}");
            sb.AppendLine($"Payment Method,{ResolveFilterLabel(model.PaymentMethodOptions, model.SelectedPaymentMethod)}");
            sb.AppendLine($"Total Orders,{model.TotalOrders}");
            sb.AppendLine($"Total Units Sold,{model.TotalUnitsSold}");
            sb.AppendLine($"Gross Sales,{model.TotalSales.ToString("0.00", CultureInfo.InvariantCulture)}");
            sb.AppendLine($"COGS,{model.TotalCostOfGoods.ToString("0.00", CultureInfo.InvariantCulture)}");
            sb.AppendLine($"Gross Profit,{model.GrossProfit.ToString("0.00", CultureInfo.InvariantCulture)}");
            sb.AppendLine($"Profit Margin %, {model.ProfitMarginPercent.ToString("0.00", CultureInfo.InvariantCulture)}");
            sb.AppendLine($"Closed Shifts,{model.ClosedShiftCount}");
            sb.AppendLine($"Total Shift Variance,{model.TotalShiftVariance.ToString("0.00", CultureInfo.InvariantCulture)}");
            sb.AppendLine($"POS Audit Events,{model.AuditEventCount}");
            sb.AppendLine($"Net Adjustments,{model.TotalAdjustments.ToString("0.00", CultureInfo.InvariantCulture)}");

            sb.AppendLine();
            sb.AppendLine("Daily Sales");
            sb.AppendLine("Date,Sales");
            for (var i = 0; i < model.DailyLabels.Count; i++)
            {
                sb.AppendLine($"{EscapeCsv(model.DailyLabels[i])},{model.DailySalesSeries[i].ToString("0.00", CultureInfo.InvariantCulture)}");
            }

            sb.AppendLine();
            sb.AppendLine("Top Items");
            sb.AppendLine("Item,Quantity,Revenue,COGS,Profit");
            foreach (var item in model.TopItems)
            {
                sb.AppendLine($"{EscapeCsv(item.ItemName)},{item.Quantity},{item.Revenue.ToString("0.00", CultureInfo.InvariantCulture)},{item.Cost.ToString("0.00", CultureInfo.InvariantCulture)},{item.Profit.ToString("0.00", CultureInfo.InvariantCulture)}");
            }

            sb.AppendLine();
            sb.AppendLine("Shift Variances");
            sb.AppendLine("Cashier,OpenedAt,ClosedAt,Expected,Actual,Variance");
            foreach (var shift in model.ShiftVariances)
            {
                sb.AppendLine($"{EscapeCsv(shift.CashierName)},{shift.OpenedAt:yyyy-MM-dd HH:mm},{shift.ClosedAt:yyyy-MM-dd HH:mm},{shift.ExpectedCash.ToString("0.00", CultureInfo.InvariantCulture)},{(shift.ActualCash ?? 0m).ToString("0.00", CultureInfo.InvariantCulture)},{(shift.Variance ?? 0m).ToString("0.00", CultureInfo.InvariantCulture)}");
            }

            sb.AppendLine();
            sb.AppendLine("POS Audit Trail");
            sb.AppendLine("When,Cashier,Action,BookingId,From Table,To Table,Split Count,Invoice IDs,Details");
            foreach (var audit in model.RecentPosAuditLogs)
            {
                sb.AppendLine($"{audit.CreatedAt:yyyy-MM-dd HH:mm},{EscapeCsv(audit.CashierName)},{EscapeCsv(audit.ActionType)},{audit.BookingId},{EscapeCsv(audit.SourceSpaceName ?? "-")},{EscapeCsv(audit.TargetSpaceName ?? "-")},{audit.SplitCount},{EscapeCsv(audit.InvoiceIds ?? "-")},{EscapeCsv(audit.Details ?? "-")}");
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            var fileName = $"report-{model.StartDate:yyyyMMdd}-{model.EndDate:yyyyMMdd}.csv";
            return File(bytes, "text/csv", fileName);
        }

        public async Task<IActionResult> ExportPdf(DateTime? startDate, DateTime? endDate, int? cashierId, string? paymentMethod)
        {
            var businessId = GetBusinessId();
            if (businessId == null)
            {
                return Forbid();
            }

            var model = await BuildReportModelAsync(businessId.Value, startDate, endDate, cashierId, paymentMethod);
            QuestPDF.Settings.License = LicenseType.Community;

            var pdfBytes = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(30);
                    page.Header().Column(column =>
                    {
                        column.Item().Text("ZoneBill Financial Report").SemiBold().FontSize(18);
                        column.Item().Text($"Range: {model.StartDate:yyyy-MM-dd} to {model.EndDate:yyyy-MM-dd}").FontSize(10);
                        column.Item().Text($"Cashier: {ResolveFilterLabel(model.CashierOptions, model.SelectedCashierId?.ToString())}").FontSize(10);
                        column.Item().Text($"Payment Method: {ResolveFilterLabel(model.PaymentMethodOptions, model.SelectedPaymentMethod)}").FontSize(10);
                    });

                    page.Content().PaddingVertical(8).Column(column =>
                    {
                        column.Spacing(5);
                        column.Item().Text($"Gross Sales: {model.TotalSales:C}");
                        column.Item().Text($"COGS: {model.TotalCostOfGoods:C}");
                        column.Item().Text($"Gross Profit: {model.GrossProfit:C} ({model.ProfitMarginPercent:N2}%)");
                        column.Item().Text($"Orders: {model.TotalOrders} | Units Sold: {model.TotalUnitsSold}");
                        column.Item().Text($"Closed Shifts: {model.ClosedShiftCount} | Shift Variance Total: {model.TotalShiftVariance:C}");
                        column.Item().Text($"POS Audit Events: {model.AuditEventCount}");
                        column.Item().Text($"Net Adjustments: {model.TotalAdjustments:C}");

                        column.Item().PaddingTop(8).Text("Top Items").SemiBold();
                        if (model.TopItems.Any())
                        {
                            foreach (var item in model.TopItems.Take(10))
                            {
                                column.Item().Text($"- {item.ItemName}: {item.Quantity} units, Revenue {item.Revenue:C}, Profit {item.Profit:C}").FontSize(10);
                            }
                        }
                        else
                        {
                            column.Item().Text("No item sales found for the selected filters.").FontSize(10);
                        }

                        column.Item().PaddingTop(8).Text("Recent Shift Variances").SemiBold();
                        if (model.ShiftVariances.Any())
                        {
                            foreach (var shift in model.ShiftVariances.Take(10))
                            {
                                column.Item().Text($"- {shift.CashierName}: {(shift.ClosedAt?.ToString("yyyy-MM-dd HH:mm") ?? "-")}, Variance {(shift.Variance ?? 0m):C}").FontSize(10);
                            }
                        }
                        else
                        {
                            column.Item().Text("No closed shifts found for the selected filters.").FontSize(10);
                        }

                        column.Item().PaddingTop(8).Text("Recent POS Audit Trail").SemiBold();
                        if (model.RecentPosAuditLogs.Any())
                        {
                            foreach (var audit in model.RecentPosAuditLogs.Take(10))
                            {
                                var bookingText = audit.BookingId.HasValue ? $"Booking #{audit.BookingId.Value}" : "No booking";
                                column.Item().Text($"- {audit.CreatedAt:yyyy-MM-dd HH:mm} | {audit.CashierName} | {audit.ActionType} | {bookingText}").FontSize(10);
                            }
                        }
                        else
                        {
                            column.Item().Text("No POS audit events found for the selected filters.").FontSize(10);
                        }
                    });

                    page.Footer().AlignRight().Text($"Generated {PhilippineTime.Now:yyyy-MM-dd HH:mm}").FontSize(9);
                });
            }).GeneratePdf();

            var fileName = $"report-{model.StartDate:yyyyMMdd}-{model.EndDate:yyyyMMdd}.pdf";
            return File(pdfBytes, "application/pdf", fileName);
        }

        private async Task<ReportsDashboardViewModel> BuildReportModelAsync(
            int businessId,
            DateTime? startDate,
            DateTime? endDate,
            int? cashierId,
            string? paymentMethod,
            int topItemsTake = 8,
            int shiftTake = 12)
        {
            var normalizedPaymentMethod = string.IsNullOrWhiteSpace(paymentMethod) || paymentMethod == "All"
                ? null
                : paymentMethod.Trim();

            var today = PhilippineTime.Now.Date;
            var rangeStart = startDate?.Date ?? today;
            var rangeEnd = endDate?.Date ?? today;
            if (rangeEnd < rangeStart)
            {
                rangeEnd = rangeStart;
            }

            var rangeEndExclusive = rangeEnd.AddDays(1);

            var cashiers = await _context.Users
                .Where(u => u.BusinessId == businessId && u.IsActive)
                .OrderBy(u => u.FirstName)
                .ThenBy(u => u.LastName)
                .Select(u => new ReportFilterOptionViewModel
                {
                    Value = u.UserId.ToString(),
                    Label = $"{u.FirstName} {u.LastName} ({u.EmailAddress})"
                })
                .ToListAsync();

            var paymentMethods = await _context.Payments
                .Where(p => p.BusinessId == businessId)
                .Select(p => p.PaymentMethod)
                .Distinct()
                .OrderBy(p => p)
                .Select(p => new ReportFilterOptionViewModel
                {
                    Value = p,
                    Label = p
                })
                .ToListAsync();

            var orderDetails = _context.OrderDetails
                .Where(od =>
                    od.Order.BusinessId == businessId &&
                    od.Order.OrderTime >= rangeStart &&
                    od.Order.OrderTime < rangeEndExclusive);

            if (cashierId.HasValue)
            {
                orderDetails = orderDetails.Where(od => od.Order.CashierId == cashierId.Value);
            }

            if (!string.IsNullOrWhiteSpace(normalizedPaymentMethod))
            {
                var bookingIdsWithMethod = _context.Invoices
                    .Where(i => i.BusinessId == businessId)
                    .Join(
                        _context.Payments.Where(p => p.BusinessId == businessId && p.PaymentMethod == normalizedPaymentMethod),
                        i => i.InvoiceId,
                        p => p.InvoiceId,
                        (i, p) => i.BookingId)
                    .Distinct();

                orderDetails = orderDetails.Where(od => bookingIdsWithMethod.Contains(od.Order.BookingId));
            }

            var orderRows = await orderDetails
                .Select(od => new
                {
                    OrderId = od.OrderId,
                    Day = od.Order.OrderTime.Date,
                    ItemName = od.MenuItem.ItemName,
                    Quantity = od.Quantity,
                    Revenue = od.LockedUnitPrice * od.Quantity,
                    Cost = od.MenuItem.CostPrice * od.Quantity
                })
                .ToListAsync();

            var totalSales = orderRows.Sum(x => x.Revenue);
            var totalCost = orderRows.Sum(x => x.Cost);
            var grossProfit = totalSales - totalCost;
            var totalUnits = orderRows.Sum(x => x.Quantity);
            var totalOrders = orderRows.Select(x => x.OrderId).Distinct().Count();
            var margin = totalSales <= 0m ? 0m : Math.Round((grossProfit / totalSales) * 100m, 2);

            var dailyMap = orderRows
                .GroupBy(x => x.Day)
                .ToDictionary(g => g.Key, g => Math.Round(g.Sum(x => x.Revenue), 2));

            var dailyLabels = new List<string>();
            var dailySeries = new List<decimal>();
            for (var day = rangeStart; day <= rangeEnd; day = day.AddDays(1))
            {
                dailyLabels.Add(day.ToString("MMM dd"));
                dailySeries.Add(dailyMap.TryGetValue(day, out var value) ? value : 0m);
            }

            var topItems = orderRows
                .GroupBy(x => x.ItemName)
                .Select(g => new ReportTopItemViewModel
                {
                    ItemName = g.Key,
                    Quantity = g.Sum(x => x.Quantity),
                    Revenue = Math.Round(g.Sum(x => x.Revenue), 2),
                    Cost = Math.Round(g.Sum(x => x.Cost), 2)
                })
                .OrderByDescending(x => x.Quantity)
                .ThenByDescending(x => x.Revenue)
                .Take(topItemsTake)
                .ToList();

            var shiftRows = await _context.PosShifts
                .Where(s =>
                    s.BusinessId == businessId &&
                    s.Status == "Closed" &&
                    s.ClosedAt != null &&
                    s.ClosedAt >= rangeStart &&
                    s.ClosedAt < rangeEndExclusive)
                .Where(s => !cashierId.HasValue || s.CashierId == cashierId.Value)
                .OrderByDescending(s => s.ClosedAt)
                .Select(s => new ReportShiftVarianceViewModel
                {
                    CashierName = s.Cashier.FirstName + " " + s.Cashier.LastName,
                    OpenedAt = s.OpenedAt,
                    ClosedAt = s.ClosedAt,
                    ExpectedCash = s.ExpectedCash,
                    ActualCash = s.ActualCash,
                    Variance = s.Variance
                })
                .Take(shiftTake)
                .ToListAsync();

            var totalShiftVariance = Math.Round(shiftRows.Sum(x => x.Variance ?? 0m), 2);

            var auditRows = await _context.PosAuditLogs
                .Where(a =>
                    a.BusinessId == businessId &&
                    a.CreatedAt >= rangeStart &&
                    a.CreatedAt < rangeEndExclusive)
                .Where(a => !cashierId.HasValue || a.CashierId == cashierId.Value)
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new ReportAuditLogViewModel
                {
                    CreatedAt = a.CreatedAt,
                    CashierName = a.Cashier.FirstName + " " + a.Cashier.LastName,
                    ActionType = a.ActionType,
                    BookingId = a.BookingId,
                    SourceSpaceName = a.SourceSpaceName,
                    TargetSpaceName = a.TargetSpaceName,
                    SplitCount = a.SplitCount,
                    InvoiceIds = a.InvoiceIds,
                    Details = a.Details
                })
                .Take(30)
                .ToListAsync();

            var auditCount = await _context.PosAuditLogs
                .Where(a =>
                    a.BusinessId == businessId &&
                    a.CreatedAt >= rangeStart &&
                    a.CreatedAt < rangeEndExclusive)
                .Where(a => !cashierId.HasValue || a.CashierId == cashierId.Value)
                .CountAsync();

            var adjustmentRows = await _context.Adjustments
                .Where(a => a.Invoice.BusinessId == businessId &&
                            a.Invoice.GeneratedDate >= rangeStart &&
                            a.Invoice.GeneratedDate < rangeEndExclusive)
                .ToListAsync();
            var totalAdjustments = adjustmentRows.Where(a => a.AdjustmentType == "Debit").Sum(a => a.Amount)
                                 - adjustmentRows.Where(a => a.AdjustmentType == "Credit").Sum(a => a.Amount);

            var model = new ReportsDashboardViewModel
            {
                StartDate = rangeStart,
                EndDate = rangeEnd,
                SelectedCashierId = cashierId,
                SelectedPaymentMethod = normalizedPaymentMethod,
                TotalOrders = totalOrders,
                TotalUnitsSold = totalUnits,
                TotalSales = Math.Round(totalSales, 2),
                TotalCostOfGoods = Math.Round(totalCost, 2),
                GrossProfit = Math.Round(grossProfit, 2),
                ProfitMarginPercent = margin,
                ClosedShiftCount = shiftRows.Count,
                OverShiftCount = shiftRows.Count(x => (x.Variance ?? 0m) > 0m),
                ShortShiftCount = shiftRows.Count(x => (x.Variance ?? 0m) < 0m),
                TotalShiftVariance = totalShiftVariance,
                AuditEventCount = auditCount,
                TotalAdjustments = Math.Round(totalAdjustments, 2),
                DailyLabels = dailyLabels,
                DailySalesSeries = dailySeries,
                CashierOptions = cashiers,
                PaymentMethodOptions = paymentMethods,
                TopItems = topItems,
                ShiftVariances = shiftRows,
                RecentPosAuditLogs = auditRows
            };

            return model;
        }

        private int? GetBusinessId()
        {
            var value = User.FindFirst("BusinessId")?.Value;
            return int.TryParse(value, out var businessId) ? businessId : null;
        }

        private static string ResolveFilterLabel(IEnumerable<ReportFilterOptionViewModel> options, string? selectedValue)
        {
            if (string.IsNullOrWhiteSpace(selectedValue))
            {
                return "All";
            }

            return options.FirstOrDefault(o => o.Value == selectedValue)?.Label ?? selectedValue;
        }

        private static string EscapeCsv(string value)
        {
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            {
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }

            return value;
        }
    }
}
