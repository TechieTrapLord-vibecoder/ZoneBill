using System.ComponentModel.DataAnnotations;

namespace ZoneBill_Lloren.Models
{
    public class InventoryIndexViewModel
    {
        public List<MenuItem> MenuItems { get; set; } = new();
        public List<MenuItem> LowStockItems { get; set; } = new();
        public List<InventoryTransaction> RecentTransactions { get; set; } = new();
        public InventoryReorderSummaryViewModel ReorderSummary { get; set; } = new();
        public InventoryDemandForecastSummaryViewModel DemandForecast { get; set; } = new();
        public InventoryAnomalySummaryViewModel AnomalySummary { get; set; } = new();
        public InventoryAlertHistoryViewModel AlertHistory { get; set; } = new();
        public List<SupplierOptionViewModel> Suppliers { get; set; } = new();
        public List<PurchaseOrderListItemViewModel> RecentPurchaseOrders { get; set; } = new();
        public List<SupplierListItemViewModel> RecentSuppliers { get; set; } = new();
        public List<string> PurchaseOrderStatuses { get; set; } = new();
    }

    public class InventoryReorderSummaryViewModel
    {
        public int TotalRecommendations { get; set; }
        public int CriticalRecommendations { get; set; }
        public int RecommendedUnits { get; set; }
        public List<InventoryReorderRecommendationViewModel> Items { get; set; } = new();
    }

    public class InventoryDemandForecastSummaryViewModel
    {
        public int LookbackDays { get; set; }
        public int PrimaryHorizonDays { get; set; }
        public int ItemsForecasted { get; set; }
        public int TotalProjectedUnits7Days { get; set; }
        public int TotalProjectedUnits14Days { get; set; }
        public int TotalProjectedUnits30Days { get; set; }
        public InventoryForecastAccuracySummaryViewModel Accuracy { get; set; } = new();
        public List<InventoryDemandForecastItemViewModel> Items { get; set; } = new();
    }

    public class InventoryForecastAccuracySummaryViewModel
    {
        public int ItemsMeasured { get; set; }
        public int AccurateItems { get; set; }
        public int OverForecastedItems { get; set; }
        public int UnderForecastedItems { get; set; }
        public decimal AverageAccuracyPercent { get; set; }
        public List<InventoryForecastAccuracyItemViewModel> Items { get; set; } = new();
    }

