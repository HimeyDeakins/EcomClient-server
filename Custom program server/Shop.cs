using System;
using System.Collections.Generic;
using System.Linq;
namespace ShopSystem
{
    public class Shop
    {
        // Using ProductId (string) as key for easy lookup
        public Dictionary<string, Product> Products { get; private set; }
        // Using CustomerId (string) as key, and Customer object as value
        public Dictionary<string, Customer> Customers { get; private set; }
        public Manager ShopManager { get; private set; } // The shop has one designated Manager

        public Shop(Manager manager)
        {
            Products = new Dictionary<string, Product>();
            Customers = new Dictionary<string, Customer>();
            ShopManager = manager; // Assign the manager passed during shop creation
                                   // Console.WriteLine($"Shop initialized with Manager: {ShopManager.Name}"); // Optional: for debugging
        }


        /// Checks if the acting manager is the authorized manager for this shop.

        private bool IsAuthorized(Manager actingManager)
        {
            // For simplicity, we check if it's the same Manager instance.
            // In a real system, you might check IDs or roles.
            if (actingManager == ShopManager)
            {
                return true;
            }
            else
            {
                Console.WriteLine("Unauthorized action: This action can only be performed by the shop manager.");
                return false;
            }
        }

        /// <summary>
        /// Lists all products currently available in the shop.
        /// </summary>
        public void ListProducts()
        {
            Console.WriteLine("\n--- Available Products ---");
            if (!Products.Any()) // Check if the Products dictionary is empty
            {
                Console.WriteLine("No products available.");
                return;
            }
            // <summary>
            // Order by ProductId for consistent display
            //</summary>
            // Prouducts.OrderBy(p => p.Key) returns an ordered IEnumerable<KeyValuePair<string, Product>>  have searched on stack 
            // to get the infromataion
            foreach (var productEntry in Products.OrderBy(p => p.Key))
            {
                Console.WriteLine(productEntry.Value); // Calls Product.ToString()
            }
            Console.WriteLine("--------------------------");
        }

        /// <summary>
        /// Lists all registered customers. Requires manager authorization.
        /// </summary>
        public void ListCustomers(Manager actingManager)
        {
            if (!IsAuthorized(actingManager)) return; // Check authorization first

            Console.WriteLine("\n--- Registered Customers ---");
            if (!Customers.Any()) // Check if the Customers dictionary is empty
            {
                Console.WriteLine("No customers registered.");
                return;
            }
            // Order by CustomerId for consistent display
            foreach (var customerEntry in Customers.OrderBy(c => c.Key))
            {
                Console.WriteLine(customerEntry.Value); // Calls Customer.ToString() (or User.ToString() if not overridden)
            }
            Console.WriteLine("----------------------------");
        }

        /// <summary>
        /// Adds a new product to the shop. Requires manager authorization.
        /// </summary>
        public void AddProduct(Manager actingManager, string productId, string name, decimal price, int quantity)
        {
            if (!IsAuthorized(actingManager)) return;

            if (Products.ContainsKey(productId))
            {
                Console.WriteLine($"Error: Product with ID '{productId}' already exists.");
                return;
            }
            if (price <= 0)
            {
                Console.WriteLine("Error: Product price must be positive.");
                return;
            }
            if (quantity < 0)
            {
                Console.WriteLine("Error: Product quantity cannot be negative.");
                return;
            }

            Product newProduct = new Product(productId, name, price, quantity);
            Products.Add(productId, newProduct);
            Console.WriteLine($"Product '{name}' (ID: {productId}) added to shop by {actingManager.Name}.");
        }

