using System;
using System.Collections.Generic;

namespace ZoneBill_Lloren.Models
{
    public class BillingPageViewModel
    {
        public Business Business { get; set; } = null!;
        public SubscriptionPlan CurrentPlan { get; set; } = null!;
        public List<SubscriptionPlan> AvailablePlans { get; set; } = new();
        public List<SubscriptionInvoice> RecentInvoices { get; set; } = new();
        public bool IsSubscriptionExpired { get; set; }
        public bool CanManageStripeSubscription { get; set; }
        public bool IsCancellationScheduled { get; set; }
        public DateTime? CancellationEffectiveDate { get; set; }
        public string SubscriptionManagementStatus { get; set; } = string.Empty;
        public List<BillingPlanComparisonRow> PlanComparisonRows { get; set; } = new();
    }

    public class BillingPlanComparisonRow
    {
        public string FeatureLabel { get; set; } = string.Empty;
        public List<string> Values { get; set; } = new();
    }
}
