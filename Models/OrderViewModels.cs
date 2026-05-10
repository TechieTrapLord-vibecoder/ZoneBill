namespace ZoneBill_Lloren.Models
{
    public class OrderListRowMetrics
    {
        public int OrderId { get; set; }
        public int LineCount { get; set; }
        public int TotalQty { get; set; }
        public decimal MenuTotal { get; set; }
        public int ServedLines { get; set; }
        public int UnservedLines { get; set; }
    }
}