        /// <summary>
        /// Adds (or removes, if negative) quantity to an existing product's stock. Requires manager authorization.
        /// </summary>
        public void AddQuantityToProduct(Manager actingManager, string productId, int quantityChange)
        {
            if (!IsAuthorized(actingManager)) return;

            if (!Products.TryGetValue(productId, out Product product))
            {
                Console.WriteLine($"Error: Product with ID '{productId}' not found.");
                return;
            }

            // The Product.UpdateStock method handles the logic of not going below zero.
            if (product.UpdateStock(quantityChange))
            {
                string action = quantityChange >= 0 ? "added to" : "removed from";
                Console.WriteLine($"{Math.Abs(quantityChange)} units {action} '{product.Name}' stock by {actingManager.Name}. New stock: {product.QuantityAvailable}");
            }
            // If UpdateStock returns false, it already printed an error message.
        }

        /// <summary>
        /// Removes a product from the shop. Requires manager authorization.
        /// </summary>
        public void RemoveProduct(Manager actingManager, string productId)
        {
            if (!IsAuthorized(actingManager)) return;

            if (!Products.ContainsKey(productId))
            {
                Console.WriteLine($"Error: Product with ID '{productId}' not found.");
                return;
            }
            string removedProductName = Products[productId].Name;
            Products.Remove(productId);
            Console.WriteLine($"Product '{removedProductName}' (ID: {productId}) removed from shop by {actingManager.Name}.");
        }

        /// <summary>
        /// Registers a new customer in the shop system.
        /// </summary>
        /// <returns>The new Customer object if successful, otherwise null.</returns>
        public Customer RegisterCustomer(string customerId, string name, bool silent = false)
        {
            if (string.IsNullOrWhiteSpace(customerId))
            {
                if (!silent) Console.WriteLine("Error: Customer ID cannot be empty.");
                return null;
            }
            if (string.IsNullOrWhiteSpace(name))
            {
                if (!silent) Console.WriteLine("Error: Customer name cannot be empty.");
                return null;
            }

            if (Customers.ContainsKey(customerId))
            {
                if (!silent) Console.WriteLine($"Error: Customer ID '{customerId}' is already taken. Please choose a different ID.");
                return null; // Indicate failure
            }
            // Potentially check if customerId conflicts with managerId, though less critical here
            if (ShopManager != null && customerId == ShopManager.UserId)
            {
                if (!silent) Console.WriteLine($"Error: Customer ID '{customerId}' is reserved. Please choose a different ID.");
                return null;
            }

            Customer newCustomer = new Customer(customerId, name); // Creates a Customer instance
            Customers.Add(customerId, newCustomer);
            if (!silent) Console.WriteLine($"Customer '{name}' (ID: {newCustomer.UserId}) registered successfully!");
            return newCustomer;
        }

        /// <summary>
        /// Allows a customer to buy a specific quantity of a product directly.
        /// Note: The primary purchasing mechanism is now through BuyCart in Program.cs.
        /// This method is kept for potential direct purchase scenarios.
        /// </summary>
        public void BuyProduct(string customerId, string productId, int quantityToBuy)
        {
            if (!Customers.TryGetValue(customerId, out Customer customer))
            {
                Console.WriteLine($"Error: Customer with ID '{customerId}' not found. Please register first.");
                return;
            }
            if (!Products.TryGetValue(productId, out Product product))
            {
                Console.WriteLine($"Error: Product with ID '{productId}' not found in shop.");
                return;
            }

            if (quantityToBuy <= 0)
            {
                Console.WriteLine("Quantity to buy must be positive.");
                return;
            }

            if (product.QuantityAvailable < quantityToBuy)
            {
                Console.WriteLine($"Sorry, not enough stock for '{product.Name}'. Available: {product.QuantityAvailable}, Requested: {quantityToBuy}");
                return;
            }

            // Reduce shop stock
            if (product.UpdateStock(-quantityToBuy))
            {
                // Add to customer's cart (or could be a direct purchase record)
                customer.AddToCart(product, quantityToBuy); // Using AddToCart for consistency
                Console.WriteLine($"Purchase successful for {customer.Name}: {quantityToBuy} x {product.Name}.");
                Console.WriteLine($"Remaining stock for {product.Name}: {product.QuantityAvailable}");
            }
            // If UpdateStock fails, it already printed an error.
        }
    }
}

    