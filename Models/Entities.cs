using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ZoneBill_Lloren.Models
{
    public class SubscriptionPlan
    {
        [Key] public int PlanId { get; set; }
        [Required, MaxLength(50)] public string PlanName { get; set; } = null!;
        [Column(TypeName = "decimal(18,2)")] public decimal MonthlyPrice { get; set; }
        [MaxLength(100)] public string? StripePriceId { get; set; }
        public int MaxTablesAllowed { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class Business
    {
        [Key] public int BusinessId { get; set; }
        public int PlanId { get; set; }
        [ForeignKey("PlanId")] public SubscriptionPlan Plan { get; set; } = null!;
        [Required, MaxLength(100)] public string BusinessName { get; set; } = null!;
        [Required, MaxLength(50)] public string DomainPrefix { get; set; } = null!;
        [MaxLength(500)] public string? LogoUrl { get; set; }
        [Column(TypeName = "decimal(5,2)")] public decimal TaxRatePercentage { get; set; } = 0m;
        [Required, MaxLength(20)] public string SubscriptionStatus { get; set; } = "Active";
        public DateTime? CurrentPeriodEnd { get; set; }
        [MaxLength(100)] public string? StripeCustomerId { get; set; }
        [MaxLength(100)] public string? StripeSubscriptionId { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class SubscriptionInvoice
    {
        [Key] public int SubscriptionInvoiceId { get; set; }
        public int BusinessId { get; set; }
        [ForeignKey("BusinessId")] public Business Business { get; set; } = null!;
        public int PlanId { get; set; }
        [ForeignKey("PlanId")] public SubscriptionPlan Plan { get; set; } = null!;
        [Column(TypeName = "decimal(18,2)")] public decimal Amount { get; set; }
        [Required, MaxLength(20)] public string Status { get; set; } = "Paid";
        [Required, MaxLength(50)] public string PaymentMethod { get; set; } = "MockGateway";
        public DateTime IssuedAt { get; set; }
        public DateTime? PaidAt { get; set; }
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        [MaxLength(100)] public string? ExternalReference { get; set; }
    }

    public class User
    {
        [Key] public int UserId { get; set; }
        public int? BusinessId { get; set; }
        [ForeignKey("BusinessId")] public Business? Business { get; set; }
        [Required, MaxLength(20)] public string UserRole { get; set; } = null!;
        [Required, MaxLength(50)] public string FirstName { get; set; } = null!;
        [Required, MaxLength(50)] public string LastName { get; set; } = null!;
        [Required, MaxLength(256)] public string EmailAddress { get; set; } = null!;
        [Required] public string PasswordHash { get; set; } = null!;
        public bool IsActive { get; set; } = true;
        public string? PasswordResetToken { get; set; }
        public DateTime? PasswordResetTokenExpiry { get; set; }
    }

    public class Customer
    {
        [Key] public int CustomerId { get; set; }
        public int BusinessId { get; set; }
        [ForeignKey("BusinessId")] public Business Business { get; set; } = null!;
        [Required, MaxLength(100)] public string Name { get; set; } = null!;
        [MaxLength(256)] public string? Email { get; set; }
        [MaxLength(20)] public string? Phone { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class Space
    {
        [Key] public int SpaceId { get; set; }
        public int BusinessId { get; set; }
        [ForeignKey("BusinessId")] public Business Business { get; set; } = null!;
        [Required, MaxLength(50)] public string SpaceName { get; set; } = null!;
        [Required, MaxLength(50)] public string FloorArea { get; set; } = "Main Floor";
        public int Capacity { get; set; } = 4;
        [Column(TypeName = "decimal(18,2)")] public decimal CurrentHourlyRate { get; set; }
        [Required, MaxLength(20)] public string CurrentStatus { get; set; } = null!;
        public bool IsActive { get; set; } = true;
    }

    public class Booking
    {
        [Key] public int BookingId { get; set; }
        public int BusinessId { get; set; }
        [ForeignKey("BusinessId")] public Business Business { get; set; } = null!;
        public int SpaceId { get; set; }
        [ForeignKey("SpaceId")] public Space Space { get; set; } = null!;
        public int? CustomerId { get; set; }
        [ForeignKey("CustomerId")] public Customer? Customer { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        [Column(TypeName = "decimal(10,2)")] public decimal? DurationHours { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal LockedHourlyRate { get; set; }
        [Required, MaxLength(20)] public string BookingStatus { get; set; } = null!;
        [MaxLength(20)] public string? ReferenceCode { get; set; }
        public bool CheckoutRequested { get; set; }
        public int? RequestedSplitCount { get; set; }
        [MaxLength(100)] public string? CustomerEmail { get; set; }
        public bool CustomerReceiptEmailSent { get; set; }
    }

    public class PosShift
    {
        [Key] public int ShiftId { get; set; }
        public int BusinessId { get; set; }
        [ForeignKey("BusinessId")] public Business Business { get; set; } = null!;
        public int CashierId { get; set; }
        [ForeignKey("CashierId")] public User Cashier { get; set; } = null!;
        public DateTime OpenedAt { get; set; }
        public DateTime? ClosedAt { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal OpeningCash { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal ExpectedCash { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal? ActualCash { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal? Variance { get; set; }
        [Required, MaxLength(20)] public string Status { get; set; } = "Open";
        [MaxLength(255)] public string? Notes { get; set; }
    }

    public class CashDrawerTransaction
    {
        [Key] public int DrawerTransactionId { get; set; }
        public int BusinessId { get; set; }
        [ForeignKey("BusinessId")] public Business Business { get; set; } = null!;
        public int ShiftId { get; set; }
        [ForeignKey("ShiftId")] public PosShift Shift { get; set; } = null!;
        [Required, MaxLength(20)] public string TransactionType { get; set; } = null!;
        [Column(TypeName = "decimal(18,2)")] public decimal Amount { get; set; }
        [MaxLength(255)] public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class PosAuditLog
    {
        [Key] public int PosAuditLogId { get; set; }
        public int BusinessId { get; set; }
        [ForeignKey("BusinessId")] public Business Business { get; set; } = null!;
        public int CashierId { get; set; }
        [ForeignKey("CashierId")] public User Cashier { get; set; } = null!;
        public int? BookingId { get; set; }
        [Required, MaxLength(40)] public string ActionType { get; set; } = null!;
        public int? SourceSpaceId { get; set; }
        [MaxLength(50)] public string? SourceSpaceName { get; set; }
        public int? TargetSpaceId { get; set; }
        [MaxLength(50)] public string? TargetSpaceName { get; set; }
        public int? SplitCount { get; set; }
        [MaxLength(255)] public string? InvoiceIds { get; set; }
        [MaxLength(500)] public string? Details { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class MenuItem
    {
        [Key] public int ItemId { get; set; }
        public int BusinessId { get; set; }
        [ForeignKey("BusinessId")] public Business Business { get; set; } = null!;
        [Required, MaxLength(100)] public string ItemName { get; set; } = null!;
        [Column(TypeName = "decimal(18,2)")] public decimal CurrentPrice { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal CostPrice { get; set; } = 0m;
        public int StockAvailable { get; set; } = 0;
        public int LowStockThreshold { get; set; } = 5;
        public bool IsActive { get; set; } = true;
    }

    public class InventoryTransaction
    {
        [Key] public int InventoryTransactionId { get; set; }
        public int BusinessId { get; set; }
        [ForeignKey("BusinessId")] public Business Business { get; set; } = null!;
        public int ItemId { get; set; }
        [ForeignKey("ItemId")] public MenuItem MenuItem { get; set; } = null!;
        public int QuantityChange { get; set; }
        public int PreviousStock { get; set; }
        public int NewStock { get; set; }
        [Required, MaxLength(20)] public string TransactionType { get; set; } = null!;
        [MaxLength(255)] public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class Order
    {
        [Key] public int OrderId { get; set; }
        public int BusinessId { get; set; }
        [ForeignKey("BusinessId")] public Business Business { get; set; } = null!;
        public int BookingId { get; set; }
        [ForeignKey("BookingId")] public Booking Booking { get; set; } = null!;
        public int CashierId { get; set; }
        [ForeignKey("CashierId")] public User Cashier { get; set; } = null!;
        public DateTime OrderTime { get; set; }
    }

    public class OrderDetail
    {
        [Key] public int OrderDetailId { get; set; }
        public int OrderId { get; set; }
        [ForeignKey("OrderId")] public Order Order { get; set; } = null!;
        public int ItemId { get; set; }
        [ForeignKey("ItemId")] public MenuItem MenuItem { get; set; } = null!;
        public int Quantity { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal LockedUnitPrice { get; set; }
    }

    public class Invoice
    {
        [Key] public int InvoiceId { get; set; }
        public int BusinessId { get; set; }
        [ForeignKey("BusinessId")] public Business Business { get; set; } = null!;
        public int BookingId { get; set; }
        [ForeignKey("BookingId")] public Booking Booking { get; set; } = null!;
        [Column(TypeName = "decimal(18,2)")] public decimal SubTotal { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal DiscountAmount { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal TaxAmount { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal TotalAmount { get; set; }
        [Column(TypeName = "decimal(5,4)")] public decimal TaxRateApplied { get; set; }
        [Required, MaxLength(20)] public string PaymentStatus { get; set; } = null!;
        public DateTime GeneratedDate { get; set; }
    }

    public class InvoiceItem
    {
        [Key] public int InvoiceItemId { get; set; }
        public int InvoiceId { get; set; }
        [ForeignKey("InvoiceId")] public Invoice Invoice { get; set; } = null!;
        [Required, MaxLength(20)] public string ItemType { get; set; } = null!;
        [Required, MaxLength(100)] public string Description { get; set; } = null!;
        public int Quantity { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal UnitPrice { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal Total { get; set; }
    }

    public class Adjustment
    {
        [Key] public int AdjustmentId { get; set; }
        public int InvoiceId { get; set; }
        [ForeignKey("InvoiceId")] public Invoice Invoice { get; set; } = null!;
        [Required, MaxLength(10)] public string AdjustmentType { get; set; } = null!;
        [Column(TypeName = "decimal(18,2)")] public decimal Amount { get; set; }
        [MaxLength(255)] public string? Reason { get; set; }
    }

    public class Payment
    {
        [Key] public int PaymentId { get; set; }
        public int BusinessId { get; set; }
        [ForeignKey("BusinessId")] public Business Business { get; set; } = null!;
        public int InvoiceId { get; set; }
        [ForeignKey("InvoiceId")] public Invoice Invoice { get; set; } = null!;
        [Column(TypeName = "decimal(18,2)")] public decimal AmountPaid { get; set; }
        [Required, MaxLength(50)] public string PaymentMethod { get; set; } = null!;
        public DateTime PaymentDate { get; set; }
        [MaxLength(100)] public string? ReferenceNumber { get; set; }
    }

    public class ChartOfAccount
    {
        [Key] public int AccountId { get; set; }
        public int BusinessId { get; set; }
        [ForeignKey("BusinessId")] public Business Business { get; set; } = null!;
        [Required, MaxLength(100)] public string AccountName { get; set; } = null!;
        [Required, MaxLength(50)] public string AccountType { get; set; } = null!;
        public bool IsActive { get; set; } = true;
    }

    public class JournalEntry
    {
        [Key] public int JournalEntryId { get; set; }
        public int BusinessId { get; set; }
        [ForeignKey("BusinessId")] public Business Business { get; set; } = null!;
        public int? ReferenceId { get; set; }
        [MaxLength(50)] public string? ReferenceType { get; set; }
        public DateTime EntryDate { get; set; }
        [MaxLength(255)] public string? Description { get; set; }
    }

    public class JournalEntryLine
    {
        [Key] public int JournalLineId { get; set; }
        public int JournalEntryId { get; set; }
        [ForeignKey("JournalEntryId")] public JournalEntry JournalEntry { get; set; } = null!;
        public int AccountId { get; set; }
        [ForeignKey("AccountId")] public ChartOfAccount ChartOfAccount { get; set; } = null!;
        [Column(TypeName = "decimal(18,2)")] public decimal Debit { get; set; } = 0;
        [Column(TypeName = "decimal(18,2)")] public decimal Credit { get; set; } = 0;
    }

    public class PendingRegistration
    {
        [Key] public int PendingRegistrationId { get; set; }
        [Required, MaxLength(36)] public string Token { get; set; } = null!;
        public int PlanId { get; set; }
        [ForeignKey("PlanId")] public SubscriptionPlan Plan { get; set; } = null!;
        [Required, MaxLength(100)] public string BusinessName { get; set; } = null!;
        [Required, MaxLength(50)] public string FirstName { get; set; } = null!;
        [Required, MaxLength(50)] public string LastName { get; set; } = null!;
        [Required, MaxLength(256)] public string EmailAddress { get; set; } = null!;
        [Required] public string PasswordHash { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool IsUsed { get; set; } = false;
    }
}