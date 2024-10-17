using Xunit;
using ConcurrencyDemo.Models;
using ConcurrencyDemo.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using Microsoft.Data.Sqlite;

namespace ConcurrencyDemo.Tests
{
    public class ConcurrencyTests
    {
        private readonly DbContextOptions<AppDbContext> _options;
        private readonly SqliteConnection _connection;

        public ConcurrencyTests()
        {
            // Set up SQLite in-memory database with a persistent connection
            _connection = new SqliteConnection("Filename=:memory:");
            _connection.Open();  // Keep the connection open for in-memory database

            _options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_connection)
                .Options;

            // Create schema and seed the database
            using var context = new AppDbContext(_options);
            context.Database.EnsureCreated();  // Ensure that the schema is created

            context.Database.ExecuteSqlRaw(@"
                CREATE TRIGGER SetProductVersionOnUpdate
                AFTER UPDATE ON Products
                BEGIN
                    UPDATE Products
                    SET Version = Version + 1
                    WHERE rowid = NEW.rowid;
                END;
            ");


            // Seed initial data if necessary
            if (!context.Products.Any())
            {
                context.Products.Add(new Product
                {
                    Id = 1,
                    Name = "Sample Product",
                    StockQuantity = 10
                });
                context.SaveChanges();
            }
        }

        // [Fact]
        // public void ConcurrencyIssue_ShouldOverwriteWithoutWarning()
        // {
        //     // First context simulating first user
        //     using var context1 = new AppDbContext(_options);
        //     var product1 = context1.Products.First(p => p.Id == 1);

        //     // Second context simulating second user
        //     using var context2 = new AppDbContext(_options);
        //     var product2 = context2.Products.First(p => p.Id == 1);

        //     // First user updates the product
        //     product1.StockQuantity = 20;
        //     context1.SaveChanges();

        //     // Second user updates the product
        //     product2.StockQuantity = 30;
        //     context2.SaveChanges();

        //     // Verify the final StockQuantity
        //     using var verificationContext = new AppDbContext(_options);
        //     var finalProduct = verificationContext.Products.First(p => p.Id == 1);

        //     // The final StockQuantity will be 30, overwriting the first user's changes
        //     Assert.Equal(30, finalProduct.StockQuantity);
        // }

        [Fact]
        public void ConcurrencyIssue_ShouldThrowException()
        {
            // First context simulating first user
            using var context1 = new AppDbContext(_options);
            var product1 = context1.Products.First(p => p.Id == 1);

            // Second context simulating second user
            using var context2 = new AppDbContext(_options);
            var product2 = context2.Products.First(p => p.Id == 1);

            // First user updates the product
            product1.StockQuantity = 20;
            context1.SaveChanges();

            // Second user attempts to update and save
            product2.StockQuantity = 30;

            // Expect a concurrency exception (this will fail)
            Assert.Throws<DbUpdateConcurrencyException>(() => context2.SaveChanges());
        }

        [Fact]
        public void ConcurrencyIssue_HandleConflictGracefully()
        {
            // First context simulating first user
            using var context1 = new AppDbContext(_options);
            var product1 = context1.Products.First(p => p.Id == 1);

            // Second context simulating second user
            using var context2 = new AppDbContext(_options);
            var product2 = context2.Products.First(p => p.Id == 1);

            // First user updates the product
            product1.StockQuantity = 20;
            context1.SaveChanges();

            // Second user attempts to update and save
            product2.StockQuantity = 30;

            try
            {
                context2.SaveChanges();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                // Simulate informing the user about the conflict
                Console.WriteLine("A concurrency conflict occurred. The data has been modified by another user.");

                // In a real application, you would inform the user and reload the latest data:
                var entry = ex.Entries.Single();
                var currentDatabaseValues = entry.GetDatabaseValues();

                // Log the current database values
                Console.WriteLine($"Current Database Stock Quantity: {(currentDatabaseValues.ToObject() as Product).StockQuantity}");

                // Refresh original values from the database
                entry.OriginalValues.SetValues(currentDatabaseValues);

                // Update only the StockQuantity property for retry
                entry.CurrentValues["StockQuantity"] = 35; // Simulating user's decision to retry with new value

                // Retry the save operation with the updated StockQuantity
                context2.SaveChanges(); // Try again with the updated value
            }

            // Verify the final StockQuantity
            using var verificationContext = new AppDbContext(_options);
            var finalProduct = verificationContext.Products.First(p => p.Id == 1);

            // The final StockQuantity should now be 35, reflecting the user's retried update
            Assert.Equal(35, finalProduct.StockQuantity);
        }


    }
}
