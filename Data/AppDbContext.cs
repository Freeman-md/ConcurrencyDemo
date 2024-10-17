using Microsoft.EntityFrameworkCore;
using ConcurrencyDemo.Models;

namespace ConcurrencyDemo.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<Product> Products { get; set; }

        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Product>()
                .Property(p => p.Version)
                .IsConcurrencyToken()
                .HasDefaultValue(0)
                .ValueGeneratedOnAddOrUpdate();
        }

    }
}
