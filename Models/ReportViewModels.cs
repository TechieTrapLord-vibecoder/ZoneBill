namespace ZoneBill_Lloren.Models
{
    public class ReportsDashboardViewModel
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int? SelectedCashierId { get; set; }
        public string? SelectedPaymentMethod { get; set; }

        public int TotalOrders { get; set; }
        public int TotalUnitsSold { get; set; }

        public decimal TotalSales { get; set; }
        public decimal TotalCostOfGoods { get; set; }
        public decimal GrossProfit { get; set; }
        public decimal ProfitMarginPercent { get; set; }

        public int ClosedShiftCount { get; set; }
        public int OverShiftCount { get; set; }
        public int ShortShiftCount { get; set; }
        public decimal TotalShiftVariance { get; set; }
        public int AuditEventCount { get; set; }
        public decimal TotalAdjustments { get; set; }
        public int ActiveSpaceCount { get; set; }
        public int SpacesUsedCount { get; set; }
        public decimal OccupancyRatePercent { get; set; }

        public List<string> DailyLabels { get; set; } = new();
        public List<decimal> DailySalesSeries { get; set; } = new();

        public List<ReportFilterOptionViewModel> CashierOptions { get; set; } = new();
        public List<ReportFilterOptionViewModel> PaymentMethodOptions { get; set; } = new();

        public List<ReportTopItemViewModel> TopItems { get; set; } = new();
        public List<ReportShiftVarianceViewModel> ShiftVariances { get; set; } = new();
        public List<ReportAuditLogViewModel> RecentPosAuditLogs { get; set; } = new();
        public List<ReportSpaceUtilizationViewModel> SpaceUtilization { get; set; } = new();
        public List<ReportStaffPerformanceViewModel> StaffPerformance { get; set; } = new();
        public List<ReportCategoryBreakdownViewModel> CategoryBreakdown { get; set; } = new();
    }

    public class ReportFilterOptionViewModel
    {
        public string Value { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
    }

    public class ReportTopItemViewModel
    {
        public string ItemName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal Revenue { get; set; }
        public decimal Cost { get; set; }
        public decimal Profit => Revenue - Cost;
    }

    public class ReportShiftVarianceViewModel
    {
        public string CashierName { get; set; } = string.Empty;
        public DateTime OpenedAt { get; set; }
        public DateTime? ClosedAt { get; set; }
        public decimal ExpectedCash { get; set; }
        public decimal? ActualCash { get; set; }
        public decimal? Variance { get; set; }
    }

    public class ReportAuditLogViewModel
    {
        public DateTime CreatedAt { get; set; }
        public string CashierName { get; set; } = string.Empty;
        public string ActionType { get; set; } = string.Empty;
        public int? BookingId { get; set; }
        public string? SourceSpaceName { get; set; }
        public string? TargetSpaceName { get; set; }
        public int? SplitCount { get; set; }
        public string? InvoiceIds { get; set; }
        public string? Details { get; set; }
    }

    public class ReportSpaceUtilizationViewModel
    {
        public string SpaceName { get; set; } = string.Empty;
        public string FloorArea { get; set; } = string.Empty;
        public int BookingCount { get; set; }
        public decimal HoursBooked { get; set; }
        public decimal Revenue { get; set; }
        public decimal UtilizationPercent { get; set; }
    }

    public class ReportStaffPerformanceViewModel
    {
        public string CashierName { get; set; } = string.Empty;
        public int Orders { get; set; }
        public int UnitsSold { get; set; }
        public decimal Sales { get; set; }
        public decimal GrossProfit { get; set; }
        public decimal AverageTicket { get; set; }
        public int AuditEvents { get; set; }
        public decimal ShiftVariance { get; set; }
    }

    public class ReportCategoryBreakdownViewModel
    {
        public string Category { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
        public int UnitsSold { get; set; }
    }

    public class TrialBalanceViewModel
    {
        public DateTime AsOfDate { get; set; }
        public List<TrialBalanceRowViewModel> Rows { get; set; } = new();
        public decimal GrandTotalDebit => Rows.Sum(r => r.TotalDebit);
        public decimal GrandTotalCredit => Rows.Sum(r => r.TotalCredit);
    }

    public class TrialBalanceRowViewModel
    {
        public string AccountName { get; set; } = string.Empty;
        public string AccountType { get; set; } = string.Empty;
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public decimal Balance => TotalDebit - TotalCredit;
    }

    public class IncomeStatementViewModel
    {
        public DateTime AsOfDate { get; set; }
        public List<IncomeStatementLineViewModel> RevenueLines { get; set; } = new();
        public List<IncomeStatementLineViewModel> ExpenseLines { get; set; } = new();
        public decimal TotalRevenue => RevenueLines.Sum(l => l.Amount);
        public decimal TotalExpenses => ExpenseLines.Sum(l => l.Amount);
        public decimal NetIncome => TotalRevenue - TotalExpenses;
    }

    public class IncomeStatementLineViewModel
    {
        public string AccountName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    public class BalanceSheetViewModel
    {
        public DateTime AsOfDate { get; set; }
        public List<IncomeStatementLineViewModel> AssetLines { get; set; } = new();
        public List<IncomeStatementLineViewModel> LiabilityLines { get; set; } = new();
        public List<IncomeStatementLineViewModel> EquityLines { get; set; } = new();
        public decimal RetainedEarnings { get; set; }
        public decimal TotalAssets => AssetLines.Sum(l => l.Amount);
        public decimal TotalLiabilities => LiabilityLines.Sum(l => l.Amount);
        public decimal TotalEquity => EquityLines.Sum(l => l.Amount) + RetainedEarnings;
        public bool IsBalanced => TotalAssets == TotalLiabilities + TotalEquity;
        public decimal BalanceDifference => TotalAssets - (TotalLiabilities + TotalEquity);
    }

    public class CashFlowViewModel
    {
        public DateTime AsOfDate { get; set; }
        // Operating Activities
        public decimal CashFromCustomers { get; set; }
        public decimal CostOfGoodsSold { get; set; }
        public decimal NetAdjustments { get; set; }
        public decimal NetOperatingCash => CashFromCustomers - CostOfGoodsSold + NetAdjustments;
        // Net change (expand when investing/financing data is available)
        public decimal NetCashChange => NetOperatingCash;
    }

    public class PagingInfo
    {
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int TotalCount { get; set; }
        /// <summary>URL with __PAGE__ as the page placeholder, e.g. /Foo?search=x&amp;page=__PAGE__</summary>
        public string UrlTemplate { get; set; } = string.Empty;
    }
}
