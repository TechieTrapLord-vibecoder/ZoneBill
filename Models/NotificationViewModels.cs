using System.Collections.Generic;

namespace ZoneBill_Lloren.Models
{
    public class NotificationItemViewModel
    {
        public string Key { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Severity { get; set; } = "info";
        public string Icon { get; set; } = "bi-bell";
        public string Link { get; set; } = "/";
        public int Count { get; set; }
    }

    public class NotificationSummaryViewModel
    {
        public int Count { get; set; }
        public List<NotificationItemViewModel> Items { get; set; } = new();
    }
}