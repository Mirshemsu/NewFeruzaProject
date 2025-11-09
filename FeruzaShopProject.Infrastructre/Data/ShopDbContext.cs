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


        public ShopDbContext(DbContextOptions<ShopDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // ✅ important line — required for Identity tables
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Branch>().HasQueryFilter(b => b.IsActive);
            modelBuilder.Entity<Category>().HasQueryFilter(c => c.IsActive);
            modelBuilder.Entity<Product>().HasQueryFilter(p => p.IsActive);
            modelBuilder.Entity<Stock>().HasQueryFilter(s => s.IsActive);
  

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

            // ✅ Product configuration - ONLY ONCE
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
                    .WithMany(c => c.Products) // Add this if Category has Products collection
                    .HasForeignKey(p => p.CategoryId)
                    .OnDelete(DeleteBehavior.Restrict);
            });



            // ✅ Stock configuration
            modelBuilder.Entity<Stock>(entity =>
            {
                entity.HasKey(s => s.Id);



                entity.HasOne(s => s.Product)
                    .WithMany(p => p.Stocks)
                    .HasForeignKey(s => s.ProductId)
                    .OnDelete(DeleteBehavior.Cascade); // Changed back to Cascade for proper deletion

                // Add unique constraint
                entity.HasIndex(s => new { s.ProductId, s.BranchId })
                    .IsUnique();
            });

            // ✅ BranchUser configuration
            modelBuilder.Entity<BranchUser>()
                .HasOne(bu => bu.Branch)
                .WithMany(b => b.Users)
                .HasForeignKey(bu => bu.BranchId)
                .OnDelete(DeleteBehavior.Restrict);

            // ✅ Category configuration (add this)
            modelBuilder.Entity<Category>(entity =>
            {
                entity.HasKey(c => c.Id);
                entity.Property(c => c.Name).HasMaxLength(100).IsRequired();
                entity.HasIndex(c => c.Name).IsUnique(); // Optional: unique category names
            });
        }
    }
}