    public class InventoryForecastAccuracyItemViewModel
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public int ForecastedUnits7Days { get; set; }
        public int ActualUnits7Days { get; set; }
        public int AbsoluteErrorUnits { get; set; }
        public decimal AccuracyPercent { get; set; }
        public string BiasDirection { get; set; } = "Balanced";
    }

    public class InventoryDemandForecastItemViewModel
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public int CurrentStock { get; set; }
        public decimal WeightedDailyForecast { get; set; }
        public int Forecast7Days { get; set; }
        public int Forecast14Days { get; set; }
        public int Forecast30Days { get; set; }
        public decimal? ForecastedDaysUntilStockout { get; set; }
        public string TrendDirection { get; set; } = "Stable";
        public string ConfidenceLabel { get; set; } = "Low";
        public int ForecastSuggestedReorderQuantity { get; set; }
    }

    public class InventoryAnomalySummaryViewModel
    {
        public int TotalAnomalies { get; set; }
        public int SpikeCount { get; set; }
        public int DeadStockCount { get; set; }
        public int DropCount { get; set; }
        public List<InventoryAnomalyItemViewModel> Items { get; set; } = new();
    }

    public class InventoryAnomalyItemViewModel
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public int CurrentStock { get; set; }
        public string AnomalyType { get; set; } = string.Empty;
        public string Severity { get; set; } = "Medium";
        public string SummaryText { get; set; } = string.Empty;
        public int RecentPeriodUnits { get; set; }
        public int BaselinePeriodUnits { get; set; }
        public int? DaysWithoutSales { get; set; }
        public decimal? TrendChangePercent { get; set; }
    }

    public class InventoryAlertHistoryViewModel
    {
        public InventoryAlertHistoryEntryViewModel? LatestAlert { get; set; }
        public List<InventoryAlertHistoryEntryViewModel> RecentAlerts { get; set; } = new();
    }

    public class InventoryAlertHistoryEntryViewModel
    {
        public string TriggerSource { get; set; } = string.Empty;
        public string RecipientEmail { get; set; } = string.Empty;
        public int RecommendationCount { get; set; }
        public int RecommendedUnits { get; set; }
        public DateTime SentAt { get; set; }
    }

    public class SupplierOptionViewModel
    {
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public string? ContactPerson { get; set; }
        public int? LeadTimeDaysOverride { get; set; }
    }

    public class PurchaseOrderListItemViewModel
    {
        public int PurchaseOrderId { get; set; }
        public int SupplierId { get; set; }
        public string PurchaseOrderNumber { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int TotalItems { get; set; }
        public int TotalUnits { get; set; }
        public int ReceivedUnits { get; set; }
        public decimal TotalCost { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ExpectedDeliveryDate { get; set; }
        public string? Notes { get; set; }
    }

    public class SupplierListItemViewModel
    {
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public string? ContactPerson { get; set; }
        public string? EmailAddress { get; set; }
        public string? PhoneNumber { get; set; }
        public int? LeadTimeDaysOverride { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class SupplierDetailsViewModel
    {
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public string? ContactPerson { get; set; }
        public string? EmailAddress { get; set; }
        public string? PhoneNumber { get; set; }
        public int? LeadTimeDaysOverride { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int PurchaseOrderCount { get; set; }
        public int ActiveDraftCount { get; set; }
        public bool CanReactivate => !IsActive;
        public List<PurchaseOrderListItemViewModel> RecentPurchaseOrders { get; set; } = new();
    }

    public class PurchaseOrderListPageViewModel
    {
        public string SelectedStatus { get; set; } = string.Empty;
        public int? SelectedSupplierId { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public List<string> AvailableStatuses { get; set; } = new();
        public List<SupplierListItemViewModel> SupplierFilters { get; set; } = new();
        public List<PurchaseOrderListItemViewModel> PurchaseOrders { get; set; } = new();
    }

    public class PurchaseOrderDetailsViewModel
    {
        public int PurchaseOrderId { get; set; }
        public int SupplierId { get; set; }
        public string PurchaseOrderNumber { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public string? ContactPerson { get; set; }
        public string? EmailAddress { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? OrderedAt { get; set; }
        public DateTime? ReceivedAt { get; set; }
        public DateTime? ExpectedDeliveryDate { get; set; }
        public bool CanEditDraft => string.Equals(Status, "Draft", StringComparison.OrdinalIgnoreCase);
        public bool CanMarkOrdered => string.Equals(Status, "Draft", StringComparison.OrdinalIgnoreCase);
        public bool CanReceive => string.Equals(Status, "Ordered", StringComparison.OrdinalIgnoreCase) || string.Equals(Status, "PartiallyReceived", StringComparison.OrdinalIgnoreCase);
        public bool CanCancel => (string.Equals(Status, "Draft", StringComparison.OrdinalIgnoreCase) || string.Equals(Status, "Ordered", StringComparison.OrdinalIgnoreCase)) && TotalReceivedUnits == 0;
        public bool CanClose => (string.Equals(Status, "Ordered", StringComparison.OrdinalIgnoreCase) || string.Equals(Status, "PartiallyReceived", StringComparison.OrdinalIgnoreCase)) && HasOutstandingUnits;
        public decimal TotalCost => Lines.Sum(line => line.LineTotal);
        public int TotalUnits => Lines.Sum(line => line.Quantity);
        public int TotalReceivedUnits => Lines.Sum(line => line.ReceivedQuantity);
        public bool HasOutstandingUnits => Lines.Any(line => line.OutstandingQuantity > 0);
        public List<PurchaseOrderLineEditorViewModel> Lines { get; set; } = new();
        public List<PurchaseOrderReceiptHistoryEntryViewModel> ReceiptHistory { get; set; } = new();
    }

    public class PurchaseOrderLineEditorViewModel
    {
        public int PurchaseOrderLineId { get; set; }
        public int ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public int CurrentStock { get; set; }
        public int Quantity { get; set; }
        public int ReceivedQuantity { get; set; }
        public int OutstandingQuantity => Math.Max(0, Quantity - ReceivedQuantity);
        public decimal UnitCost { get; set; }
        public decimal LineTotal => UnitCost * Quantity;
    }

    public class PurchaseOrderReceiveLineInput
    {
        public int PurchaseOrderLineId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public int OrderedQuantity { get; set; }
        public int ReceivedQuantity { get; set; }
        public int OutstandingQuantity => Math.Max(0, OrderedQuantity - ReceivedQuantity);
        public int ReceiveNowQuantity { get; set; }
    }

    public class PrintablePurchaseOrderViewModel
    {
        public string PurchaseOrderNumber { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string BusinessName { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public string? ContactPerson { get; set; }
        public string? EmailAddress { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ExpectedDeliveryDate { get; set; }
        public decimal TotalCost => Lines.Sum(line => line.LineTotal);
        public List<PurchaseOrderLineEditorViewModel> Lines { get; set; } = new();
        public List<PurchaseOrderReceiptHistoryEntryViewModel> ReceiptHistory { get; set; } = new();
    }

    public class PurchaseOrderReceiptHistoryEntryViewModel
    {
        public int PurchaseOrderReceiptId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public int QuantityReceived { get; set; }
        public int PreviousReceivedQuantity { get; set; }
        public int NewReceivedQuantity { get; set; }
        public int PreviousStock { get; set; }
        public int NewStock { get; set; }
        public string? Notes { get; set; }
        public DateTime ReceivedAt { get; set; }
    }

    public class InventoryReorderRecommendationViewModel
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public int CurrentStock { get; set; }
        public int LowStockThreshold { get; set; }
        public int QuantitySoldInLookback { get; set; }
        public decimal AverageDailyDemand { get; set; }
        public int ReorderPoint { get; set; }
        public int TargetStock { get; set; }
        public int RecommendedReorderQuantity { get; set; }
        public int ForecastSuggestedReorderQuantity { get; set; }
        public decimal? DaysUntilStockout { get; set; }
        public decimal? ForecastedDaysUntilStockout { get; set; }
        public decimal ForecastedDailyDemand { get; set; }
        public int Forecast7Days { get; set; }
        public string ForecastTrendDirection { get; set; } = "Stable";
        public string Urgency { get; set; } = "Stable";
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

    public class CreateSupplierRequest
    {
        [Required]
        [StringLength(120)]
        public string SupplierName { get; set; } = string.Empty;

        [StringLength(120)]
        public string? ContactPerson { get; set; }

        [EmailAddress]
        [StringLength(256)]
        public string? EmailAddress { get; set; }

        [StringLength(30)]
        public string? PhoneNumber { get; set; }

        [Range(1, 30)]
        public int? LeadTimeDaysOverride { get; set; }
    }

    public class UpdateSupplierRequest
    {
        [Required]
        public int SupplierId { get; set; }

        [Required]
        [StringLength(120)]
        public string SupplierName { get; set; } = string.Empty;

        [StringLength(120)]
        public string? ContactPerson { get; set; }

        [EmailAddress]
        [StringLength(256)]
        public string? EmailAddress { get; set; }

        [StringLength(30)]
        public string? PhoneNumber { get; set; }

        [Range(1, 30)]
        public int? LeadTimeDaysOverride { get; set; }
    }

    public class CreateDraftPurchaseOrderRequest
    {
        [Required]
        public int SupplierId { get; set; }

        [StringLength(255)]
        public string? Notes { get; set; }
    }

    public class InventoryAnomalyActionRequest
    {
        [Required]
        public int ItemId { get; set; }

        public int? SupplierId { get; set; }
    }

    public class UpdatePurchaseOrderDraftRequest
    {
        [Required]
        public int PurchaseOrderId { get; set; }

        [StringLength(255)]
        public string? Notes { get; set; }

        public List<int> PurchaseOrderLineIds { get; set; } = new();
        public List<int> Quantities { get; set; } = new();
        public List<decimal> UnitCosts { get; set; } = new();
    }

    public class PurchaseOrderActionRequest
    {
        [Required]
        public int PurchaseOrderId { get; set; }
    }

    public class ReceivePurchaseOrderRequest
    {
        [Required]
        public int PurchaseOrderId { get; set; }

        public List<int> PurchaseOrderLineIds { get; set; } = new();
        public List<int> ReceiveQuantities { get; set; } = new();
        [StringLength(255)]
        public string? Notes { get; set; }
    }

    public class SupplierActionRequest
    {
        [Required]
        public int SupplierId { get; set; }
    }
}
