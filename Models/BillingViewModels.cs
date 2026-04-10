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
    }
}
