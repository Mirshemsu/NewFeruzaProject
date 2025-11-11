using FeruzaShopProject.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FeruzaShopProject.Infrastructre.Data
{
    public class ShopDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>
    {
        public DbSet<Branch> Branches { get; set; }
        public DbSet<BankAccount> BankAccounts { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Stock> Stocks { get; set; }
        public DbSet<StockMovement> StockMovements { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Painter> Painters { get; set; }
        public DbSet<DailySales> DailySales { get; set; }
        public DbSet<CreditPayment> CreditPayments { get; set; }
        public DbSet<Supplier> Suppliers { get; set; }
        public DbSet<PurchaseOrder> PurchaseOrders { get; set; }
        public DbSet<PurchaseHistory> PurchaseHistory { get; set; }
        public DbSet<PurchaseOrderItem> PurchaseOrderItems { get; set; }

        public ShopDbContext(DbContextOptions<ShopDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // ✅ important line — required for Identity tables
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Branch>().HasQueryFilter(b => b.IsActive);
            modelBuilder.Entity<Category>().HasQueryFilter(c => c.IsActive);
            modelBuilder.Entity<Product>().HasQueryFilter(p => p.IsActive);
            modelBuilder.Entity<Stock>().HasQueryFilter(s => s.IsActive);
            modelBuilder.Entity<Customer>().HasQueryFilter(c => c.IsActive);
            modelBuilder.Entity<Painter>().HasQueryFilter(p => p.IsActive);

            // ✅ User configuration
            modelBuilder.Entity<User>()
                .Property(u => u.Role)
                .HasConversion<string>()
                .HasMaxLength(50);

            modelBuilder.Entity<User>()
                .HasDiscriminator<string>("UserType")
                .HasValue<BranchUser>("BranchUser")
                .HasValue<GlobalUser>("GlobalUser");

            // ✅ Branch configuration
            modelBuilder.Entity<Branch>()
               .HasIndex(b => b.Name)
               .IsUnique();

            // ✅ Product configuration
            modelBuilder.Entity<Product>(entity =>
            {
                entity.HasKey(p => p.Id);

                // Configure decimal precision
                entity.Property(p => p.Amount).HasPrecision(18, 3);
                entity.Property(p => p.BuyingPrice).HasPrecision(18, 2);
                entity.Property(p => p.UnitPrice).HasPrecision(18, 2);
                entity.Property(p => p.CommissionPerProduct).HasPrecision(18, 2);

                // Configure Category relationship
                entity.HasOne(p => p.Category)
                    .WithMany(c => c.Products)
                    .HasForeignKey(p => p.CategoryId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Configure Unit enum conversion
                entity.Property(p => p.Unit)
                    .HasConversion<string>()
                    .HasMaxLength(20);
            });

            // ✅ Stock configuration
            modelBuilder.Entity<Stock>(entity =>
            {
                entity.HasKey(s => s.Id);

                // Configure decimal precision
                entity.Property(s => s.Quantity).HasPrecision(18, 3);

                entity.HasOne(s => s.Product)
                    .WithMany(p => p.Stocks)
                    .HasForeignKey(s => s.ProductId)
                    .OnDelete(DeleteBehavior.Restrict); // Changed from Cascade

                entity.HasOne(s => s.Branch)
                    .WithMany(b => b.Stocks)
                    .HasForeignKey(s => s.BranchId)
                    .OnDelete(DeleteBehavior.Restrict); // Changed from Cascade

                // Add unique constraint
                entity.HasIndex(s => new { s.ProductId, s.BranchId })
                    .IsUnique();
            });

            // ✅ StockMovement configuration
            modelBuilder.Entity<StockMovement>(entity =>
            {
                entity.HasKey(sm => sm.Id);

                // Configure decimal precision
                entity.Property(sm => sm.Quantity).HasPrecision(18, 3);
                entity.Property(sm => sm.PreviousQuantity).HasPrecision(18, 3);
                entity.Property(sm => sm.NewQuantity).HasPrecision(18, 3);

                // Configure enum conversion
                entity.Property(sm => sm.MovementType)
                    .HasConversion<string>()
                    .HasMaxLength(20);

                // Relationships - ALL RESTRICT to avoid cascade paths
                entity.HasOne(sm => sm.Product)
                    .WithMany(p => p.StockMovements)
                    .HasForeignKey(sm => sm.ProductId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(sm => sm.Branch)
                    .WithMany(b => b.StockMovements)
                    .HasForeignKey(sm => sm.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(sm => sm.Transaction)
                    .WithMany(t => t.StockMovements)
                    .HasForeignKey(sm => sm.TransactionId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ✅ Transaction configuration
            modelBuilder.Entity<Transaction>(entity =>
            {
                entity.HasKey(t => t.Id);

                // Configure decimal precision
                entity.Property(t => t.UnitPrice).HasPrecision(18, 2);
                entity.Property(t => t.Quantity).HasPrecision(18, 3);
                entity.Property(t => t.CommissionRate).HasPrecision(18, 2);

                // Configure enum conversion
                entity.Property(t => t.PaymentMethod)
                    .HasConversion<string>()
                    .HasMaxLength(20);

                // Relationships - ALL RESTRICT to avoid cascade paths
                entity.HasOne(t => t.Branch)
                    .WithMany(b => b.Transactions)
                    .HasForeignKey(t => t.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(t => t.Product)
                    .WithMany(p => p.Transactions)
                    .HasForeignKey(t => t.ProductId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(t => t.Customer)
                    .WithMany(c => c.Transactions)
                    .HasForeignKey(t => t.CustomerId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(t => t.Painter)
                    .WithMany(p => p.Transactions)
                    .HasForeignKey(t => t.PainterId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Indexes
                entity.HasIndex(t => t.TransactionDate);
                entity.HasIndex(t => t.PaymentMethod);
                entity.HasIndex(t => new { t.BranchId, t.TransactionDate });
            });

            // ✅ DailySales configuration - FIXED CASCADE PATHS
            modelBuilder.Entity<DailySales>(entity =>
            {
                entity.HasKey(ds => ds.Id);

                // Configure decimal precision - FIXED Quantity precision
                entity.Property(ds => ds.Quantity).HasPrecision(18, 3); // Changed from 18,2 to 18,3
                entity.Property(ds => ds.UnitPrice).HasPrecision(18, 2);
                entity.Property(ds => ds.TotalAmount).HasPrecision(18, 2);
                entity.Property(ds => ds.CommissionRate).HasPrecision(18, 2);
                entity.Property(ds => ds.CommissionAmount).HasPrecision(18, 2);

                // Configure enum conversion - FIXED to string
                entity.Property(ds => ds.PaymentMethod)
                    .HasConversion<string>() // Changed from int to string
                    .HasMaxLength(20);

                // Relationships - ALL RESTRICT to avoid cascade paths
                entity.HasOne(ds => ds.Branch)
                    .WithMany(b => b.DailySales)
                    .HasForeignKey(ds => ds.BranchId)
                    .OnDelete(DeleteBehavior.Restrict); // Changed from Cascade

                entity.HasOne(ds => ds.Product)
                    .WithMany(p => p.DailySales)
                    .HasForeignKey(ds => ds.ProductId)
                    .OnDelete(DeleteBehavior.Restrict); // Changed from Cascade

                entity.HasOne(ds => ds.Transaction)
                    .WithMany(t => t.DailySales)
                    .HasForeignKey(ds => ds.TransactionId)
                    .OnDelete(DeleteBehavior.Restrict); // Changed from Cascade - THIS WAS THE MAIN ISSUE

                entity.HasOne(ds => ds.Customer)
                    .WithMany(c => c.DailySales)
                    .HasForeignKey(ds => ds.CustomerId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(ds => ds.Painter)
                    .WithMany(p => p.DailySales)
                    .HasForeignKey(ds => ds.PainterId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Indexes
                entity.HasIndex(ds => ds.SaleDate);
                entity.HasIndex(ds => new { ds.BranchId, ds.SaleDate });
                entity.HasIndex(ds => new { ds.SaleDate, ds.PaymentMethod });
            });

            // ✅ CreditPayment configuration
            modelBuilder.Entity<CreditPayment>(entity =>
            {
                entity.HasKey(cp => cp.Id);

                // Configure decimal precision
                entity.Property(cp => cp.Amount).HasPrecision(18, 2);

                // Configure enum conversion
                entity.Property(cp => cp.PaymentMethod)
                    .HasConversion<string>()
                    .HasMaxLength(20);

                // Relationships
                entity.HasOne(cp => cp.Transaction)
                    .WithMany(t => t.CreditPayments)
                    .HasForeignKey(cp => cp.TransactionId)
                    .OnDelete(DeleteBehavior.Restrict); // Changed from Cascade

                // Indexes
                entity.HasIndex(cp => cp.PaymentDate);
                entity.HasIndex(cp => cp.TransactionId);
            });

            // ✅ Customer configuration
            modelBuilder.Entity<Customer>(entity =>
            {
                entity.HasKey(c => c.Id);
                entity.Property(c => c.Name).HasMaxLength(200).IsRequired();
                entity.Property(c => c.PhoneNumber).HasMaxLength(20).IsRequired();

                // Index for quick lookup by phone
                entity.HasIndex(c => c.PhoneNumber).IsUnique();
            });

            // ✅ Painter configuration
            modelBuilder.Entity<Painter>(entity =>
            {
                entity.HasKey(p => p.Id);
                entity.Property(p => p.Name).HasMaxLength(200).IsRequired();
                entity.Property(p => p.PhoneNumber).HasMaxLength(20).IsRequired();

                // Index for quick lookup by phone
                entity.HasIndex(p => p.PhoneNumber).IsUnique();
            });

            // ✅ BankAccount configuration
            modelBuilder.Entity<BankAccount>(entity =>
            {
                entity.HasKey(ba => ba.Id);
                entity.Property(ba => ba.AccountNumber).HasMaxLength(50).IsRequired();
                entity.Property(ba => ba.BankName).HasMaxLength(100).IsRequired();
                entity.Property(ba => ba.AccountOwner).HasMaxLength(200).IsRequired();

                // Relationship with Branch
                entity.HasOne(ba => ba.Branch)
                    .WithMany(b => b.BankAccounts)
                    .HasForeignKey(ba => ba.BranchId)
                    .OnDelete(DeleteBehavior.Restrict); // Changed from Cascade
            });
            modelBuilder.Entity<Supplier>().HasQueryFilter(s => s.IsActive);
            modelBuilder.Entity<Supplier>()
               .HasIndex(s => s.Name)
               .IsUnique();
            modelBuilder.Entity<PurchaseOrder>()
               .HasIndex(po => po.Id)
               .IsUnique();
            modelBuilder.Entity<PurchaseOrderItem>()
                .Property(p => p.UnitPrice)
                .HasPrecision(18, 2);
            modelBuilder.Entity<PurchaseOrder>()
               .Property(po => po.BranchId)
               .IsRequired();

            modelBuilder.Entity<PurchaseOrder>()
                .HasOne(po => po.Supplier)
                .WithMany(s => s.PurchaseOrders)
                .HasForeignKey(po => po.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PurchaseOrder>()
                .HasOne(po => po.Creator)
                .WithMany(u => u.CreatedPurchaseOrders)
                .HasForeignKey(po => po.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PurchaseOrderItem>()
                .HasOne(poi => poi.Product)
                .WithMany(p => p.PurchaseOrderItems)
                .HasForeignKey(poi => poi.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
            // ✅ BranchUser configuration
            modelBuilder.Entity<BranchUser>()
                .HasOne(bu => bu.Branch)
                .WithMany(b => b.Users)
                .HasForeignKey(bu => bu.BranchId)
                .OnDelete(DeleteBehavior.Restrict);

            // ✅ Category configuration
            modelBuilder.Entity<Category>(entity =>
            {
                entity.HasKey(c => c.Id);
                entity.Property(c => c.Name).HasMaxLength(100).IsRequired();
                entity.HasIndex(c => c.Name).IsUnique();
            });
        }
    }
}