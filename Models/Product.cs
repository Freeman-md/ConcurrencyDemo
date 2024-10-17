namespace ConcurrencyDemo.Models
{
    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int StockQuantity { get; set; }

         // Concurrency token
        public int Version { get; set; }
    }
}
