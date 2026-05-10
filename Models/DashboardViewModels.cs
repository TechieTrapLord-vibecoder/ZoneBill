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
        public decimal RangeRevenue { get; set; }
        public decimal MonthToDateRevenue { get; set; }
        public decimal YearToDateRevenue { get; set; }
        public int UnpaidInvoices { get; set; }
        public int LowStockCount { get; set; }
        public List<string> LowStockItems { get; set; } = new();
        public int ActiveShiftCount { get; set; }
        public string RangePreset { get; set; } = "7d";
        public string RangeLabel { get; set; } = "Last 7 Days";
        public DateTime RangeStart { get; set; }
        public DateTime RangeEnd { get; set; }
        public List<string> DailyLabels { get; set; } = new();
        public List<decimal> DailyRevenueSeries { get; set; } = new();
        public List<string> TopSpaceLabels { get; set; } = new();
        public List<decimal> TopSpaceRevenueSeries { get; set; } = new();
        public List<string> TopMenuLabels { get; set; } = new();
        public List<int> TopMenuQuantitySeries { get; set; } = new();
        public List<string> PaymentMethodLabels { get; set; } = new();
        public List<decimal> PaymentMethodAmounts { get; set; } = new();
        public int TodayBookings { get; set; }
        public int ActiveBookings { get; set; }
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
        public decimal CollectedThisMonth { get; set; }
        public decimal FailedThisMonth { get; set; }
        public decimal OutstandingReceivables { get; set; }
        public int ActiveSubscriptions { get; set; }
        public int PastDueSubscriptions { get; set; }
        public int BusinessesNeedingAttention { get; set; }
        public int NewBusinesses30Days { get; set; }
        public int NewBusinesses90Days { get; set; }
        public int ChurnedBusinesses30Days { get; set; }
        public int ChurnedBusinesses90Days { get; set; }
        public int FailedRenewals30Days { get; set; }
        public int FailedRenewals90Days { get; set; }
        public List<string> PlanLabels { get; set; } = new();
        public List<int> PlanBusinessCounts { get; set; } = new();
        public List<BusinessSignupViewModel> RecentSignups { get; set; } = new();
        public List<AttentionBusinessViewModel> AttentionBusinesses { get; set; } = new();
        public List<string> TrendLabels { get; set; } = new();
        public List<int> NewBusinessTrend { get; set; } = new();
        public List<int> ChurnTrend { get; set; } = new();
        public List<int> FailedRenewalTrend { get; set; } = new();
        public List<SuperAdminAuditItemViewModel> RecentAuditLogs { get; set; } = new();
    }

    public class AttentionBusinessViewModel
    {
        public int BusinessId { get; set; }
        public string BusinessName { get; set; } = string.Empty;
        public string PlanName { get; set; } = string.Empty;
        public string SubscriptionStatus { get; set; } = string.Empty;
        public DateTime? CurrentPeriodEnd { get; set; }
        public string LatestInvoiceStatus { get; set; } = string.Empty;
        public decimal LatestInvoiceAmount { get; set; }
        public string AttentionReason { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }

    public class SuperAdminAuditItemViewModel
    {
        public int AuditLogId { get; set; }
        public string ActionType { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public int? EntityId { get; set; }
        public int? BusinessId { get; set; }
        public string? BusinessName { get; set; }
        public string Details { get; set; } = string.Empty;
        public string? Reason { get; set; }
        public string ActorName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class AuditLogPageViewModel
    {
        public List<SuperAdminAuditItemViewModel> Logs { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; } = 25;
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
        public string? FilterActionType { get; set; }
        public string? FilterEntityType { get; set; }
        public string? FilterActor { get; set; }
        public string? FilterBusiness { get; set; }
        public DateTime? FilterFrom { get; set; }
        public DateTime? FilterTo { get; set; }
        public List<string> AvailableActionTypes { get; set; } = new();
        public List<string> AvailableEntityTypes { get; set; } = new();
    }

    public class AttentionPageViewModel
    {
        public List<AttentionBusinessViewModel> Businesses { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; } = 20;
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
        public string? Filter { get; set; }
        public string? Search { get; set; }
    }

    public class UserDetailsViewModel
    {
        public User User { get; set; } = null!;
        public List<PosAuditLog> RecentActivity { get; set; } = new();
    }
}
