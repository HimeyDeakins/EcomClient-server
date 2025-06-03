
namespace ShopSystem
{
    public class Product
    {
        public string ProductId { get; private set; }
        public string Name { get; set; }
        public decimal Price { get; set; } // Use decimal for currency
        public int QuantityAvailable { get; private set; }

        public Product(string productId, string name, decimal price, int quantityAvailable)
        {
            ProductId = productId;
            Name = name;
            Price = price;
            QuantityAvailable = quantityAvailable;
        }

        public override string ToString()
        {
            return $"ID: {ProductId}, Name: {Name}, Price: {Price:C}, Stock: {QuantityAvailable}"; // :C for currency format
        }

        public bool UpdateStock(int quantityChange)
        {
            if (QuantityAvailable + quantityChange < 0)
            {
                Console.WriteLine($"Error: Not enough stock for {Name} to reduce by {-quantityChange}.");
                return false;
            }
            QuantityAvailable += quantityChange;
            return true;
        }
    }
}