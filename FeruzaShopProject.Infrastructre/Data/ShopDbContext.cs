using FeruzaShopProject.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FeruzaShopProject.Infrastructre.Data
{
    public class ShopDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>
    {
        public DbSet<Branch> Branches { get; set; }
        public ShopDbContext(DbContextOptions<ShopDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // ✅ important line — required for Identity tables
            base.OnModelCreating(modelBuilder);

            // ✅ your custom config
            modelBuilder.Entity<User>()
                .Property(u => u.Role)
                .HasConversion<string>()
                .HasMaxLength(50);

            modelBuilder.Entity<User>()
            .HasDiscriminator<string>("UserType")
            .HasValue<BranchUser>("BranchUser")
            .HasValue<GlobalUser>("GlobalUser");

            modelBuilder.Entity<Branch>()
               .HasIndex(b => b.Name)
               .IsUnique();


            modelBuilder.Entity<Branch>().HasQueryFilter(b => b.IsActive);
        }
    }
}
