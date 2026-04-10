using System.ComponentModel.DataAnnotations;

namespace ZoneBill_Lloren.Models
{
    public class InventoryIndexViewModel
    {
        public List<MenuItem> MenuItems { get; set; } = new();
        public List<MenuItem> LowStockItems { get; set; } = new();
        public List<InventoryTransaction> RecentTransactions { get; set; } = new();
    }

    public class RestockRequest
    {
        [Required]
        public int ItemId { get; set; }

        [Range(1, 100000)]
        public int Quantity { get; set; }

        [StringLength(255)]
        public string? Notes { get; set; }
    }

    public class StockAdjustmentRequest
    {
        [Required]
        public int ItemId { get; set; }

        [Required]
        [RegularExpression("Spoilage|Correction")]
        public string TransactionType { get; set; } = string.Empty;

        [Range(-100000, 100000)]
        public int Quantity { get; set; }

        [StringLength(255)]
        public string? Notes { get; set; }
    }
}
