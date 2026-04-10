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
            
            modelBuilder.Entity<Order>().Property(o => o.OrderTime).HasDefaultValueSql("GETDATE()");

            modelBuilder.Entity<InventoryTransaction>().Property(i => i.CreatedAt).HasDefaultValueSql("GETDATE()");
            modelBuilder.Entity<InventoryTransaction>().HasIndex(i => new { i.BusinessId, i.ItemId, i.CreatedAt });
            
            modelBuilder.Entity<Invoice>().Property(i => i.GeneratedDate).HasDefaultValueSql("GETDATE()");
            
            modelBuilder.Entity<Payment>().Property(p => p.PaymentDate).HasDefaultValueSql("GETDATE()");
            
            modelBuilder.Entity<JournalEntry>().Property(j => j.EntryDate).HasDefaultValueSql("GETDATE()");
        }
    }
}
