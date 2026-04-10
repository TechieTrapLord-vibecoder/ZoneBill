using System.ComponentModel.DataAnnotations;

namespace ZoneBill_Lloren.Models
{
    public class PosSpaceCardViewModel
    {
        public int SpaceId { get; set; }
        public string SpaceName { get; set; } = string.Empty;
        public string FloorArea { get; set; } = string.Empty;
        public int Capacity { get; set; }
        public decimal HourlyRate { get; set; }
        public string Status { get; set; } = "Available";
        public int? ActiveBookingId { get; set; }
        public DateTime? ActiveStartTime { get; set; }
        public int OrderItemCount { get; set; }
        public bool CheckoutRequested { get; set; }
    }

    public class PosFloorAreaViewModel
    {
        public string FloorArea { get; set; } = string.Empty;
        public List<PosSpaceCardViewModel> Spaces { get; set; } = new();
    }

    public class PosTableLayoutViewModel
    {
        public List<PosFloorAreaViewModel> Floors { get; set; } = new();
        public List<Space> AvailableSpaces { get; set; } = new();
    }

    public class PosDashboardViewModel
    {
        public List<PosSpaceCardViewModel> Spaces { get; set; } = new();
        public List<Customer> Customers { get; set; } = new();
        public List<MenuItem> MenuItems { get; set; } = new();
    }

    public class StartSessionRequest
    {
        [Required]
        public int SpaceId { get; set; }

        public int? CustomerId { get; set; }
    }

    public class AddOrderRequest
    {
        [Required]
        public int BookingId { get; set; }

        [Required]
        public int ItemId { get; set; }

        [Range(1, 999)]
        public int Quantity { get; set; } = 1;
    }

    public class CheckoutRequest
    {
        [Required]
        public int BookingId { get; set; }

        [Range(0, 100)]
        public decimal DiscountPercentage { get; set; } = 0m;

        [MaxLength(50)]
        public string PaymentMethod { get; set; } = "Cash";
    }

    public class TransferTableRequest
    {
        [Required]
        public int BookingId { get; set; }

        [Required]
        public int ToSpaceId { get; set; }
    }

    public class SplitCheckoutRequest
    {
        [Required]
        public int BookingId { get; set; }

        [Range(0, 100)]
        public decimal DiscountPercentage { get; set; } = 0m;

        [Range(2, 20)]
        public int SplitCount { get; set; } = 2;

        [MaxLength(50)]
        public string PaymentMethod { get; set; } = "Cash";
    }

    // ── Customer QR Portal ViewModels ─────────────────────────────────────

    public class CustomerPortalViewModel
    {
        public bool HasActiveSession { get; set; }
        public string BusinessName { get; set; } = string.Empty;
        public int SpaceId { get; set; }
        public string SpaceName { get; set; } = string.Empty;
        public string FloorArea { get; set; } = string.Empty;
        public int? BookingId { get; set; }
        public string? ReferenceCode { get; set; }
        public DateTime? StartTime { get; set; }
        public decimal HourlyRate { get; set; }
        public decimal ElapsedHours { get; set; }
        public decimal TimeCharge { get; set; }
        public List<CustomerOrderItemViewModel> OrderItems { get; set; } = new();
        public decimal MenuTotal { get; set; }
        public decimal TaxRatePercent { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal EstimatedTotal { get; set; }
        public bool CheckoutRequested { get; set; }
        public List<CustomerMenuItemViewModel> MenuItems { get; set; } = new();
    }

    public class CustomerOrderItemViewModel
    {
        public string ItemName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
    }

    public class CustomerMenuItemViewModel
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int StockAvailable { get; set; }
    }

    public class CustomerOrderRequest
    {
        [Required]
        public int SpaceId { get; set; }

        [Required]
        public int ItemId { get; set; }

        [Range(1, 99)]
        public int Quantity { get; set; } = 1;
    }
}
