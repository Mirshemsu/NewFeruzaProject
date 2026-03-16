using FeruzaShopProject.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FeruzaShopProject.Infrastructre.Data
{
    public class ShopDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>
    {
        public DbSet<Branch> Branches { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Stock> Stocks { get; set; }
        public DbSet<StockMovement> StockMovements { get; set; }
        public DbSet<ProductExchange> ProductExchanges { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Painter> Painters { get; set; }
        public DbSet<DailySales> DailySales { get; set; }
        public DbSet<CreditPayment> CreditPayments { get; set; }
        public DbSet<PurchaseOrder> PurchaseOrders { get; set; }
        public DbSet<PurchaseHistory> PurchaseHistory { get; set; }
        public DbSet<PurchaseOrderItem> PurchaseOrderItems { get; set; }
        public DbSet<DailyClosing> DailyClosings { get; set; }

        public ShopDbContext(DbContextOptions<ShopDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // ✅ important line — required for Identity tables
            base.OnModelCreating(modelBuilder);

            // ========== GLOBAL QUERY FILTERS ==========
            modelBuilder.Entity<Branch>().HasQueryFilter(b => b.IsActive);
            modelBuilder.Entity<Category>().HasQueryFilter(c => c.IsActive);
            modelBuilder.Entity<Product>().HasQueryFilter(p => p.IsActive);
            modelBuilder.Entity<Stock>().HasQueryFilter(s => s.IsActive);
            modelBuilder.Entity<Customer>().HasQueryFilter(c => c.IsActive);
            modelBuilder.Entity<Painter>().HasQueryFilter(p => p.IsActive);
            modelBuilder.Entity<ProductExchange>().HasQueryFilter(pe => pe.IsActive);

            // ========== USER CONFIGURATION ==========
            modelBuilder.Entity<User>(entity =>
            {
                entity.Property(u => u.Role)
                    .HasConversion<string>()
                    .HasMaxLength(50);

                entity.HasDiscriminator<string>("UserType")
                    .HasValue<BranchUser>("BranchUser")
                    .HasValue<GlobalUser>("GlobalUser");
            });

            // ========== BRANCH CONFIGURATION ==========
            modelBuilder.Entity<Branch>(entity =>
            {
                entity.HasKey(b => b.Id);
                entity.HasIndex(b => b.Name).IsUnique();
            });

            // ========== CATEGORY CONFIGURATION ==========
            modelBuilder.Entity<Category>(entity =>
            {
                entity.HasKey(c => c.Id);
                entity.Property(c => c.Name).HasMaxLength(100).IsRequired();
                entity.HasIndex(c => c.Name).IsUnique();
            });

            // ========== PRODUCT CONFIGURATION ==========
            modelBuilder.Entity<Product>(entity =>
            {
                entity.HasKey(p => p.Id);

                // Configure decimal precision
                entity.Property(p => p.Amount).HasPrecision(18, 3);
                entity.Property(p => p.BuyingPrice).HasPrecision(18, 2);
                entity.Property(p => p.UnitPrice).HasPrecision(18, 2);
                entity.Property(p => p.CommissionPerProduct).HasPrecision(18, 2);

                // Configure Unit enum conversion
                entity.Property(p => p.Unit)
                    .HasConversion<string>()
                    .HasMaxLength(20);

                // Configure Category relationship
                entity.HasOne(p => p.Category)
                    .WithMany(c => c.Products)
                    .HasForeignKey(p => p.CategoryId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Configure exchange relationships - THESE ARE CRITICAL
                entity.HasMany(p => p.OriginalExchanges)
                    .WithOne(e => e.OriginalProduct)
                    .HasForeignKey(e => e.OriginalProductId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasMany(p => p.NewExchanges)
                    .WithOne(e => e.NewProduct)
                    .HasForeignKey(e => e.NewProductId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ========== PRODUCT EXCHANGE CONFIGURATION ==========
            modelBuilder.Entity<ProductExchange>(entity =>
            {
                entity.HasKey(pe => pe.Id);

                // Configure decimal precision
                entity.Property(pe => pe.OriginalQuantity).HasPrecision(18, 3);
                entity.Property(pe => pe.OriginalPrice).HasPrecision(18, 2);
                entity.Property(pe => pe.NewQuantity).HasPrecision(18, 3);
                entity.Property(pe => pe.NewPrice).HasPrecision(18, 2);

                // Configure relationship with Original Transaction
                entity.HasOne(pe => pe.OriginalTransaction)
                    .WithMany(t => t.Exchanges)
                    .HasForeignKey(pe => pe.OriginalTransactionId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Configure relationship with Original Product - FIXED
                entity.HasOne(pe => pe.OriginalProduct)
                    .WithMany(p => p.OriginalExchanges)
                    .HasForeignKey(pe => pe.OriginalProductId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Configure relationship with New Product - FIXED
                entity.HasOne(pe => pe.NewProduct)
                    .WithMany(p => p.NewExchanges)
                    .HasForeignKey(pe => pe.NewProductId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Indexes
                entity.HasIndex(pe => pe.OriginalTransactionId);
                entity.HasIndex(pe => pe.OriginalProductId);
                entity.HasIndex(pe => pe.NewProductId);
                entity.HasIndex(pe => pe.CreatedAt);
            });

            // ========== PURCHASE ORDER CONFIGURATION ==========
            modelBuilder.Entity<PurchaseOrder>(entity =>
            {
                entity.HasKey(po => po.Id);
                entity.HasIndex(po => po.Id).IsUnique();
                entity.Property(po => po.BranchId).IsRequired();
                entity.Property(po => po.InvoiceNumber).HasMaxLength(50);

                // Configure Creator relationship
                entity.HasOne(po => po.Creator)
                    .WithMany(u => u.CreatedPurchaseOrders)
                    .HasForeignKey(po => po.CreatedBy)
                    .OnDelete(DeleteBehavior.Restrict);

                // Configure FinanceVerifier relationship - FIXED (with no reverse navigation)
                entity.HasOne(po => po.FinanceVerifier)
                    .WithMany() // No reverse navigation needed
                    .HasForeignKey(po => po.FinanceVerifiedBy)
                    .OnDelete(DeleteBehavior.Restrict)
                    .IsRequired(false);

                // Configure Approver relationship - FIXED (with no reverse navigation)
                entity.HasOne(po => po.Approver)
                    .WithMany() // No reverse navigation needed
                    .HasForeignKey(po => po.ApprovedBy)
                    .OnDelete(DeleteBehavior.Restrict)
                    .IsRequired(false);

                // Configure Status enum conversion
                entity.Property(po => po.Status)
                    .HasConversion<string>()
                    .HasMaxLength(30);
            });

            // ========== PURCHASE ORDER ITEM CONFIGURATION ==========
            modelBuilder.Entity<PurchaseOrderItem>(entity =>
            {
                entity.HasKey(poi => poi.Id);

                entity.Property(poi => poi.Quantity).IsRequired();
                entity.Property(poi => poi.BuyingPrice).HasPrecision(18, 2);
                entity.Property(poi => poi.UnitPrice).HasPrecision(18, 2);
                entity.Property(poi => poi.SupplierName).HasMaxLength(200);

                // Relationship with PurchaseOrder
                entity.HasOne(poi => poi.PurchaseOrder)
                    .WithMany(po => po.Items)
                    .HasForeignKey(poi => poi.PurchaseOrderId)
                    .OnDelete(DeleteBehavior.Cascade); // Cascade is safe here

                // Relationship with Product
                entity.HasOne(poi => poi.Product)
                    .WithMany(p => p.PurchaseOrderItems)
                    .HasForeignKey(poi => poi.ProductId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ========== PURCHASE HISTORY CONFIGURATION ==========
            modelBuilder.Entity<PurchaseHistory>(entity =>
            {
                entity.HasKey(ph => ph.Id);

                entity.Property(ph => ph.Action).HasMaxLength(100).IsRequired();
                entity.Property(ph => ph.Details).HasMaxLength(500);

                entity.HasOne(ph => ph.PurchaseOrder)
                    .WithMany(po => po.History)
                    .HasForeignKey(ph => ph.PurchaseOrderId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(ph => ph.PerformedByUser)
                    .WithMany()
                    .HasForeignKey(ph => ph.PerformedByUserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(ph => ph.PurchaseOrderItem)
                    .WithMany()
                    .HasForeignKey(ph => ph.PurchaseOrderItemId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .IsRequired(false);
            });

            // ========== DAILY CLOSING CONFIGURATION ==========
            modelBuilder.Entity<DailyClosing>(entity =>
            {
                entity.HasKey(dc => dc.Id);

                entity.Property(dc => dc.TotalCashAmount).HasPrecision(18, 2);
                entity.Property(dc => dc.TotalBankAmount).HasPrecision(18, 2);
                entity.Property(dc => dc.TotalCreditAmount).HasPrecision(18, 2);
                entity.Property(dc => dc.TotalSalesAmount).HasPrecision(18, 2);
                entity.Property(dc => dc.CashBankTransactionId).HasMaxLength(100);
                entity.Property(dc => dc.BankTransferTransactionId).HasMaxLength(100);

                // Configure Status enum conversion
                entity.Property(dc => dc.Status)
                    .HasConversion<string>()
                    .HasMaxLength(20);

                // Relationships
                entity.HasOne(dc => dc.Closer)
                    .WithMany()
                    .HasForeignKey(dc => dc.ClosedBy)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(dc => dc.Approver)
                    .WithMany()
                    .HasForeignKey(dc => dc.ApprovedBy)
                    .OnDelete(DeleteBehavior.Restrict)
                    .IsRequired(false);

                entity.HasOne(dc => dc.Branch)
                    .WithMany()
                    .HasForeignKey(dc => dc.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ========== STOCK CONFIGURATION ==========
            modelBuilder.Entity<Stock>(entity =>
            {
                entity.HasKey(s => s.Id);
                entity.Property(s => s.Quantity).HasPrecision(18, 3);

                entity.HasOne(s => s.Product)
                    .WithMany(p => p.Stocks)
                    .HasForeignKey(s => s.ProductId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(s => s.Branch)
                    .WithMany(b => b.Stocks)
                    .HasForeignKey(s => s.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(s => new { s.ProductId, s.BranchId }).IsUnique();
            });

            // ========== STOCK MOVEMENT CONFIGURATION ==========
            modelBuilder.Entity<StockMovement>(entity =>
            {
                entity.HasKey(sm => sm.Id);
                entity.Property(sm => sm.Quantity).HasPrecision(18, 3);
                entity.Property(sm => sm.PreviousQuantity).HasPrecision(18, 3);
                entity.Property(sm => sm.NewQuantity).HasPrecision(18, 3);

                entity.Property(sm => sm.MovementType)
                    .HasConversion<string>()
                    .HasMaxLength(20);

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

            // ========== TRANSACTION CONFIGURATION ==========
            modelBuilder.Entity<Transaction>(entity =>
            {
                entity.HasKey(t => t.Id);
                entity.Property(t => t.UnitPrice).HasPrecision(18, 2);
                entity.Property(t => t.Quantity).HasPrecision(18, 3);
                entity.Property(t => t.CommissionRate).HasPrecision(18, 2);
                entity.Property(t => t.ItemCode).HasMaxLength(50).IsRequired();
                entity.Property(t => t.Remark).HasMaxLength(500);

                entity.Property(t => t.PaymentMethod)
                    .HasConversion<string>()
                    .HasMaxLength(20);

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

                entity.HasIndex(t => t.TransactionDate);
                entity.HasIndex(t => t.PaymentMethod);
                entity.HasIndex(t => new { t.BranchId, t.TransactionDate });
            });

            // ========== DAILY SALES CONFIGURATION ==========
            modelBuilder.Entity<DailySales>(entity =>
            {
                entity.HasKey(ds => ds.Id);
                entity.Property(ds => ds.Quantity).HasPrecision(18, 3);
                entity.Property(ds => ds.UnitPrice).HasPrecision(18, 2);
                entity.Property(ds => ds.TotalAmount).HasPrecision(18, 2);
                entity.Property(ds => ds.CommissionRate).HasPrecision(18, 2);
                entity.Property(ds => ds.CommissionAmount).HasPrecision(18, 2);
                entity.Property(ds => ds.Remark).HasMaxLength(500);

                entity.Property(ds => ds.PaymentMethod)
                    .HasConversion<string>()
                    .HasMaxLength(20);

                entity.HasOne(ds => ds.Branch)
                    .WithMany(b => b.DailySales)
                    .HasForeignKey(ds => ds.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(ds => ds.Product)
                    .WithMany(p => p.DailySales)
                    .HasForeignKey(ds => ds.ProductId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(ds => ds.Transaction)
                    .WithMany(t => t.DailySales)
                    .HasForeignKey(ds => ds.TransactionId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(ds => ds.Customer)
                    .WithMany(c => c.DailySales)
                    .HasForeignKey(ds => ds.CustomerId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(ds => ds.Painter)
                    .WithMany(p => p.DailySales)
                    .HasForeignKey(ds => ds.PainterId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(ds => ds.SaleDate);
                entity.HasIndex(ds => new { ds.BranchId, ds.SaleDate });
            });

            // ========== CREDIT PAYMENT CONFIGURATION ==========
            modelBuilder.Entity<CreditPayment>(entity =>
            {
                entity.HasKey(cp => cp.Id);
                entity.Property(cp => cp.Amount).HasPrecision(18, 2);
                entity.Property(cp => cp.Remark).HasMaxLength(500);

                entity.Property(cp => cp.PaymentMethod)
                    .HasConversion<string>()
                    .HasMaxLength(20);

                entity.HasOne(cp => cp.Transaction)
                    .WithMany(t => t.CreditPayments)
                    .HasForeignKey(cp => cp.TransactionId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(cp => cp.PaymentDate);
                entity.HasIndex(cp => cp.TransactionId);
            });

            // ========== CUSTOMER CONFIGURATION ==========
            modelBuilder.Entity<Customer>(entity =>
            {
                entity.HasKey(c => c.Id);
                entity.Property(c => c.Name).HasMaxLength(200).IsRequired();
                entity.Property(c => c.PhoneNumber).HasMaxLength(20).IsRequired();
                entity.HasIndex(c => c.PhoneNumber).IsUnique();
            });

            // ========== PAINTER CONFIGURATION ==========
            modelBuilder.Entity<Painter>(entity =>
            {
                entity.HasKey(p => p.Id);
                entity.Property(p => p.Name).HasMaxLength(200).IsRequired();
                entity.Property(p => p.PhoneNumber).HasMaxLength(20).IsRequired();
                entity.HasIndex(p => p.PhoneNumber).IsUnique();
            });

        

            // ========== BRANCH USER CONFIGURATION ==========
            modelBuilder.Entity<BranchUser>(entity =>
            {
                entity.HasOne(bu => bu.Branch)
                    .WithMany(b => b.Users)
                    .HasForeignKey(bu => bu.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}