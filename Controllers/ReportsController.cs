using ClosedXML.Excel;
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

        public async Task<IActionResult> ExportExcel(DateTime? startDate, DateTime? endDate, int? cashierId, string? paymentMethod)
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var model = await BuildReportModelAsync(businessId.Value, startDate, endDate, cashierId, paymentMethod, topItemsTake: 200, shiftTake: 200);

            using var wb = new XLWorkbook();

            // ── Sheet 1: Summary ─────────────────────────────────────────────
            var ws = wb.Worksheets.Add("Summary");
            ws.Cell(1, 1).Value = "ZONEBILL FINANCIAL REPORT";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 20;
            ws.Cell(1, 1).Style.Font.FontColor = XLColor.White;
            ws.Range(1, 1, 1, 2).Merge();
            ws.Range(1, 1, 1, 2).Style.Fill.BackgroundColor = XLColor.FromHtml("#0F172A");
            ws.Range(1, 1, 1, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Row(1).Height = 30;

            ws.Cell(3, 1).Value = "Report Date Range:";
            ws.Cell(3, 2).Value = $"{model.StartDate:MMM dd, yyyy} to {model.EndDate:MMM dd, yyyy}";
            
            ws.Cell(4, 1).Value = "Generated By:";
            ws.Cell(4, 2).Value = User.Identity?.Name ?? "Authorized Admin";
            ws.Cell(4, 2).Style.Font.Bold = true;
            ws.Cell(4, 2).Style.Font.FontColor = XLColor.FromHtml("#06B6D4");

            ws.Cell(5, 1).Value = "Generated On:";
            ws.Cell(5, 2).Value = PhilippineTime.Now.ToString("MMM dd, yyyy HH:mm PHT");

            ws.Cell(6, 1).Value = "Cashier Filter:";
            ws.Cell(6, 2).Value = ResolveFilterLabel(model.CashierOptions, model.SelectedCashierId?.ToString());
            
            ws.Cell(7, 1).Value = "Payment Method:";
            ws.Cell(7, 2).Value = ResolveFilterLabel(model.PaymentMethodOptions, model.SelectedPaymentMethod);

            int r = 9;
            ws.Cell(r, 1).Value = "EXECUTIVE SUMMARY";
            ws.Cell(r, 1).Style.Font.Bold = true;
            ws.Cell(r, 1).Style.Font.FontSize = 14;
            ws.Cell(r, 1).Style.Font.FontColor = XLColor.FromHtml("#F97316");
            r++;

            void AddKpi(string label, object value, string format = "") {
                ws.Cell(r, 1).Value = label; 
                ws.Cell(r, 1).Style.Font.Bold = true;
                ws.Cell(r, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#F8FAFC");
                ws.Cell(r, 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                ws.Cell(r, 1).Style.Border.OutsideBorderColor = XLColor.FromHtml("#E2E8F0");
                
                if (value is string s) ws.Cell(r, 2).Value = s;
                else if (value is decimal d) ws.Cell(r, 2).Value = d;
                else if (value is int i) ws.Cell(r, 2).Value = i;

                if (!string.IsNullOrEmpty(format)) {
                    ws.Cell(r, 2).Style.NumberFormat.Format = format;
                }
                
                ws.Cell(r, 2).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                ws.Cell(r, 2).Style.Border.OutsideBorderColor = XLColor.FromHtml("#E2E8F0");
                ws.Cell(r, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                r++;
            }
            AddKpi("Total Orders", model.TotalOrders, "#,##0");
            AddKpi("Total Units Sold", model.TotalUnitsSold, "#,##0");
            AddKpi("Gross Sales", model.TotalSales, "₱#,##0.00");
            AddKpi("Cost of Goods Sold", model.TotalCostOfGoods, "₱#,##0.00");
            AddKpi("Gross Profit", model.GrossProfit, "₱#,##0.00");
            AddKpi("Profit Margin %", model.ProfitMarginPercent / 100m, "0.00%");
            AddKpi("Occupancy %", model.OccupancyRatePercent / 100m, "0.00%");
            AddKpi("Spaces Used", $"{model.SpacesUsedCount} of {model.ActiveSpaceCount}");
            AddKpi("Closed Shifts", model.ClosedShiftCount, "#,##0");
            AddKpi("Total Shift Variance", model.TotalShiftVariance, "₱#,##0.00");
            AddKpi("Net Adjustments", model.TotalAdjustments, "₱#,##0.00");
            
            ws.Column(1).Width = 25; 
            ws.Column(2).Width = 35;

            // Helper for Header styling
            void StyleHeader(IXLWorksheet sheet, int rowNum, int colCount) {
                var range = sheet.Range(rowNum, 1, rowNum, colCount);
                range.Style.Font.Bold = true;
                range.Style.Fill.BackgroundColor = XLColor.FromHtml("#0F172A");
                range.Style.Font.FontColor = XLColor.White;
                range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                sheet.Row(rowNum).Height = 25;
            }

            // Helper for Data Table styling
            void StyleTable(IXLWorksheet sheet, int startRow, int endRow, int colCount) {
                if (endRow < startRow) return;
                var range = sheet.Range(startRow, 1, endRow, colCount);
                range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                range.Style.Border.OutsideBorderColor = XLColor.FromHtml("#CBD5E1");
                range.Style.Border.InsideBorderColor = XLColor.FromHtml("#E2E8F0");
                sheet.Columns(1, colCount).AdjustToContents();
            }

            // ── Sheet 2: Top Items ────────────────────────────────────────────
            var wsItems = wb.Worksheets.Add("Top Items");
            string[] itemHdrs = { "Item", "Qty Sold", "Revenue", "COGS", "Profit" };
            for (int i = 0; i < itemHdrs.Length; i++) wsItems.Cell(1, i + 1).Value = itemHdrs[i];
            StyleHeader(wsItems, 1, itemHdrs.Length);
            
            int ir = 2;
            foreach (var item in model.TopItems) {
                wsItems.Cell(ir, 1).Value = item.ItemName;
                wsItems.Cell(ir, 2).Value = item.Quantity; wsItems.Cell(ir, 2).Style.NumberFormat.Format = "#,##0"; wsItems.Cell(ir, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                wsItems.Cell(ir, 3).Value = item.Revenue; wsItems.Cell(ir, 3).Style.NumberFormat.Format = "₱#,##0.00";
                wsItems.Cell(ir, 4).Value = item.Cost; wsItems.Cell(ir, 4).Style.NumberFormat.Format = "₱#,##0.00";
                wsItems.Cell(ir, 5).Value = item.Profit; wsItems.Cell(ir, 5).Style.NumberFormat.Format = "₱#,##0.00";
                if (ir % 2 == 0) wsItems.Range(ir, 1, ir, 5).Style.Fill.BackgroundColor = XLColor.FromHtml("#F8FAFC");
                ir++;
            }
            StyleTable(wsItems, 2, ir - 1, 5);

            // ── Sheet 3: Staff Performance ────────────────────────────────────
            var wsStaff = wb.Worksheets.Add("Staff Performance");
            string[] staffHdrs = { "Cashier", "Orders", "Units Sold", "Sales", "Gross Profit", "Avg Ticket", "Audit Events", "Shift Variance" };
            for (int i = 0; i < staffHdrs.Length; i++) wsStaff.Cell(1, i + 1).Value = staffHdrs[i];
            StyleHeader(wsStaff, 1, staffHdrs.Length);
            
            int sr = 2;
            foreach (var staff in model.StaffPerformance) {
                wsStaff.Cell(sr, 1).Value = staff.CashierName;
                wsStaff.Cell(sr, 2).Value = staff.Orders; wsStaff.Cell(sr, 2).Style.NumberFormat.Format = "#,##0"; wsStaff.Cell(sr, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                wsStaff.Cell(sr, 3).Value = staff.UnitsSold; wsStaff.Cell(sr, 3).Style.NumberFormat.Format = "#,##0"; wsStaff.Cell(sr, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                wsStaff.Cell(sr, 4).Value = staff.Sales; wsStaff.Cell(sr, 4).Style.NumberFormat.Format = "₱#,##0.00";
                wsStaff.Cell(sr, 5).Value = staff.GrossProfit; wsStaff.Cell(sr, 5).Style.NumberFormat.Format = "₱#,##0.00";
                wsStaff.Cell(sr, 6).Value = staff.AverageTicket; wsStaff.Cell(sr, 6).Style.NumberFormat.Format = "₱#,##0.00";
                wsStaff.Cell(sr, 7).Value = staff.AuditEvents; wsStaff.Cell(sr, 7).Style.NumberFormat.Format = "#,##0"; wsStaff.Cell(sr, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                wsStaff.Cell(sr, 8).Value = staff.ShiftVariance; wsStaff.Cell(sr, 8).Style.NumberFormat.Format = "₱#,##0.00";
                if (sr % 2 == 0) wsStaff.Range(sr, 1, sr, 8).Style.Fill.BackgroundColor = XLColor.FromHtml("#F8FAFC");
                sr++;
            }
            StyleTable(wsStaff, 2, sr - 1, 8);

            // ── Sheet 4: Space Utilization ────────────────────────────────────
            var wsSpaces = wb.Worksheets.Add("Space Utilization");
            string[] spaceHdrs = { "Space", "Floor Area", "Bookings", "Hours Booked", "Revenue", "Utilization %" };
            for (int i = 0; i < spaceHdrs.Length; i++) wsSpaces.Cell(1, i + 1).Value = spaceHdrs[i];
            StyleHeader(wsSpaces, 1, spaceHdrs.Length);
            
            int spr = 2;
            foreach (var space in model.SpaceUtilization) {
                wsSpaces.Cell(spr, 1).Value = space.SpaceName;
                wsSpaces.Cell(spr, 2).Value = space.FloorArea;
                wsSpaces.Cell(spr, 3).Value = space.BookingCount; wsSpaces.Cell(spr, 3).Style.NumberFormat.Format = "#,##0"; wsSpaces.Cell(spr, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                wsSpaces.Cell(spr, 4).Value = space.HoursBooked; wsSpaces.Cell(spr, 4).Style.NumberFormat.Format = "#,##0.0"; wsSpaces.Cell(spr, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                wsSpaces.Cell(spr, 5).Value = space.Revenue; wsSpaces.Cell(spr, 5).Style.NumberFormat.Format = "₱#,##0.00";
                wsSpaces.Cell(spr, 6).Value = space.UtilizationPercent / 100m; wsSpaces.Cell(spr, 6).Style.NumberFormat.Format = "0.00%"; wsSpaces.Cell(spr, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                if (spr % 2 == 0) wsSpaces.Range(spr, 1, spr, 6).Style.Fill.BackgroundColor = XLColor.FromHtml("#F8FAFC");
                spr++;
            }
            StyleTable(wsSpaces, 2, spr - 1, 6);

            // ── Sheet 5: Daily Sales ──────────────────────────────────────────
            var wsDaily = wb.Worksheets.Add("Daily Sales");
            wsDaily.Cell(1, 1).Value = "Date"; 
            wsDaily.Cell(1, 2).Value = "Sales";
            StyleHeader(wsDaily, 1, 2);
            
            for (int i = 0; i < model.DailyLabels.Count; i++) {
                wsDaily.Cell(i + 2, 1).Value = model.DailyLabels[i];
                wsDaily.Cell(i + 2, 2).Value = model.DailySalesSeries[i]; wsDaily.Cell(i + 2, 2).Style.NumberFormat.Format = "₱#,##0.00";
                if (i % 2 == 0) wsDaily.Range(i + 2, 1, i + 2, 2).Style.Fill.BackgroundColor = XLColor.FromHtml("#F8FAFC");
            }
            StyleTable(wsDaily, 2, model.DailyLabels.Count + 1, 2);

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            ms.Position = 0;
            var excelFileName = $"ZoneBill-Report-{model.StartDate:yyyyMMdd}-{model.EndDate:yyyyMMdd}.xlsx";
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", excelFileName);
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

                    // ── BACKGROUND WATERMARK ───────────────────────────────
                    page.Background().AlignCenter().AlignMiddle()
                        .Text("CONFIDENTIAL")
                        .FontColor(Colors.Grey.Lighten4)
                        .FontSize(80).Bold();

                    // ── HEADER ─────────────────────────────────────────────
                    page.Header().Column(col =>
                    {
                        col.Item().Background(cNavy).Padding(16).Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text(t =>
                                {
                                    t.Span("ZoneBill ").FontColor(Colors.White).Bold().FontSize(22);
                                    t.Span("Financial Report").FontColor(cCyan).Bold().FontSize(22);
                                });
                                c.Item().PaddingTop(5).Text(t =>
                                {
                                    t.Span($"{model.StartDate:MMM dd, yyyy}  –  {model.EndDate:MMM dd, yyyy}").FontColor(Colors.White).FontSize(9f);
                                    t.Span("    |    ").FontColor(Color.FromHex("334155")).FontSize(8.5f);
                                    t.Span($"Cashier: {cashierLabel}").FontColor(cSubtle).FontSize(8.5f);
                                    t.Span("    |    ").FontColor(Color.FromHex("334155")).FontSize(8.5f);
                                    t.Span($"Payment: {methodLabel}").FontColor(cSubtle).FontSize(8.5f);
                                });
                            });
                            row.ConstantItem(150).AlignRight().AlignMiddle().Column(c =>
                            {
                                c.Item().AlignRight().Text(t =>
                                {
                                    t.Span("GENERATED BY\n").FontColor(cSubtle).FontSize(7f).Bold();
                                    t.Span(User.Identity?.Name ?? "Authorized Admin").FontColor(Colors.White).FontSize(9f).SemiBold();
                                });
                                c.Item().PaddingTop(6).AlignRight().Text(t =>
                                {
                                    t.Span("DATE\n").FontColor(cSubtle).FontSize(7f).Bold();
                                    t.Span(PhilippineTime.Now.ToString("MMM dd, yyyy  HH:mm PHT")).FontColor(cCyan).FontSize(8.5f);
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
                            t.Span("ZoneBill Certified Financial Report  ·  ").FontColor(cMuted).FontSize(7.5f).Bold();
                            t.Span($"Generated by {User.Identity?.Name ?? "Admin"} at {PhilippineTime.Now:MMM dd, yyyy  HH:mm} PHT").FontColor(cMuted).FontSize(7.5f);
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

            // Build trial balance directly from real transaction data
            var rows = new List<TrialBalanceRowViewModel>();

            // ── Cash on Hand (by payment method) ──────────────────────────────
            var cashByMethod = await _context.Payments
                .Where(p => p.BusinessId == businessId.Value)
                .GroupBy(p => p.PaymentMethod)
                .Select(g => new { Method = g.Key, Total = g.Sum(p => p.AmountPaid) })
                .ToListAsync();
            foreach (var cb in cashByMethod)
            {
                var label = cb.Method switch { "GCash" => "GCash Wallet", "Card" => "Card Clearing", _ => $"Cash ({cb.Method})" };
                rows.Add(new TrialBalanceRowViewModel { AccountName = label, AccountType = "Asset", TotalDebit = cb.Total, TotalCredit = 0m });
            }

            // ── Accounts Receivable (unpaid invoices) ──────────────────────────
            var arTotal = await _context.Invoices
                .Where(i => i.BusinessId == businessId.Value && i.PaymentStatus != "Paid" && i.PaymentStatus != "Voided")
                .SumAsync(i => (decimal?)i.TotalAmount) ?? 0m;
            if (arTotal > 0)
                rows.Add(new TrialBalanceRowViewModel { AccountName = "Accounts Receivable", AccountType = "Asset", TotalDebit = arTotal, TotalCredit = 0m });

            // ── Inventory on Hand ─────────────────────────────────────────────
            var inventoryValue = await _context.MenuItems
                .Where(m => m.BusinessId == businessId.Value && m.IsActive)
                .SumAsync(m => (decimal?)(m.StockAvailable * m.CostPrice)) ?? 0m;
            if (inventoryValue > 0)
                rows.Add(new TrialBalanceRowViewModel { AccountName = "Inventory on Hand", AccountType = "Asset", TotalDebit = inventoryValue, TotalCredit = 0m });

            // ── Sales Revenue (paid invoices, net of tax) ─────────────────────
            var salesRevenue = await _context.Invoices
                .Where(i => i.BusinessId == businessId.Value && i.PaymentStatus == "Paid")
                .SumAsync(i => (decimal?)(i.TotalAmount - i.TaxAmount)) ?? 0m;
            if (salesRevenue > 0)
                rows.Add(new TrialBalanceRowViewModel { AccountName = "Sales Revenue", AccountType = "Revenue", TotalDebit = 0m, TotalCredit = salesRevenue });

            // ── Output Tax Payable ────────────────────────────────────────────
            var taxPayable = await _context.Invoices
                .Where(i => i.BusinessId == businessId.Value && i.PaymentStatus == "Paid")
                .SumAsync(i => (decimal?)i.TaxAmount) ?? 0m;
            if (taxPayable > 0)
                rows.Add(new TrialBalanceRowViewModel { AccountName = "Output Tax Payable (VAT)", AccountType = "Liability", TotalDebit = 0m, TotalCredit = taxPayable });

            // ── Cost of Goods Sold ────────────────────────────────────────────
            var totalCogs = await _context.OrderDetails
                .Where(od => od.Order.BusinessId == businessId.Value
                          && od.Order.Booking.BookingStatus == "Completed")
                .SumAsync(od => (decimal?)(od.MenuItem.CostPrice * od.Quantity)) ?? 0m;
            if (totalCogs > 0)
                rows.Add(new TrialBalanceRowViewModel { AccountName = "Cost of Goods Sold", AccountType = "Expense", TotalDebit = totalCogs, TotalCredit = 0m });

            // ── Retained Earnings (plug to balance) ───────────────────────────
            var totalPaidRevenue = await _context.Invoices
                .Where(i => i.BusinessId == businessId.Value && i.PaymentStatus == "Paid")
                .SumAsync(i => (decimal?)i.TotalAmount) ?? 0m;
            var retainedEarnings = totalPaidRevenue - totalCogs - taxPayable;
            if (retainedEarnings != 0)
                rows.Add(new TrialBalanceRowViewModel { AccountName = "Retained Earnings", AccountType = "Equity", TotalDebit = retainedEarnings < 0 ? Math.Abs(retainedEarnings) : 0m, TotalCredit = retainedEarnings >= 0 ? retainedEarnings : 0m });

            rows = rows.OrderBy(r => r.AccountType).ThenBy(r => r.AccountName).ToList();

            var model = new TrialBalanceViewModel
            {
                AsOfDate = PhilippineTime.Now.Date,
                Rows     = rows
            };

            return View(model);
        }

        public async Task<IActionResult> IncomeStatement()
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            // ── Revenue: sum of all PAID invoices (taxable base = TotalAmount - TaxAmount) ──
            var paidInvoices = await _context.Invoices
                .Where(i => i.BusinessId == businessId.Value && i.PaymentStatus == "Paid")
                .ToListAsync();

            var salesRevenue   = paidInvoices.Sum(i => i.TotalAmount - i.TaxAmount);
            var taxCollected   = paidInvoices.Sum(i => i.TaxAmount);
            var totalRevenue   = paidInvoices.Sum(i => i.TotalAmount);

            var revenueLines = new List<IncomeStatementLineViewModel>();
            if (salesRevenue > 0)
                revenueLines.Add(new IncomeStatementLineViewModel { AccountName = "Sales Revenue (Net of Tax)", Amount = salesRevenue });
            if (taxCollected > 0)
                revenueLines.Add(new IncomeStatementLineViewModel { AccountName = "Output Tax Collected (VAT)", Amount = taxCollected });

            // ── Expenses: COGS from all order details linked to completed bookings ──
            var totalCogs = await _context.OrderDetails
                .Where(od => od.Order.BusinessId == businessId.Value
                          && od.Order.Booking.BookingStatus == "Completed")
                .SumAsync(od => (decimal?)(od.MenuItem.CostPrice * od.Quantity)) ?? 0m;

            var expenseLines = new List<IncomeStatementLineViewModel>();
            if (totalCogs > 0)
                expenseLines.Add(new IncomeStatementLineViewModel { AccountName = "Cost of Goods Sold (COGS)", Amount = totalCogs });

            var model = new IncomeStatementViewModel
            {
                AsOfDate     = PhilippineTime.Now.Date,
                RevenueLines = revenueLines,
                ExpenseLines = expenseLines
            };

            return View(model);
        }

        public async Task<IActionResult> BalanceSheet()
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            // ── Assets ──────────────────────────────────────────────────────────
            // Cash = total of all payments received
            var cashBreakdown = await _context.Payments
                .Where(p => p.BusinessId == businessId.Value)
                .GroupBy(p => p.PaymentMethod)
                .Select(g => new { Method = g.Key, Total = g.Sum(p => p.AmountPaid) })
                .ToListAsync();

            var assetLines = new List<IncomeStatementLineViewModel>();
            foreach (var cb in cashBreakdown.OrderBy(x => x.Method))
            {
                var label = cb.Method switch
                {
                    "GCash" => "GCash Wallet",
                    "Card"  => "Card Clearing",
                    _       => $"Cash on Hand ({cb.Method})"
                };
                assetLines.Add(new IncomeStatementLineViewModel { AccountName = label, Amount = cb.Total });
            }

            // Accounts Receivable = unpaid invoice totals
            var arTotal = await _context.Invoices
                .Where(i => i.BusinessId == businessId.Value && i.PaymentStatus != "Paid" && i.PaymentStatus != "Voided")
                .SumAsync(i => (decimal?)i.TotalAmount) ?? 0m;
            if (arTotal > 0)
                assetLines.Add(new IncomeStatementLineViewModel { AccountName = "Accounts Receivable", Amount = arTotal });

            // Inventory value = current stock × cost price
            var inventoryValue = await _context.MenuItems
                .Where(m => m.BusinessId == businessId.Value && m.IsActive)
                .SumAsync(m => (decimal?)((decimal)m.StockAvailable * m.CostPrice)) ?? 0m;
            if (inventoryValue > 0)
                assetLines.Add(new IncomeStatementLineViewModel { AccountName = "Inventory on Hand", Amount = inventoryValue });

            // ── Liabilities ──────────────────────────────────────────────────────
            // Output Tax Payable = tax collected on all paid invoices
            var taxPayable = await _context.Invoices
                .Where(i => i.BusinessId == businessId.Value && i.PaymentStatus == "Paid")
                .SumAsync(i => (decimal?)i.TaxAmount) ?? 0m;

            var liabilityLines = new List<IncomeStatementLineViewModel>();
            if (taxPayable > 0)
                liabilityLines.Add(new IncomeStatementLineViewModel { AccountName = "Output Tax Payable (VAT)", Amount = taxPayable });

            // ── Equity / Retained Earnings ────────────────────────────────────────
            // Revenue = sum paid invoice totals (gross)
            var totalRevenue = await _context.Invoices
                .Where(i => i.BusinessId == businessId.Value && i.PaymentStatus == "Paid")
                .SumAsync(i => (decimal?)i.TotalAmount) ?? 0m;

            // COGS = cost price × qty for completed bookings
            var totalCogs = await _context.OrderDetails
                .Where(od => od.Order.BusinessId == businessId.Value
                          && od.Order.Booking.BookingStatus == "Completed")
                .SumAsync(od => (decimal?)(od.MenuItem.CostPrice * od.Quantity)) ?? 0m;

            var retainedEarnings = totalRevenue - totalCogs - taxPayable;

            var balanceSheetModel = new BalanceSheetViewModel
            {
                AsOfDate        = PhilippineTime.Now.Date,
                AssetLines      = assetLines,
                LiabilityLines  = liabilityLines,
                EquityLines     = new List<IncomeStatementLineViewModel>(),
                RetainedEarnings = retainedEarnings
            };

            return View(balanceSheetModel);
        }

        public async Task<IActionResult> CashFlow()
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            // Cash collected from customers (all payments received)
            var cashFromCustomers = await _context.Payments
                .Where(p => p.BusinessId == businessId.Value)
                .SumAsync(p => (decimal?)p.AmountPaid) ?? 0m;

            // COGS: cost of all items sold (CostPrice × Qty) for completed bookings only
            var cogs = await _context.OrderDetails
                .Where(od => od.Order.BusinessId == businessId.Value
                          && od.Order.Booking.BookingStatus == "Completed")
                .SumAsync(od => (decimal?)(od.MenuItem.CostPrice * od.Quantity)) ?? 0m;

            // Net adjustments on invoices
            var debitAdj = await _context.Adjustments
                .Where(a => a.Invoice.BusinessId == businessId.Value && a.AdjustmentType == "Debit")
                .SumAsync(a => (decimal?)a.Amount) ?? 0m;

            var creditAdj = await _context.Adjustments
                .Where(a => a.Invoice.BusinessId == businessId.Value && a.AdjustmentType == "Credit")
                .SumAsync(a => (decimal?)a.Amount) ?? 0m;

            var cashFlowModel = new CashFlowViewModel
            {
                AsOfDate          = PhilippineTime.Now.Date,
                CashFromCustomers = cashFromCustomers,
                CostOfGoodsSold   = cogs,
                NetAdjustments    = debitAdj - creditAdj
            };

            return View(cashFlowModel);
        }

        [Authorize(Roles = "MainAdmin")]
        public async Task<IActionResult> TenantAuditLog(DateTime? startDate, DateTime? endDate, string? actionType, string? staffName, int page = 1)
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var today = PhilippineTime.Now.Date;
            var rangeStart = startDate?.Date ?? today;
            var rangeEnd = endDate?.Date ?? today;
            if (rangeEnd < rangeStart) rangeEnd = rangeStart;
            var rangeEndExclusive = rangeEnd.AddDays(1);

            var query = _context.TenantAuditLogs
                .Where(l => l.BusinessId == businessId.Value && l.CreatedAt >= rangeStart && l.CreatedAt < rangeEndExclusive);

            if (!string.IsNullOrWhiteSpace(actionType))
                query = query.Where(l => l.ActionType == actionType);

            if (!string.IsNullOrWhiteSpace(staffName))
                query = query.Where(l => l.UserName != null && l.UserName.Contains(staffName));

            const int pageSize = 50;
            var totalCount = await query.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
            page = Math.Clamp(page, 1, totalPages);

            var logs = await query
                .OrderByDescending(l => l.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var actionTypes = await _context.TenantAuditLogs
                .Where(l => l.BusinessId == businessId.Value)
                .Select(l => l.ActionType)
                .Distinct()
                .OrderBy(a => a)
                .ToListAsync();

            ViewBag.StartDate = rangeStart.ToString("yyyy-MM-dd");
            ViewBag.EndDate = rangeEnd.ToString("yyyy-MM-dd");
            ViewBag.ActionType = actionType;
            ViewBag.StaffName = staffName;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalCount = totalCount;
            ViewBag.ActionTypes = actionTypes;

            return View(logs);
        }

        [Authorize(Roles = "MainAdmin")]
        public async Task<IActionResult> ExportAuditExcel(DateTime? startDate, DateTime? endDate, string? actionType, string? staffName)
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var today = PhilippineTime.Now.Date;
            var rangeStart = startDate?.Date ?? today;
            var rangeEnd = endDate?.Date ?? today;
            if (rangeEnd < rangeStart) rangeEnd = rangeStart;
            var rangeEndExclusive = rangeEnd.AddDays(1);

            var query = _context.TenantAuditLogs
                .Where(l => l.BusinessId == businessId.Value && l.CreatedAt >= rangeStart && l.CreatedAt < rangeEndExclusive);
            if (!string.IsNullOrWhiteSpace(actionType))
                query = query.Where(l => l.ActionType == actionType);
            if (!string.IsNullOrWhiteSpace(staffName))
                query = query.Where(l => l.UserName != null && l.UserName.Contains(staffName));

            var logs = await query.OrderByDescending(l => l.CreatedAt).ToListAsync();

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Audit Log");

            // Title
            ws.Cell(1, 1).Value = "ZONEBILL SECURITY AUDIT LOG";
            ws.Range(1, 1, 1, 7).Merge().Style.Font.SetBold().Font.SetFontSize(18).Font.SetFontColor(XLColor.White).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            ws.Range(1, 1, 1, 7).Style.Fill.BackgroundColor = XLColor.FromHtml("#0F172A");
            ws.Row(1).Height = 28;
            
            ws.Cell(3, 1).Value = "Period:"; ws.Cell(3, 1).Style.Font.SetBold(); ws.Cell(3, 2).Value = $"{rangeStart:MMM dd, yyyy} to {rangeEnd:MMM dd, yyyy}";
            ws.Cell(4, 1).Value = "Generated By:"; ws.Cell(4, 1).Style.Font.SetBold(); ws.Cell(4, 2).Value = User.Identity?.Name ?? "Authorized Admin"; ws.Cell(4, 2).Style.Font.SetBold().Font.SetFontColor(XLColor.FromHtml("#DC2626"));
            ws.Cell(5, 1).Value = "Generated On:"; ws.Cell(5, 1).Style.Font.SetBold(); ws.Cell(5, 2).Value = PhilippineTime.Now.ToString("MMM dd, yyyy HH:mm PHT");
            ws.Cell(6, 1).Value = "Total Records:"; ws.Cell(6, 1).Style.Font.SetBold(); ws.Cell(6, 2).Value = logs.Count;

            // Headers
            string[] hdrs = { "Date & Time", "Staff", "Role", "Action", "Entity Type", "Entity ID", "Details" };
            var hdrRow = ws.Row(6);
            for (int i = 0; i < hdrs.Length; i++)
            {
                ws.Cell(6, i + 1).Value = hdrs[i];
                ws.Cell(6, i + 1).Style.Font.Bold = true;
                ws.Cell(6, i + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#0F172A");
                ws.Cell(6, i + 1).Style.Font.FontColor = XLColor.White;
                ws.Cell(6, i + 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Cell(6, i + 1).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            }
            hdrRow.Height = 25;

            // Data rows
            int row = 7;
            foreach (var log in logs)
            {
                ws.Cell(row, 1).Value = PhilippineTime.ToDateTime(log.CreatedAt).ToString("yyyy-MM-dd HH:mm:ss"); ws.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Cell(row, 2).Value = log.UserName ?? "-";
                ws.Cell(row, 3).Value = log.UserRole ?? "-"; ws.Cell(row, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Cell(row, 4).Value = log.ActionType ?? "-"; ws.Cell(row, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Cell(row, 5).Value = log.EntityType ?? "-"; ws.Cell(row, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Cell(row, 6).Value = log.EntityId ?? "-"; ws.Cell(row, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Cell(row, 7).Value = log.Details ?? "-";
                
                if (row % 2 == 0) ws.Range(row, 1, row, 7).Style.Fill.BackgroundColor = XLColor.FromHtml("#F8FAFC");
                row++;
            }
            
            if (logs.Count > 0) {
                var dataRange = ws.Range(7, 1, row - 1, 7);
                dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                dataRange.Style.Border.InsideBorderColor = XLColor.FromHtml("#E2E8F0");
                dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                dataRange.Style.Border.OutsideBorderColor = XLColor.FromHtml("#CBD5E1");
            }

            ws.Columns(1, 7).AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            var fileName = $"ZoneBill-AuditLog-{rangeStart:yyyyMMdd}-{rangeEnd:yyyyMMdd}.xlsx";
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        [Authorize(Roles = "MainAdmin")]
        public async Task<IActionResult> ExportAuditPdf(DateTime? startDate, DateTime? endDate, string? actionType, string? staffName)
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var today = PhilippineTime.Now.Date;
            var rangeStart = startDate?.Date ?? today;
            var rangeEnd = endDate?.Date ?? today;
            if (rangeEnd < rangeStart) rangeEnd = rangeStart;
            var rangeEndExclusive = rangeEnd.AddDays(1);

            var query = _context.TenantAuditLogs
                .Where(l => l.BusinessId == businessId.Value && l.CreatedAt >= rangeStart && l.CreatedAt < rangeEndExclusive);
            if (!string.IsNullOrWhiteSpace(actionType))
                query = query.Where(l => l.ActionType == actionType);
            if (!string.IsNullOrWhiteSpace(staffName))
                query = query.Where(l => l.UserName != null && l.UserName.Contains(staffName));

            var logs = await query.OrderByDescending(l => l.CreatedAt).ToListAsync();

            QuestPDF.Settings.License = LicenseType.Community;
            var cNavy  = Color.FromHex("0F172A");
            var cCyan  = Color.FromHex("06B6D4");
            var cRed   = Color.FromHex("DC2626");
            var cMuted = Color.FromHex("64748B");
            var cSubtle = Color.FromHex("94A3B8");
            var cRowAlt = Color.FromHex("F1F5F9");
            var cBorder = Color.FromHex("E2E8F0");

            var pdfBytes = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(28);
                    page.Background(Colors.White);

                    // ── BACKGROUND WATERMARK ───────────────────────────────
                    page.Background().AlignCenter().AlignMiddle()
                        .Text("CONFIDENTIAL")
                        .FontColor(Colors.Grey.Lighten4)
                        .FontSize(70).Bold();

                    page.Header().Column(col =>
                    {
                        col.Item().Background(cNavy).Padding(12).Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text(t =>
                                {
                                    t.Span("ZoneBill ").FontColor(Colors.White).Bold().FontSize(18);
                                    t.Span("Security Audit Log").FontColor(cCyan).Bold().FontSize(18);
                                });
                                c.Item().PaddingTop(4).Text(t =>
                                {
                                    t.Span($"Period: {rangeStart:MMM dd, yyyy} — {rangeEnd:MMM dd, yyyy}").FontColor(Colors.White).FontSize(8.5f);
                                    t.Span("    |    ").FontColor(Color.FromHex("334155")).FontSize(8.5f);
                                    t.Span($"Records: {logs.Count}").FontColor(cMuted).FontSize(8.5f);
                                });
                            });
                            row.ConstantItem(150).AlignRight().AlignMiddle().Column(c =>
                            {
                                c.Item().AlignRight().Text(t =>
                                {
                                    t.Span("GENERATED BY\n").FontColor(cSubtle).FontSize(7f).Bold();
                                    t.Span(User.Identity?.Name ?? "Authorized Admin").FontColor(Colors.White).FontSize(9f).SemiBold();
                                });
                                c.Item().PaddingTop(6).AlignRight().Text(t =>
                                {
                                    t.Span("DATE\n").FontColor(cSubtle).FontSize(7f).Bold();
                                    t.Span(PhilippineTime.Now.ToString("MMM dd, yyyy  HH:mm PHT")).FontColor(cCyan).FontSize(8.5f);
                                });
                            });
                        });
                    });

                    page.Content().PaddingTop(12).Table(table =>
                    {
                        // Setup columns
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(1.8f); // Date
                            columns.RelativeColumn(1.5f); // Staff
                            columns.RelativeColumn(1f);   // Role
                            columns.RelativeColumn(1.2f); // Action
                            columns.RelativeColumn(1.2f); // Entity
                            columns.RelativeColumn(2.5f); // Details
                        });

                        // Table header
                        table.Header(header =>
                        {
                            void HdrCell(string txt, bool center = false) {
                                var c = header.Cell().Background(cNavy).PaddingHorizontal(5).PaddingVertical(6);
                                if (center) c = c.AlignCenter(); else c = c.AlignLeft();
                                c.Text(txt).FontColor(Colors.White).Bold().FontSize(8f);
                            }
                            
                            HdrCell("Date & Time"); 
                            HdrCell("Staff"); 
                            HdrCell("Role", true); 
                            HdrCell("Action", true); 
                            HdrCell("Entity", true); 
                            HdrCell("Details");
                        });

                        // Data rows
                        bool even = false;
                        foreach (var log in logs)
                        {
                            even = !even;
                            var bg = even ? cRowAlt : Colors.White;
                            
                            void Cell(string txt, bool center = false) {
                                var c = table.Cell().Background(bg).BorderBottom(0.5f).BorderColor(cBorder).PaddingHorizontal(5).PaddingVertical(4);
                                if (center) c = c.AlignCenter(); else c = c.AlignLeft();
                                c.Text(txt).FontSize(7.5f).FontColor(cNavy);
                            }

                            Cell(PhilippineTime.ToDateTime(log.CreatedAt).ToString("MMM dd, yyyy HH:mm"));
                            Cell(log.UserName ?? "-");
                            Cell(log.UserRole ?? "-", true);
                            Cell(log.ActionType ?? "-", true);
                            Cell(log.EntityType ?? "-", true);
                            Cell(log.Details ?? "-");
                        }
                    });

                    page.Footer().AlignCenter().Text(t =>
                    {
                        t.Span("ZoneBill Audit Log — Exported ").FontColor(cMuted).FontSize(7.5f);
                        t.Span(PhilippineTime.Now.ToString("yyyy-MM-dd HH:mm")).FontColor(cMuted).FontSize(7.5f);
                        t.Span("  |  Page ").FontColor(cMuted).FontSize(7.5f);
                        t.CurrentPageNumber().FontColor(cMuted).FontSize(7.5f);
                        t.Span(" of ").FontColor(cMuted).FontSize(7.5f);
                        t.TotalPages().FontColor(cMuted).FontSize(7.5f);
                    });
                });
            }).GeneratePdf();

            var pdfFileName = $"ZoneBill-AuditLog-{rangeStart:yyyyMMdd}-{rangeEnd:yyyyMMdd}.pdf";
            return File(pdfBytes, "application/pdf", pdfFileName);
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
