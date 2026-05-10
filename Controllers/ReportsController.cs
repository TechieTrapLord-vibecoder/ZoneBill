using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
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
            sb.AppendLine($"Occupancy %, {model.OccupancyRatePercent.ToString("0.00", CultureInfo.InvariantCulture)}");
            sb.AppendLine($"Spaces Used,{model.SpacesUsedCount} of {model.ActiveSpaceCount}");

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
            sb.AppendLine("Space Utilization");
            sb.AppendLine("Space,Floor Area,Bookings,Hours Booked,Revenue,Utilization %");
            foreach (var space in model.SpaceUtilization)
            {
                sb.AppendLine($"{EscapeCsv(space.SpaceName)},{EscapeCsv(space.FloorArea)},{space.BookingCount},{space.HoursBooked.ToString("0.00", CultureInfo.InvariantCulture)},{space.Revenue.ToString("0.00", CultureInfo.InvariantCulture)},{space.UtilizationPercent.ToString("0.00", CultureInfo.InvariantCulture)}");
            }

            sb.AppendLine();
            sb.AppendLine("Staff Performance");
            sb.AppendLine("Cashier,Orders,Units Sold,Sales,Gross Profit,Average Ticket,Audit Events,Shift Variance");
            foreach (var staff in model.StaffPerformance)
            {
                sb.AppendLine($"{EscapeCsv(staff.CashierName)},{staff.Orders},{staff.UnitsSold},{staff.Sales.ToString("0.00", CultureInfo.InvariantCulture)},{staff.GrossProfit.ToString("0.00", CultureInfo.InvariantCulture)},{staff.AverageTicket.ToString("0.00", CultureInfo.InvariantCulture)},{staff.AuditEvents},{staff.ShiftVariance.ToString("0.00", CultureInfo.InvariantCulture)}");
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
            if (businessId == null) return Forbid();

            var model = await BuildReportModelAsync(businessId.Value, startDate, endDate, cashierId, paymentMethod, topItemsTake: 20, shiftTake: 20);
            QuestPDF.Settings.License = LicenseType.Community;

            // ── Brand palette (light PDF, brand colours as accents) ──────────
            var cNavy   = Color.FromHex("0F172A");
            var cCyan   = Color.FromHex("06B6D4");
            var cOrange = Color.FromHex("F97316");
            var cGreen  = Color.FromHex("16A34A");
            var cRed    = Color.FromHex("DC2626");
            var cSlate  = Color.FromHex("1E293B");
            var cMuted  = Color.FromHex("64748B");
            var cRowAlt = Color.FromHex("F1F5F9");
            var cBorder = Color.FromHex("E2E8F0");
            var cHdrBg  = Color.FromHex("0E2240");
            var cSubtle = Color.FromHex("94A3B8");

            // ── Local helpers ────────────────────────────────────────────────
            string Ph(decimal v) => $"\u20B1{v:N2}";
            string PhSigned(decimal v) => v < 0 ? $"-\u20B1{Math.Abs(v):N2}" : $"\u20B1{v:N2}";
            Color ProfitClr(decimal v) => v >= 0 ? cGreen : cRed;

            IContainer HeaderCell(IContainer c) =>
                c.Background(cHdrBg)
                 .PaddingHorizontal(7).PaddingVertical(5);

            IContainer DataCell(IContainer c, bool even) =>
                c.Background(even ? cRowAlt : Colors.White)
                 .BorderBottom(0.5f).BorderColor(cBorder)
                 .PaddingHorizontal(7).PaddingVertical(4);

            void KpiBox(IContainer container, string label, string value, Color accent)
            {
                container
                    .Border(1).BorderColor(cBorder)
                    .Background(Colors.White)
                    .Padding(10)
                    .Column(col =>
                    {
                        col.Item().Text(t => t.Span(label).FontColor(cMuted).FontSize(7.5f));
                        col.Item().PaddingTop(4).Text(t => t.Span(value).FontColor(accent).SemiBold().FontSize(12));
                    });
            }

            void SectionTitle(ColumnDescriptor col, string title, Color accent)
            {
                col.Item().PaddingTop(14).Row(row =>
                {
                    row.ConstantItem(4).Background(accent);
                    row.RelativeItem().Background(cNavy)
                       .PaddingHorizontal(10).PaddingVertical(6)
                       .Text(t => t.Span(title).FontColor(Colors.White).SemiBold().FontSize(9f));
                });
            }

            var cashierLabel = ResolveFilterLabel(model.CashierOptions, model.SelectedCashierId?.ToString());
            var methodLabel  = ResolveFilterLabel(model.PaymentMethodOptions, model.SelectedPaymentMethod);

            var pdfBytes = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(36);
                    page.Background(Colors.White);

                    // ── HEADER ─────────────────────────────────────────────
                    page.Header().Column(col =>
                    {
                        col.Item().Background(cNavy).Padding(16).Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text(t =>
                                {
                                    t.Span("ZoneBill ").FontColor(Colors.White).Bold().FontSize(20);
                                    t.Span("Financial Report").FontColor(cCyan).Bold().FontSize(20);
                                });
                                c.Item().PaddingTop(5).Text(t =>
                                {
                                    t.Span($"{model.StartDate:MMM dd, yyyy}  –  {model.EndDate:MMM dd, yyyy}").FontColor(cSubtle).FontSize(8.5f);
                                    t.Span("    |    ").FontColor(Color.FromHex("334155")).FontSize(8.5f);
                                    t.Span($"Cashier: {cashierLabel}").FontColor(cSubtle).FontSize(8.5f);
                                    t.Span("    |    ").FontColor(Color.FromHex("334155")).FontSize(8.5f);
                                    t.Span($"Payment: {methodLabel}").FontColor(cSubtle).FontSize(8.5f);
                                });
                            });
                            row.ConstantItem(120).AlignRight().AlignMiddle().Column(c =>
                            {
                                c.Item().AlignRight().Text(t =>
                                {
                                    t.Span("Generated\n").FontColor(cMuted).FontSize(7.5f);
                                    t.Span(PhilippineTime.Now.ToString("MMM dd, yyyy  HH:mm PHT")).FontColor(cCyan).FontSize(7.5f);
                                });
                            });
                        });
                        col.Item().Height(3).Background(cCyan);
                    });

                    // ── CONTENT ────────────────────────────────────────────
                    page.Content().PaddingTop(12).Column(col =>
                    {
                        // KPI Row 1 – sales
                        col.Item().PaddingBottom(4).Text(t =>
                            t.Span("KEY PERFORMANCE INDICATORS").FontColor(cMuted).FontSize(7.5f));

                        col.Item().Row(row =>
                        {
                            KpiBox(row.RelativeItem().Padding(3), "Total Orders",    model.TotalOrders.ToString(),                   cCyan);
                            KpiBox(row.RelativeItem().Padding(3), "Units Sold",       model.TotalUnitsSold.ToString(),                cCyan);
                            KpiBox(row.RelativeItem().Padding(3), "Gross Sales",      Ph(model.TotalSales),                          cCyan);
                            KpiBox(row.RelativeItem().Padding(3), "COGS",             Ph(model.TotalCostOfGoods),                    cOrange);
                            KpiBox(row.RelativeItem().Padding(3), "Gross Profit",     Ph(model.GrossProfit),                         ProfitClr(model.GrossProfit));
                            KpiBox(row.RelativeItem().Padding(3), "Profit Margin",    $"{model.ProfitMarginPercent:N1}%",             ProfitClr(model.GrossProfit));
                        });

                        // KPI Row 2 – operations
                        col.Item().PaddingTop(2).Row(row =>
                        {
                            KpiBox(row.RelativeItem().Padding(3), "Closed Shifts",    model.ClosedShiftCount.ToString(),             cMuted);
                            KpiBox(row.RelativeItem().Padding(3), "Shift Variance",   PhSigned(model.TotalShiftVariance),            ProfitClr(model.TotalShiftVariance));
                            KpiBox(row.RelativeItem().Padding(3), "POS Audit Events", model.AuditEventCount.ToString(),              cMuted);
                            KpiBox(row.RelativeItem().Padding(3), "Net Adjustments",  PhSigned(model.TotalAdjustments),              ProfitClr(model.TotalAdjustments));
                            KpiBox(row.RelativeItem().Padding(3), "Spaces Used",      $"{model.SpacesUsedCount}/{model.ActiveSpaceCount}", cMuted);
                            KpiBox(row.RelativeItem().Padding(3), "Occupancy",        $"{model.OccupancyRatePercent:N1}%",           cCyan);
                        });

                        // ── TOP ITEMS ──────────────────────────────────────
                        SectionTitle(col, "TOP ITEM FINANCIALS", cCyan);
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(4);
                                c.RelativeColumn(1.5f);
                                c.RelativeColumn(2);
                                c.RelativeColumn(2);
                                c.RelativeColumn(2);
                            });
                            table.Header(h =>
                            {
                                foreach (var lbl in new[] { "Item", "Qty", "Revenue", "COGS", "Profit" })
                                    h.Cell().Element(HeaderCell).Text(t => t.Span(lbl).FontColor(Colors.White).SemiBold().FontSize(8));
                            });
                            if (model.TopItems.Any())
                            {
                                int idx = 0;
                                foreach (var item in model.TopItems.Take(20))
                                {
                                    bool even = idx++ % 2 == 0;
                                    table.Cell().Element(c => DataCell(c, even)).Text(t => t.Span(item.ItemName).FontColor(cSlate).FontSize(8));
                                    table.Cell().Element(c => DataCell(c, even)).AlignRight().Text(t => t.Span(item.Quantity.ToString()).FontColor(cSlate).FontSize(8));
                                    table.Cell().Element(c => DataCell(c, even)).AlignRight().Text(t => t.Span(Ph(item.Revenue)).FontColor(cCyan).FontSize(8));
                                    table.Cell().Element(c => DataCell(c, even)).AlignRight().Text(t => t.Span(Ph(item.Cost)).FontColor(cOrange).FontSize(8));
                                    table.Cell().Element(c => DataCell(c, even)).AlignRight().Text(t => t.Span(PhSigned(item.Profit)).FontColor(ProfitClr(item.Profit)).FontSize(8));
                                }
                            }
                            else
                                table.Cell().ColumnSpan(5).Background(Color.FromHex("F8FAFC")).Padding(10)
                                    .Text(t => t.Span("No item sales in the selected range.").FontColor(cMuted).FontSize(8).Italic());
                        });

                        // ── STAFF PERFORMANCE ─────────────────────────────
                        SectionTitle(col, "STAFF PERFORMANCE", cOrange);
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(3);
                                c.RelativeColumn(1.2f);
                                c.RelativeColumn(2);
                                c.RelativeColumn(2);
                                c.RelativeColumn(2);
                                c.RelativeColumn(1.2f);
                            });
                            table.Header(h =>
                            {
                                foreach (var lbl in new[] { "Cashier", "Orders", "Sales", "Profit", "Avg Ticket", "Audits" })
                                    h.Cell().Element(HeaderCell).Text(t => t.Span(lbl).FontColor(Colors.White).SemiBold().FontSize(8));
                            });
                            if (model.StaffPerformance.Any())
                            {
                                int idx = 0;
                                foreach (var s in model.StaffPerformance.Take(15))
                                {
                                    bool even = idx++ % 2 == 0;
                                    table.Cell().Element(c => DataCell(c, even)).Text(t => t.Span(s.CashierName).FontColor(cSlate).FontSize(8));
                                    table.Cell().Element(c => DataCell(c, even)).AlignCenter().Text(t => t.Span(s.Orders.ToString()).FontColor(cSlate).FontSize(8));
                                    table.Cell().Element(c => DataCell(c, even)).AlignRight().Text(t => t.Span(Ph(s.Sales)).FontColor(cCyan).FontSize(8));
                                    table.Cell().Element(c => DataCell(c, even)).AlignRight().Text(t => t.Span(PhSigned(s.GrossProfit)).FontColor(ProfitClr(s.GrossProfit)).FontSize(8));
                                    table.Cell().Element(c => DataCell(c, even)).AlignRight().Text(t => t.Span(Ph(s.AverageTicket)).FontColor(cSlate).FontSize(8));
                                    table.Cell().Element(c => DataCell(c, even)).AlignCenter().Text(t => t.Span(s.AuditEvents.ToString()).FontColor(cSlate).FontSize(8));
                                }
                            }
                            else
                                table.Cell().ColumnSpan(6).Background(Color.FromHex("F8FAFC")).Padding(10)
                                    .Text(t => t.Span("No staff activity in the selected range.").FontColor(cMuted).FontSize(8).Italic());
                        });

                        // ── SPACE UTILIZATION ─────────────────────────────
                        SectionTitle(col, "SPACE UTILIZATION", cCyan);
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(3);
                                c.RelativeColumn(2);
                                c.RelativeColumn(1.5f);
                                c.RelativeColumn(1.5f);
                                c.RelativeColumn(2);
                                c.RelativeColumn(2);
                            });
                            table.Header(h =>
                            {
                                foreach (var lbl in new[] { "Space", "Floor", "Bookings", "Hours", "Revenue", "Utilization" })
                                    h.Cell().Element(HeaderCell).Text(t => t.Span(lbl).FontColor(Colors.White).SemiBold().FontSize(8));
                            });
                            if (model.SpaceUtilization.Any())
                            {
                                int idx = 0;
                                foreach (var sp in model.SpaceUtilization.Take(15))
                                {
                                    bool even = idx++ % 2 == 0;
                                    var utilClr = sp.UtilizationPercent >= 75 ? cGreen : sp.UtilizationPercent >= 40 ? cOrange : cRed;
                                    table.Cell().Element(c => DataCell(c, even)).Text(t => t.Span(sp.SpaceName).FontColor(cSlate).FontSize(8));
                                    table.Cell().Element(c => DataCell(c, even)).Text(t => t.Span(sp.FloorArea).FontColor(cMuted).FontSize(8));
                                    table.Cell().Element(c => DataCell(c, even)).AlignCenter().Text(t => t.Span(sp.BookingCount.ToString()).FontColor(cSlate).FontSize(8));
                                    table.Cell().Element(c => DataCell(c, even)).AlignRight().Text(t => t.Span($"{sp.HoursBooked:N1} hrs").FontColor(cSlate).FontSize(8));
                                    table.Cell().Element(c => DataCell(c, even)).AlignRight().Text(t => t.Span(Ph(sp.Revenue)).FontColor(cCyan).FontSize(8));
                                    table.Cell().Element(c => DataCell(c, even)).AlignRight().Text(t => t.Span($"{sp.UtilizationPercent:N1}%").FontColor(utilClr).FontSize(8));
                                }
                            }
                            else
                                table.Cell().ColumnSpan(6).Background(Color.FromHex("F8FAFC")).Padding(10)
                                    .Text(t => t.Span("No space occupancy in the selected range.").FontColor(cMuted).FontSize(8).Italic());
                        });

                        // ── SHIFT VARIANCES ────────────────────────────────
                        SectionTitle(col, "RECENT SHIFT CLOSURES — VARIANCE", cOrange);
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(3);
                                c.RelativeColumn(2.5f);
                                c.RelativeColumn(2);
                                c.RelativeColumn(2);
                                c.RelativeColumn(2);
                            });
                            table.Header(h =>
                            {
                                foreach (var lbl in new[] { "Cashier", "Closed At", "Expected", "Actual", "Variance" })
                                    h.Cell().Element(HeaderCell).Text(t => t.Span(lbl).FontColor(Colors.White).SemiBold().FontSize(8));
                            });
                            if (model.ShiftVariances.Any())
                            {
                                int idx = 0;
                                foreach (var sv in model.ShiftVariances.Take(15))
                                {
                                    bool even = idx++ % 2 == 0;
                                    var variance = sv.Variance ?? 0m;
                                    table.Cell().Element(c => DataCell(c, even)).Text(t => t.Span(sv.CashierName).FontColor(cSlate).FontSize(8));
                                    table.Cell().Element(c => DataCell(c, even)).Text(t => t.Span(sv.ClosedAt?.ToString("MMM dd, HH:mm") ?? "–").FontColor(cMuted).FontSize(8));
                                    table.Cell().Element(c => DataCell(c, even)).AlignRight().Text(t => t.Span(Ph(sv.ExpectedCash)).FontColor(cSlate).FontSize(8));
                                    table.Cell().Element(c => DataCell(c, even)).AlignRight().Text(t => t.Span(Ph(sv.ActualCash ?? 0m)).FontColor(cSlate).FontSize(8));
                                    table.Cell().Element(c => DataCell(c, even)).AlignRight().Text(t => t.Span(PhSigned(variance)).FontColor(ProfitClr(variance)).FontSize(8));
                                }
                            }
                            else
                                table.Cell().ColumnSpan(5).Background(Color.FromHex("F8FAFC")).Padding(10)
                                    .Text(t => t.Span("No closed shifts in the selected range.").FontColor(cMuted).FontSize(8).Italic());
                        });
                    });

                    // ── FOOTER ─────────────────────────────────────────────
                    page.Footer().PaddingTop(8).BorderTop(0.5f).BorderColor(cBorder).Row(row =>
                    {
                        row.RelativeItem().Text(t =>
                        {
                            t.Span("ZoneBill Financial Report  ·  ").FontColor(cMuted).FontSize(7.5f);
                            t.Span($"Generated {PhilippineTime.Now:MMM dd, yyyy  HH:mm} PHT").FontColor(cMuted).FontSize(7.5f);
                        });
                        row.ConstantItem(70).AlignRight().Text(t =>
                        {
                            t.Span("Page ").FontColor(cMuted).FontSize(7.5f);
                            t.CurrentPageNumber().FontColor(cCyan).Bold().FontSize(7.5f);
                            t.Span(" / ").FontColor(cMuted).FontSize(7.5f);
                            t.TotalPages().FontColor(cCyan).Bold().FontSize(7.5f);
                        });
                    });
                });
            }).GeneratePdf();

            var fileName = $"ZoneBill-Report-{model.StartDate:yyyyMMdd}-{model.EndDate:yyyyMMdd}.pdf";
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
            var totalRangeHours = Math.Max(1m, (decimal)(rangeEndExclusive - rangeStart).TotalHours);

            var cashierUsers = await _context.Users
                .Where(u => u.BusinessId == businessId && u.IsActive)
                .OrderBy(u => u.FirstName)
                .ThenBy(u => u.LastName)
                .Select(u => new
                {
                    u.UserId,
                    Name = u.FirstName + " " + u.LastName,
                    Label = $"{u.FirstName} {u.LastName} ({u.EmailAddress})"
                })
                .ToListAsync();

            var cashiers = cashierUsers
                .Select(u => new ReportFilterOptionViewModel
                {
                    Value = u.UserId.ToString(),
                    Label = u.Label
                })
                .ToList();

            var cashierNameById = cashierUsers.ToDictionary(u => u.UserId, u => u.Name);

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
                    CashierId = od.Order.CashierId,
                    CashierName = od.Order.Cashier.FirstName + " " + od.Order.Cashier.LastName,
                    Day = od.Order.OrderTime.Date,
                    ItemName = od.MenuItem.ItemName,
                    Category = od.MenuItem.Category,
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

            var categoryBreakdown = orderRows
                .GroupBy(x => x.Category)
                .Select(g => new ReportCategoryBreakdownViewModel
                {
                    Category = g.Key,
                    Revenue = Math.Round(g.Sum(x => x.Revenue), 2),
                    UnitsSold = g.Sum(x => x.Quantity)
                })
                .OrderByDescending(x => x.Revenue)
                .ToList();

            var activeSpaceCount = await _context.Spaces
                .Where(s => s.BusinessId == businessId && s.IsActive)
                .CountAsync();

            var spaceBookings = await _context.Bookings
                .Where(b =>
                    b.BusinessId == businessId &&
                    b.StartTime < rangeEndExclusive &&
                    (b.EndTime ?? rangeEndExclusive) > rangeStart)
                .Select(b => new
                {
                    b.SpaceId,
                    b.Space.SpaceName,
                    b.Space.FloorArea,
                    b.StartTime,
                    b.EndTime,
                    b.LockedHourlyRate
                })
                .ToListAsync();

            var spaceUtilizationAll = spaceBookings
                .Select(b =>
                {
                    var effectiveStart = b.StartTime < rangeStart ? rangeStart : b.StartTime;
                    var bookingEnd = b.EndTime ?? rangeEndExclusive;
                    var effectiveEnd = bookingEnd > rangeEndExclusive ? rangeEndExclusive : bookingEnd;
                    var bookedHours = (decimal)Math.Max(0d, (effectiveEnd - effectiveStart).TotalHours);

                    return new ReportSpaceUtilizationViewModel
                    {
                        SpaceName = b.SpaceName,
                        FloorArea = b.FloorArea,
                        BookingCount = bookedHours > 0m ? 1 : 0,
                        HoursBooked = Math.Round(bookedHours, 2),
                        Revenue = Math.Round(bookedHours * b.LockedHourlyRate, 2),
                        UtilizationPercent = 0m
                    };
                })
                .Where(x => x.HoursBooked > 0m)
                .GroupBy(x => new { x.SpaceName, x.FloorArea })
                .Select(g => new ReportSpaceUtilizationViewModel
                {
                    SpaceName = g.Key.SpaceName,
                    FloorArea = g.Key.FloorArea,
                    BookingCount = g.Sum(x => x.BookingCount),
                    HoursBooked = Math.Round(g.Sum(x => x.HoursBooked), 2),
                    Revenue = Math.Round(g.Sum(x => x.Revenue), 2),
                    UtilizationPercent = Math.Round((g.Sum(x => x.HoursBooked) / totalRangeHours) * 100m, 2)
                })
                .OrderByDescending(x => x.UtilizationPercent)
                .ThenByDescending(x => x.Revenue)
                .ToList();

            var occupancyRate = activeSpaceCount <= 0
                ? 0m
                : Math.Round((spaceUtilizationAll.Sum(x => x.HoursBooked) / (totalRangeHours * activeSpaceCount)) * 100m, 2);

            var spaceUtilization = spaceUtilizationAll
                .Take(10)
                .ToList();

            var shiftSummaryRows = await _context.PosShifts
                .Where(s =>
                    s.BusinessId == businessId &&
                    s.Status == "Closed" &&
                    s.ClosedAt != null &&
                    s.ClosedAt >= rangeStart &&
                    s.ClosedAt < rangeEndExclusive)
                .Where(s => !cashierId.HasValue || s.CashierId == cashierId.Value)
                .Select(s => new
                {
                    s.CashierId,
                    CashierName = s.Cashier.FirstName + " " + s.Cashier.LastName,
                    OpenedAt = s.OpenedAt,
                    ClosedAt = s.ClosedAt,
                    ExpectedCash = s.ExpectedCash,
                    ActualCash = s.ActualCash,
                    Variance = s.Variance
                })
                .ToListAsync();

            var shiftRows = shiftSummaryRows
                .OrderByDescending(s => s.ClosedAt)
                .Take(shiftTake)
                .Select(s => new ReportShiftVarianceViewModel
                {
                    CashierName = s.CashierName,
                    OpenedAt = s.OpenedAt,
                    ClosedAt = s.ClosedAt,
                    ExpectedCash = s.ExpectedCash,
                    ActualCash = s.ActualCash,
                    Variance = s.Variance
                })
                .ToList();

            var totalShiftVariance = Math.Round(shiftSummaryRows.Sum(x => x.Variance ?? 0m), 2);

            var auditSummaryRows = await _context.PosAuditLogs
                .Where(a =>
                    a.BusinessId == businessId &&
                    a.CreatedAt >= rangeStart &&
                    a.CreatedAt < rangeEndExclusive)
                .Where(a => !cashierId.HasValue || a.CashierId == cashierId.Value)
                .Select(a => new
                {
                    a.CashierId,
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
                .ToListAsync();

            var auditRows = auditSummaryRows
                .OrderByDescending(a => a.CreatedAt)
                .Take(30)
                .Select(a => new ReportAuditLogViewModel
                {
                    CreatedAt = a.CreatedAt,
                    CashierName = a.CashierName,
                    ActionType = a.ActionType,
                    BookingId = a.BookingId,
                    SourceSpaceName = a.SourceSpaceName,
                    TargetSpaceName = a.TargetSpaceName,
                    SplitCount = a.SplitCount,
                    InvoiceIds = a.InvoiceIds,
                    Details = a.Details
                })
                .ToList();

            var auditCount = auditSummaryRows.Count;

            var adjustmentRows = await _context.Adjustments
                .Where(a => a.Invoice.BusinessId == businessId &&
                            a.Invoice.GeneratedDate >= rangeStart &&
                            a.Invoice.GeneratedDate < rangeEndExclusive)
                .ToListAsync();
            var totalAdjustments = adjustmentRows.Where(a => a.AdjustmentType == "Debit").Sum(a => a.Amount)
                                 - adjustmentRows.Where(a => a.AdjustmentType == "Credit").Sum(a => a.Amount);

            var orderStatsByCashier = orderRows
                .GroupBy(x => x.CashierId)
                .ToDictionary(
                    g => g.Key,
                    g => new
                    {
                        Orders = g.Select(x => x.OrderId).Distinct().Count(),
                        Units = g.Sum(x => x.Quantity),
                        Sales = Math.Round(g.Sum(x => x.Revenue), 2),
                        Cost = Math.Round(g.Sum(x => x.Cost), 2)
                    });

            var shiftVarianceByCashier = shiftSummaryRows
                .GroupBy(x => x.CashierId)
                .ToDictionary(g => g.Key, g => Math.Round(g.Sum(x => x.Variance ?? 0m), 2));

            var auditCountsByCashier = auditSummaryRows
                .GroupBy(x => x.CashierId)
                .ToDictionary(g => g.Key, g => g.Count());

            var staffIds = orderStatsByCashier.Keys
                .Union(shiftVarianceByCashier.Keys)
                .Union(auditCountsByCashier.Keys)
                .Distinct();

            var staffPerformance = staffIds
                .Select(id =>
                {
                    orderStatsByCashier.TryGetValue(id, out var stats);
                    shiftVarianceByCashier.TryGetValue(id, out var shiftVariance);
                    auditCountsByCashier.TryGetValue(id, out var staffAuditCount);

                    var staffName = cashierNameById.TryGetValue(id, out var knownName)
                        ? knownName
                        : orderRows.FirstOrDefault(x => x.CashierId == id)?.CashierName
                            ?? shiftSummaryRows.FirstOrDefault(x => x.CashierId == id)?.CashierName
                            ?? auditSummaryRows.FirstOrDefault(x => x.CashierId == id)?.CashierName
                            ?? $"Cashier #{id}";

                    var sales = stats?.Sales ?? 0m;
                    var orders = stats?.Orders ?? 0;

                    return new ReportStaffPerformanceViewModel
                    {
                        CashierName = staffName,
                        Orders = orders,
                        UnitsSold = stats?.Units ?? 0,
                        Sales = sales,
                        GrossProfit = Math.Round(sales - (stats?.Cost ?? 0m), 2),
                        AverageTicket = orders <= 0 ? 0m : Math.Round(sales / orders, 2),
                        AuditEvents = staffAuditCount,
                        ShiftVariance = shiftVariance
                    };
                })
                .OrderByDescending(x => x.Sales)
                .ThenByDescending(x => x.Orders)
                .Take(10)
                .ToList();

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
                ActiveSpaceCount = activeSpaceCount,
                SpacesUsedCount = spaceUtilizationAll.Count,
                OccupancyRatePercent = occupancyRate,
                DailyLabels = dailyLabels,
                DailySalesSeries = dailySeries,
                CashierOptions = cashiers,
                PaymentMethodOptions = paymentMethods,
                TopItems = topItems,
                CategoryBreakdown = categoryBreakdown,
                ShiftVariances = shiftRows,
                RecentPosAuditLogs = auditRows,
                SpaceUtilization = spaceUtilization,
                StaffPerformance = staffPerformance
            };

            return model;
        }

        private int? GetBusinessId()
        {
            var value = User.FindFirst("BusinessId")?.Value;
            return int.TryParse(value, out var businessId) ? businessId : null;
        }

        public async Task<IActionResult> TrialBalance()
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var rows = await _context.JournalEntryLines
                .Where(l => l.ChartOfAccount.BusinessId == businessId.Value)
                .GroupBy(l => new { l.AccountId, l.ChartOfAccount.AccountName, l.ChartOfAccount.AccountType })
                .Select(g => new TrialBalanceRowViewModel
                {
                    AccountName = g.Key.AccountName,
                    AccountType = g.Key.AccountType,
                    TotalDebit = g.Sum(l => l.Debit),
                    TotalCredit = g.Sum(l => l.Credit)
                })
                .OrderBy(r => r.AccountType)
                .ThenBy(r => r.AccountName)
                .ToListAsync();

            var model = new TrialBalanceViewModel
            {
                AsOfDate = PhilippineTime.Now.Date,
                Rows = rows
            };

            return View(model);
        }

        public async Task<IActionResult> IncomeStatement()
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            // Revenue accounts: net = Credits - Debits (revenue increases on credit)
            var revenueLines = await _context.JournalEntryLines
                .Where(l => l.ChartOfAccount.BusinessId == businessId.Value
                         && l.ChartOfAccount.AccountType == "Revenue")
                .GroupBy(l => l.ChartOfAccount.AccountName)
                .Select(g => new IncomeStatementLineViewModel
                {
                    AccountName = g.Key,
                    Amount = g.Sum(l => l.Credit) - g.Sum(l => l.Debit)
                })
                .Where(l => l.Amount != 0)
                .OrderBy(l => l.AccountName)
                .ToListAsync();

            // Expense accounts: net = Debits - Credits (expenses increase on debit)
            var expenseLines = await _context.JournalEntryLines
                .Where(l => l.ChartOfAccount.BusinessId == businessId.Value
                         && l.ChartOfAccount.AccountType == "Expense")
                .GroupBy(l => l.ChartOfAccount.AccountName)
                .Select(g => new IncomeStatementLineViewModel
                {
                    AccountName = g.Key,
                    Amount = g.Sum(l => l.Debit) - g.Sum(l => l.Credit)
                })
                .Where(l => l.Amount != 0)
                .OrderBy(l => l.AccountName)
                .ToListAsync();

            var model = new IncomeStatementViewModel
            {
                AsOfDate = PhilippineTime.Now.Date,
                RevenueLines = revenueLines,
                ExpenseLines = expenseLines
            };

            return View(model);
        }

        public async Task<IActionResult> BalanceSheet()
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            // Assets: normal debit balance (Debit - Credit)
            var assetLines = await _context.JournalEntryLines
                .Where(l => l.ChartOfAccount.BusinessId == businessId.Value
                         && l.ChartOfAccount.AccountType == "Asset")
                .GroupBy(l => l.ChartOfAccount.AccountName)
                .Select(g => new IncomeStatementLineViewModel
                {
                    AccountName = g.Key,
                    Amount = g.Sum(l => l.Debit) - g.Sum(l => l.Credit)
                })
                .Where(l => l.Amount != 0)
                .OrderBy(l => l.AccountName)
                .ToListAsync();

            // Liabilities: normal credit balance (Credit - Debit)
            var liabilityLines = await _context.JournalEntryLines
                .Where(l => l.ChartOfAccount.BusinessId == businessId.Value
                         && l.ChartOfAccount.AccountType == "Liability")
                .GroupBy(l => l.ChartOfAccount.AccountName)
                .Select(g => new IncomeStatementLineViewModel
                {
                    AccountName = g.Key,
                    Amount = g.Sum(l => l.Credit) - g.Sum(l => l.Debit)
                })
                .Where(l => l.Amount != 0)
                .OrderBy(l => l.AccountName)
                .ToListAsync();

            // Equity: normal credit balance (Credit - Debit)
            var equityLines = await _context.JournalEntryLines
                .Where(l => l.ChartOfAccount.BusinessId == businessId.Value
                         && l.ChartOfAccount.AccountType == "Equity")
                .GroupBy(l => l.ChartOfAccount.AccountName)
                .Select(g => new IncomeStatementLineViewModel
                {
                    AccountName = g.Key,
                    Amount = g.Sum(l => l.Credit) - g.Sum(l => l.Debit)
                })
                .Where(l => l.Amount != 0)
                .OrderBy(l => l.AccountName)
                .ToListAsync();

            // Retained Earnings = cumulative Net Income (Revenue Credits - Expense Debits net)
            var totalRevenue = await _context.JournalEntryLines
                .Where(l => l.ChartOfAccount.BusinessId == businessId.Value
                         && l.ChartOfAccount.AccountType == "Revenue")
                .SumAsync(l => l.Credit - l.Debit);

            var totalExpenses = await _context.JournalEntryLines
                .Where(l => l.ChartOfAccount.BusinessId == businessId.Value
                         && l.ChartOfAccount.AccountType == "Expense")
                .SumAsync(l => l.Debit - l.Credit);

            var balanceSheetModel = new BalanceSheetViewModel
            {
                AsOfDate = PhilippineTime.Now.Date,
                AssetLines = assetLines,
                LiabilityLines = liabilityLines,
                EquityLines = equityLines,
                RetainedEarnings = totalRevenue - totalExpenses
            };

            return View(balanceSheetModel);
        }

        public async Task<IActionResult> CashFlow()
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            // Cash collected from customers via payments
            var cashFromCustomers = await _context.Payments
                .Where(p => p.BusinessId == businessId.Value)
                .SumAsync(p => (decimal?)p.AmountPaid) ?? 0m;

            // COGS: cost of all items sold (CostPrice × Qty) across all orders in this business
            var cogs = await _context.OrderDetails
                .Where(od => od.Order.BusinessId == businessId.Value)
                .SumAsync(od => (decimal?)(od.MenuItem.CostPrice * od.Quantity)) ?? 0m;

            // Net adjustments: Debit adjustments add to receivable, Credit adjustments reduce it
            var debitAdj = await _context.Adjustments
                .Where(a => a.Invoice.BusinessId == businessId.Value && a.AdjustmentType == "Debit")
                .SumAsync(a => (decimal?)a.Amount) ?? 0m;

            var creditAdj = await _context.Adjustments
                .Where(a => a.Invoice.BusinessId == businessId.Value && a.AdjustmentType == "Credit")
                .SumAsync(a => (decimal?)a.Amount) ?? 0m;

            var cashFlowModel = new CashFlowViewModel
            {
                AsOfDate = PhilippineTime.Now.Date,
                CashFromCustomers = cashFromCustomers,
                CostOfGoodsSold = cogs,
                NetAdjustments = debitAdj - creditAdj
            };

            return View(cashFlowModel);
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
