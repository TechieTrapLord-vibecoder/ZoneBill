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

        public List<string> DailyLabels { get; set; } = new();
        public List<decimal> DailySalesSeries { get; set; } = new();

        public List<ReportFilterOptionViewModel> CashierOptions { get; set; } = new();
        public List<ReportFilterOptionViewModel> PaymentMethodOptions { get; set; } = new();

        public List<ReportTopItemViewModel> TopItems { get; set; } = new();
        public List<ReportShiftVarianceViewModel> ShiftVariances { get; set; } = new();
        public List<ReportAuditLogViewModel> RecentPosAuditLogs { get; set; } = new();
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
}
