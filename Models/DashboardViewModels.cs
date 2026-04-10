using System;
using System.Collections.Generic;

namespace ZoneBill_Lloren.Models
{
    public class JournalEntryTimelineViewModel
    {
        public JournalEntry Entry { get; set; } = null!;
        public List<JournalEntryLine> Lines { get; set; } = new();
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
    }

    public class DashboardViewModel
    {
        public decimal TodayRevenue { get; set; }
        public decimal SevenDayRevenue { get; set; }
        public int UnpaidInvoices { get; set; }
        public int LowStockCount { get; set; }
        public List<string> LowStockItems { get; set; } = new();
        public int ActiveShiftCount { get; set; }
        public List<string> DailyLabels { get; set; } = new();
        public List<decimal> DailyRevenueSeries { get; set; } = new();
        public List<string> TopSpaceLabels { get; set; } = new();
        public List<decimal> TopSpaceRevenueSeries { get; set; } = new();
        public List<string> TopMenuLabels { get; set; } = new();
        public List<int> TopMenuQuantitySeries { get; set; } = new();
    }

    public class InvoiceReceiptViewModel
    {
        public Invoice Invoice { get; set; } = null!;
        public List<InvoiceItem> Items { get; set; } = new();
        public List<Payment> Payments { get; set; } = new();
        public List<Adjustment> Adjustments { get; set; } = new();
        public decimal PaidAmount { get; set; }
        public decimal AdjustmentSum { get; set; }
        public decimal Balance { get; set; }
        public string InvoiceLookupUrl { get; set; } = string.Empty;
    }

    public class BusinessSignupViewModel
    {
        public string BusinessName { get; set; } = string.Empty;
        public string PlanName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class SuperAdminDashboardViewModel
    {
        public int TotalBusinesses { get; set; }
        public int ActiveBusinesses { get; set; }
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public decimal MonthlyRecurringRevenue { get; set; }
        public int ActiveSubscriptions { get; set; }
        public int PastDueSubscriptions { get; set; }
        public List<string> PlanLabels { get; set; } = new();
        public List<int> PlanBusinessCounts { get; set; } = new();
        public List<BusinessSignupViewModel> RecentSignups { get; set; } = new();
    }
}
