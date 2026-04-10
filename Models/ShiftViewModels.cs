using System.ComponentModel.DataAnnotations;

namespace ZoneBill_Lloren.Models
{
    public class ShiftIndexViewModel
    {
        public PosShift? ActiveShift { get; set; }
        public decimal ActiveShiftCashSales { get; set; }
        public decimal ActiveShiftCashIn { get; set; }
        public decimal ActiveShiftCashOut { get; set; }
        public List<CashDrawerTransaction> ActiveShiftTransactions { get; set; } = new();
        public List<PosShift> RecentShifts { get; set; } = new();
        public List<ShiftOptionViewModel> OpenShiftOptions { get; set; } = new();
        public List<ShiftOptionViewModel> ClosedShiftOptions { get; set; } = new();

        public decimal ActiveShiftExpectedCash =>
            (ActiveShift?.OpeningCash ?? 0m) + ActiveShiftCashSales + ActiveShiftCashIn - ActiveShiftCashOut;
    }

    public class ShiftOptionViewModel
    {
        public int ShiftId { get; set; }
        public string Label { get; set; } = string.Empty;
    }

    public class OpenShiftRequest
    {
        [Range(0, 1000000)]
        public decimal OpeningCash { get; set; }

        [StringLength(255)]
        public string? Notes { get; set; }
    }

    public class CashDrawerTransactionRequest
    {
        [Required]
        [RegularExpression("CashIn|CashOut")]
        public string TransactionType { get; set; } = string.Empty;

        [Range(0.01, 1000000)]
        public decimal Amount { get; set; }

        [StringLength(255)]
        public string? Notes { get; set; }
    }

    public class CloseShiftRequest
    {
        [Range(0, 1000000)]
        public decimal ActualCash { get; set; }

        [StringLength(255)]
        public string? Notes { get; set; }
    }

    public class ForceCloseShiftRequest
    {
        [Range(1, int.MaxValue)]
        public int ShiftId { get; set; }

        [Range(0, 1000000)]
        public decimal ActualCash { get; set; }

        [StringLength(255)]
        public string? Notes { get; set; }
    }

    public class ReopenShiftRequest
    {
        [Range(1, int.MaxValue)]
        public int ShiftId { get; set; }

        [StringLength(255)]
        public string? Notes { get; set; }
    }
}
