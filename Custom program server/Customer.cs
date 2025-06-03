using System;
using System.Collections.Generic;
using System.Linq;
namespace ShopSystem
{

    public class Customer : User // Inherits from User

    {
        // UserId and Name are inherited from User
        public Dictionary<Product, int> Cart { get; private set; }

        public Customer(string customerId, string name) : base(customerId, name) // Call base constructor
        {
            // UserId and Name are set by the base constructor
            Cart = new Dictionary<Product, int>();
        }

        // ToString() is inherited, but if you want specific Customer info, you can override it
        public override string ToString()
        {
            return base.ToString() + " (Customer)";
        }

        public void AddToCart(Product product, int quantity)
        {
            if (quantity <= 0)
            {
                Console.WriteLine("Quantity must be positive.");
                return;
            }
            if (Cart.ContainsKey(product))
            {
                Cart[product] += quantity;
            }
            else
            {
                Cart.Add(product, quantity);
            }
            Console.WriteLine($"{quantity} x {product.Name} added to {this.Name}'s cart."); // Use this.Name from base
        }

        public void ViewCart()
        {
            if (!Cart.Any())
            {
                Console.WriteLine($"{this.Name}'s cart is empty."); // Use this.Name from base
                return;
            }
            Console.WriteLine($"\n--- {this.Name}'s Cart ---");
            decimal totalCost = 0;
            foreach (var item in Cart)
            {
                Product product = item.Key;
                int quantity = item.Value;
                decimal cost = product.Price * quantity;
                totalCost += cost;
                Console.WriteLine($"- {product.Name} (ID: {product.ProductId}): {quantity} x {product.Price:C} = {cost:C}");
            }
            Console.WriteLine($"Total potential cost: {totalCost:C}");
            Console.WriteLine("----------------------");
        }
    }
}