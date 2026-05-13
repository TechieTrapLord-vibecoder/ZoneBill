using System.Linq;
using Microsoft.EntityFrameworkCore;
using ZoneBill_Lloren.Models;

namespace ZoneBill_Lloren.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<SubscriptionPlan> SubscriptionPlans { get; set; }
        public DbSet<SubscriptionInvoice> SubscriptionInvoices { get; set; }
        public DbSet<Business> Businesses { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Space> Spaces { get; set; }
        public DbSet<Booking> Bookings { get; set; }
        public DbSet<PosShift> PosShifts { get; set; }
        public DbSet<CashDrawerTransaction> CashDrawerTransactions { get; set; }
        public DbSet<PosAuditLog> PosAuditLogs { get; set; }
        public DbSet<MenuItem> MenuItems { get; set; }
        public DbSet<InventoryTransaction> InventoryTransactions { get; set; }
        public DbSet<InventoryAlertLog> InventoryAlertLogs { get; set; }
        public DbSet<Supplier> Suppliers { get; set; }
        public DbSet<PurchaseOrder> PurchaseOrders { get; set; }
        public DbSet<PurchaseOrderLine> PurchaseOrderLines { get; set; }
        public DbSet<PurchaseOrderReceipt> PurchaseOrderReceipts { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderDetail> OrderDetails { get; set; }
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<InvoiceItem> InvoiceItems { get; set; }
        public DbSet<Adjustment> Adjustments { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<ChartOfAccount> ChartOfAccounts { get; set; }
        public DbSet<JournalEntry> JournalEntries { get; set; }
        public DbSet<JournalEntryLine> JournalEntryLines { get; set; }
        public DbSet<PendingRegistration> PendingRegistrations { get; set; }
        public DbSet<BusinessLifecycleEvent> BusinessLifecycleEvents { get; set; }
        public DbSet<SuperAdminAuditLog> SuperAdminAuditLogs { get; set; }
        public DbSet<TenantAuditLog> TenantAuditLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            // Disable cascading deletes globally to prevent cycles due to the many BusinessId relationships
            foreach (var relationship in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
            {
                relationship.DeleteBehavior = DeleteBehavior.Restrict;
            }

            // Defaults and Unique Constraints
            modelBuilder.Entity<Business>().HasIndex(b => b.DomainPrefix).IsUnique();
            modelBuilder.Entity<Business>().Property(b => b.CreatedAt).HasDefaultValueSql("GETDATE()");
            modelBuilder.Entity<Business>().Property(b => b.InventoryAlertEnabled).HasDefaultValue(true);
            modelBuilder.Entity<Business>().Property(b => b.InventoryReorderLookbackDays).HasDefaultValue(30);
            modelBuilder.Entity<Business>().Property(b => b.InventoryLeadTimeDays).HasDefaultValue(3);
            modelBuilder.Entity<Business>().Property(b => b.InventorySafetyStockDays).HasDefaultValue(2);
            modelBuilder.Entity<Business>().Property(b => b.InventoryTargetCoverageDays).HasDefaultValue(7);
            modelBuilder.Entity<Business>().Property(b => b.InventoryForecastLookbackDays).HasDefaultValue(28);
            modelBuilder.Entity<Business>().Property(b => b.InventoryForecastHorizonDays).HasDefaultValue(7);
            
            modelBuilder.Entity<User>().HasIndex(u => u.EmailAddress).IsUnique();
            
            modelBuilder.Entity<Customer>().Property(c => c.CreatedAt).HasDefaultValueSql("GETDATE()");

            modelBuilder.Entity<Space>().Property(s => s.FloorArea).HasDefaultValue("Main Floor");
            modelBuilder.Entity<Space>().Property(s => s.Capacity).HasDefaultValue(4);
            modelBuilder.Entity<Space>().HasIndex(s => new { s.BusinessId, s.FloorArea, s.CurrentStatus });

            modelBuilder.Entity<PosShift>().Property(s => s.OpenedAt).HasDefaultValueSql("GETDATE()");
            modelBuilder.Entity<PosShift>().Property(s => s.Status).HasDefaultValue("Open");
            modelBuilder.Entity<PosShift>().HasIndex(s => new { s.BusinessId, s.CashierId, s.Status, s.OpenedAt });

            modelBuilder.Entity<CashDrawerTransaction>().Property(t => t.CreatedAt).HasDefaultValueSql("GETDATE()");
            modelBuilder.Entity<CashDrawerTransaction>().HasIndex(t => new { t.BusinessId, t.ShiftId, t.CreatedAt });

            modelBuilder.Entity<PosAuditLog>().Property(a => a.CreatedAt).HasDefaultValueSql("GETDATE()");
            modelBuilder.Entity<PosAuditLog>().HasIndex(a => new { a.BusinessId, a.ActionType, a.CreatedAt });
            modelBuilder.Entity<PosAuditLog>().HasIndex(a => new { a.BusinessId, a.CashierId, a.CreatedAt });

            modelBuilder.Entity<MenuItem>().Property(m => m.LowStockThreshold).HasDefaultValue(5);
            modelBuilder.Entity<MenuItem>().Property(m => m.CostPrice).HasDefaultValue(0m);
            modelBuilder.Entity<MenuItem>().Property(m => m.Category).HasDefaultValue("General");
            modelBuilder.Entity<MenuItem>().Property(m => m.SortOrder).HasDefaultValue(0);
            modelBuilder.Entity<MenuItem>().HasIndex(m => new { m.BusinessId, m.Category, m.SortOrder, m.ItemName });
            
            modelBuilder.Entity<Order>().Property(o => o.OrderTime).HasDefaultValueSql("GETDATE()");

            modelBuilder.Entity<InventoryTransaction>().Property(i => i.CreatedAt).HasDefaultValueSql("GETDATE()");
            modelBuilder.Entity<InventoryTransaction>().HasIndex(i => new { i.BusinessId, i.ItemId, i.CreatedAt });

            modelBuilder.Entity<InventoryAlertLog>().Property(a => a.SentAt).HasDefaultValueSql("GETDATE()");
            modelBuilder.Entity<InventoryAlertLog>().HasIndex(a => new { a.BusinessId, a.SentAt });
            modelBuilder.Entity<InventoryAlertLog>().HasIndex(a => new { a.BusinessId, a.AlertType, a.AlertSignature, a.SentAt });

            modelBuilder.Entity<Supplier>().Property(s => s.CreatedAt).HasDefaultValueSql("GETDATE()");
            modelBuilder.Entity<Supplier>().HasIndex(s => new { s.BusinessId, s.SupplierName }).IsUnique();

            modelBuilder.Entity<PurchaseOrder>().Property(p => p.CreatedAt).HasDefaultValueSql("GETDATE()");
            modelBuilder.Entity<PurchaseOrder>().Property(p => p.Status).HasDefaultValue("Draft");
            modelBuilder.Entity<PurchaseOrder>().HasIndex(p => new { p.BusinessId, p.PurchaseOrderNumber }).IsUnique();
            modelBuilder.Entity<PurchaseOrder>().HasIndex(p => new { p.BusinessId, p.Status, p.CreatedAt });

            modelBuilder.Entity<PurchaseOrderLine>().Property(p => p.ReceivedQuantity).HasDefaultValue(0);
            modelBuilder.Entity<PurchaseOrderLine>().HasIndex(p => new { p.PurchaseOrderId, p.ItemId });

            modelBuilder.Entity<PurchaseOrderReceipt>().Property(r => r.ReceivedAt).HasDefaultValueSql("GETDATE()");
            modelBuilder.Entity<PurchaseOrderReceipt>().HasIndex(r => new { r.PurchaseOrderId, r.ReceivedAt });
            modelBuilder.Entity<PurchaseOrderReceipt>().HasIndex(r => new { r.BusinessId, r.ItemId, r.ReceivedAt });
            
            modelBuilder.Entity<Invoice>().Property(i => i.GeneratedDate).HasDefaultValueSql("GETDATE()");
            
            modelBuilder.Entity<Payment>().Property(p => p.PaymentDate).HasDefaultValueSql("GETDATE()");
            
            modelBuilder.Entity<JournalEntry>().Property(j => j.EntryDate).HasDefaultValueSql("GETDATE()");

            modelBuilder.Entity<BusinessLifecycleEvent>().Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");
            modelBuilder.Entity<BusinessLifecycleEvent>().HasIndex(e => new { e.BusinessId, e.CreatedAt });
            modelBuilder.Entity<BusinessLifecycleEvent>().HasIndex(e => new { e.EventType, e.CreatedAt });

            modelBuilder.Entity<SuperAdminAuditLog>().Property(a => a.CreatedAt).HasDefaultValueSql("GETDATE()");
            modelBuilder.Entity<SuperAdminAuditLog>().HasIndex(a => new { a.ActionType, a.CreatedAt });
            modelBuilder.Entity<SuperAdminAuditLog>().HasIndex(a => new { a.EntityType, a.CreatedAt });
        }
    }
}
