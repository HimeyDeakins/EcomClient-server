using System;
using System.Collections.Generic;
using System.Linq;
using SplashKitSDK; 
using System.Text.Json; // Required for JSON serialization/deserialization
// I have used the SplashKitSDK for networking and other functionalities.
// I confess that I have made use of Documentation and examples provided by SplashKit to understand how to implement the server functionality.
namespace ShopServer
{

    public class ServerProgram
    {
        private const string SERVER_NAME = "ECommerceShopServer";
        private const ushort SERVER_PORT = 3000;

        // The core shop logic and data reside here on the server
        private static ShopSystem.Manager _mainManager;
        private static ShopSystem.Shop _myShop;

        // Dictionary to map active client connections to logged-in Customers
        // This tracks WHICH customer is using WHICH connection.
        private static Dictionary<Connection, ShopSystem.Customer> _loggedInCustomers = new Dictionary<Connection, ShopSystem.Customer>();

        public static void Main(string[] args)
        {
            // Initialize Shop and Manager

            _mainManager = new ShopSystem.Manager(managerId: "MNG001", name: "Mr. Server");
            _myShop = new ShopSystem.Shop(_mainManager); // Manager is assigned during shop creation

            PrepopulateData(); // Add some initial products and customers

            ServerSocket serverSocket = null;

            try
            {
                Console.WriteLine($"[SERVER] Starting server '{SERVER_NAME}' on port {SERVER_PORT}...");

                // Create the server socket
                serverSocket = SplashKit.CreateServer(SERVER_NAME, SERVER_PORT);

                if (serverSocket == null)
                {
                    Console.WriteLine("[SERVER] Failed to create server socket! Port might be in use.");
                    Console.WriteLine("Press any key to exit...");
                    Console.ReadKey();
                    return;
                }

                Console.WriteLine("[SERVER] Server started successfully. Waiting for connections and messages...");
                Console.WriteLine("[SERVER] Press ESC to stop server.");


                // Main server loop - continuously check network activity
                // Run until Escape is pressed 
                // it runs untill the user presses the Escape key
                while (!Console.KeyAvailable || Console.ReadKey(true).Key != ConsoleKey.Escape)
                {
                    // --- Network Activity Check ---
                    // Check for network activity on the server socket which is very essential and get all teh messages
                    SplashKit.CheckNetworkActivity();

                    // --- Handle New Connections ---
                    // Process all pending new connections
                    while (SplashKit.ServerHasNewConnection(serverSocket))
                    {
                        // Fetch the oldest new connection
                        Connection newConnection = SplashKit.FetchNewConnection(serverSocket);
                        if (newConnection != null)
                        {
                            // Get connection details for logging
                            uint clientIP_uint = SplashKit.ConnectionIP(newConnection);
                            string clientAddress = SplashKit.DecToIpv4(clientIP_uint);
                            ushort clientPort = SplashKit.ConnectionPort(newConnection);
                            Console.WriteLine($"[SERVER] Accepted new connection from {clientAddress}:{clientPort}");

                            // New connections are automatically managed by the server socket.
                            // We don't need to store them explicitly here unless we need to iterate them later,
                            // but we do track logged-in customers using _loggedInCustomers.
                        }
                    }

                    // --- Handle Messages from Clients ---
                    // Process all pending messages for this server
                    while (SplashKit.HasMessages(serverSocket))
                    {
                        // Read the oldest message from the server's queue (includes messages from all clients)
                        Message incomingMessage = SplashKit.ReadMessage(serverSocket);
                        if (incomingMessage != null)
                        {
                            // Get the connection object that sent this specific message
                            Connection senderConnection = SplashKit.MessageConnection(incomingMessage);
                            // Get the message data as a string
                            string messageData = SplashKit.MessageData(incomingMessage);

                            // Get sender details for logging using the correct method chain
                            uint senderIP_uint = SplashKit.ConnectionIP(senderConnection);
                            string senderAddress = SplashKit.DecToIpv4(senderIP_uint);
                            ushort senderPort = SplashKit.ConnectionPort(senderConnection); // Or SplashKit.MessagePort(incomingMessage) - both should be the same for a TCP message received by the server

                            Console.WriteLine($"[SERVER] Received message from {senderAddress}:{senderPort}: '{messageData}'");

                            // Process the received message and send response back via senderConnection
                            ProcessClientMessage(senderConnection, messageData);

                            // IMPORTANT: Close/Free the message resource after you're done with it
                            SplashKit.CloseMessage(incomingMessage);
                        }
                    }

                    // Add a small delay to prevent high CPU usage when idle
                    SplashKit.Delay(10);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SERVER] An unexpected error occurred: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
            finally
            {
                // Clean up resources when the server stops
                Console.WriteLine("\n[SERVER] Shutting down server...");
                _loggedInCustomers.Clear(); // Clear tracked logged-in customers
                if (serverSocket != null && SplashKit.HasServer(SERVER_NAME))
                {
                    // Close the specific server socket. This typically also closes all its associated connections.
                    SplashKit.CloseServer(serverSocket);
                }
                // As a safeguard, close all servers and connections managed by SplashKit.
                SplashKit.CloseAllServers();
                SplashKit.CloseAllConnections();
                Console.WriteLine("[SERVER] Server stopped.");
            }
        }

        // Method to add some initial data to the shop
        static void PrepopulateData()
        {
            Console.WriteLine("[SERVER] Initializing shop with sample data...");
            // Using the manager object held by the shop to add products
            _myShop.AddProduct(_mainManager, "P001", "Laptop", 1200.00m, 10);
            _myShop.AddProduct(_mainManager, "P002", "Mouse", 25.00m, 50);
            _myShop.AddProduct(_mainManager, "P003", "Keyboard", 75.00m, 30);
            _myShop.AddProduct(_mainManager, "P004", "Webcam", 45.00m, 0); // Out of stock item

            // Add some initial customers directly to the shop's customer list
            // They will still need to "login" via the client to be associated with a connection
            _myShop.RegisterCustomer("C101", "Alice", silent: true);
            _myShop.RegisterCustomer("C102", "Bob", silent: true);

            Console.WriteLine("[SERVER] Sample data loaded.");
        }

        // Processes a message received from a client connection
        static void ProcessClientMessage(Connection clientConnection, string message)
        {
            string[] parts = message.Split(':');
            if (parts.Length == 0) return; // Ignore empty messages

            string command = parts[0].ToUpper(); // Get the command part (case-insensitive)

            // Try to get the logged-in customer associated with this specific connection (if any)
            _loggedInCustomers.TryGetValue(clientConnection, out ShopSystem.Customer currentCustomer);

            // --- Handle Commands ---
            switch (command)
            {
                case "REGISTER_CUSTOMER":
                    if (parts.Length >= 3)
                    {
                        string customerId = parts[1];
                        // Join remaining parts in case the name contained colons
                        string customerName = string.Join(":", parts.Skip(2));

                        // Use the Shop's registration logic
                        ShopSystem.Customer newCustomer = _myShop.RegisterCustomer(customerId, customerName, silent: true);

                        if (newCustomer != null)
                        {
                            SendMessage(clientConnection, "RESPONSE_SUCCESS:Registration successful. You can now log in with your ID.");
                        }
                        else
                        {
                            // RegisterCustomer already prints specific errors to the server console
                            SendMessage(clientConnection, "RESPONSE_ERROR:Registration failed. ID might be taken or input invalid.");
                        }
                    }
                    else
                    {
                        SendMessage(clientConnection, "RESPONSE_ERROR:Invalid REGISTER_CUSTOMER format. Use REGISTER_CUSTOMER:customerId:name");
                    }
                    break;

                case "LOGIN_CUSTOMER":
                    if (parts.Length >= 2)
                    {
                        string customerId = string.Join(":", parts.Skip(1)); // Join in case ID had colons (unlikely, but safe)

                        // Find the customer in the shop's known customers list
                        if (_myShop.Customers.TryGetValue(customerId, out ShopSystem.Customer customerToLogin))
                        {
                            // Check if this customer is already logged in on *any* active connection
                            if (_loggedInCustomers.ContainsValue(customerToLogin))
                            {
                                // Find the existing connection mapping
                                // var existingEntry = _loggedInCustomers.FirstOrDefault(pair => pair.Value == customerToLogin);
                                SendMessage(clientConnection, $"RESPONSE_ERROR:Customer '{customerToLogin.Name}' is already logged in elsewhere.");
                                Console.WriteLine($"[SERVER] Login attempt for {customerToLogin.UserId} failed: Already logged in.");
                            }
                            else
                            {
                                // Successfully logged in. Associate this connection with the customer object.
                                _loggedInCustomers[clientConnection] = customerToLogin;
                                // Send back success response including user ID and name for the client to display
                                SendMessage(clientConnection, $"RESPONSE_LOGIN_SUCCESS:{customerToLogin.UserId}:{customerToLogin.Name}");
                                Console.WriteLine($"[SERVER] Customer '{customerToLogin.Name}' (ID: {customerToLogin.UserId}) logged in on connection {clientConnection.IP}:{clientConnection.Port}.");
                            }
                        }
                        else
                        {
                            // Customer ID not found in the shop's customer list
                            SendMessage(clientConnection, "RESPONSE_ERROR:Invalid Customer ID or customer not found. Please register first.");
                        }
                    }
                    else
                    {
                        SendMessage(clientConnection, "RESPONSE_ERROR:Invalid LOGIN_CUSTOMER format. Use LOGIN_CUSTOMER:customerId");
                    }
                    break;

                case "LIST_PRODUCTS":
                    // This command does NOT require login
                    // We need to retrieve products and send the list as structured data (JSON)

                    // Create a list of simple objects suitable for JSON serialization
                    var productList = _myShop.Products.Values.ToList();
                    var simplifiedProducts = productList.Select(p => new
                    {
                        id = p.ProductId,
                        name = p.Name,
                        price = p.Price,
                        stock = p.QuantityAvailable
                    }).ToList();

                    // Serialize the list of objects to a JSON string
                    string jsonProducts = JsonSerializer.Serialize(simplifiedProducts);
                    // Send the response type identifier followed by the JSON data
                    SendMessage(clientConnection, $"RESPONSE_PRODUCTS:{jsonProducts}");
                    Console.WriteLine($"[SERVER] Sent product list to client {clientConnection.IP}:{clientConnection.Port}.");
                    break;

                // --- Commands requiring Customer Login ---
                case "ADD_TO_CART":
                    // Check if the connection is associated with a logged-in customer
                    if (currentCustomer == null)
                    {
                        SendMessage(clientConnection, "RESPONSE_ERROR:You must be logged in to add items to your cart.");
                        break;
                    }

                    if (parts.Length >= 3 && int.TryParse(parts[2], out int quantity) && quantity > 0)
                    {
                        // Join parts for productId in case it had colons
                        string productId = string.Join(":", parts.Skip(1).Take(parts.Length - 2));

                        // Find the product in the shop's main product list
                        if (_myShop.Products.TryGetValue(productId, out ShopSystem.Product productToAdd))
                        {
                            // Check available stock before allowing to add to cart
                            int currentCartQuantity = 0;
                            // Check if the product is already in the customer's cart
                            if (currentCustomer.Cart.TryGetValue(productToAdd, out int qtyInCart))
                            {
                                currentCartQuantity = qtyInCart;
                            }
                            int totalQuantityWanted = quantity + currentCartQuantity;

                            if (productToAdd.QuantityAvailable >= totalQuantityWanted)
                            {
                                // Add to the specific customer's cart instance on the server
                                currentCustomer.AddToCart(productToAdd, quantity);
                                SendMessage(clientConnection, $"RESPONSE_SUCCESS:{quantity} x {productToAdd.Name} added to your cart.");
                                Console.WriteLine($"[SERVER] Added {quantity} x {productToAdd.Name} to {currentCustomer.Name}'s cart.");
                            }
                            else if (productToAdd.QuantityAvailable > currentCartQuantity)
                            {
                                // Can add some, but not the full requested quantity
                                int canAdd = productToAdd.QuantityAvailable - currentCartQuantity;
                                currentCustomer.AddToCart(productToAdd, canAdd);
                                SendMessage(clientConnection, $"RESPONSE_ERROR:Only {productToAdd.QuantityAvailable} available in total (including what's in your cart). Added {canAdd} x {productToAdd.Name} to cart.");
                                Console.WriteLine($"[SERVER] Added {canAdd} x {productToAdd.Name} to {currentCustomer.Name}'s cart (limited by stock).");
                            }
                            else
                            {
                                // Not enough stock even for the current quantity requested
                                SendMessage(clientConnection, $"RESPONSE_ERROR:Sorry, not enough stock for {productToAdd.Name}. Available: {productToAdd.QuantityAvailable}");
                                Console.WriteLine($"[SERVER] Add to cart failed for {currentCustomer.Name} ({productToAdd.Name}): Not enough stock.");
                            }
                        }
                        else
                        {
                            SendMessage(clientConnection, "RESPONSE_ERROR:Product ID not found in shop.");
                        }
                    }
                    else
                    {
                        SendMessage(clientConnection, "RESPONSE_ERROR:Invalid ADD_TO_CART format. Use ADD_TO_CART:productId:quantity (quantity must be positive integer)");
                    }
                    break;

                case "VIEW_CART":
                    // Check if the connection is associated with a logged-in customer
                    if (currentCustomer == null)
                    {
                        SendMessage(clientConnection, "RESPONSE_ERROR:You must be logged in to view your cart.");
                        break;
                    }
                    // Send the customer's cart content as structured data (JSON)
                    var cartItems = currentCustomer.Cart.Select(item => new
                    {
                        id = item.Key.ProductId,
                        name = item.Key.Name,
                        price = item.Key.Price,
                        quantityInCart = item.Value // Use a distinct name from 'stock'
                    }).ToList();

                    string jsonCart = JsonSerializer.Serialize(cartItems);
                    SendMessage(clientConnection, $"RESPONSE_CART:{jsonCart}");
                    Console.WriteLine($"[SERVER] Sent cart content to {currentCustomer.Name}.");
                    break;

                case "CHECKOUT":
                    // Check if the connection is associated with a logged-in customer
                    if (currentCustomer == null)
                    {
                        SendMessage(clientConnection, "RESPONSE_ERROR:You must be logged in to checkout.");
                        break;
                    }

                    // The BuyCart logic needs to run on the server against the server's shop instance
                    // and the server's instance of the customer's cart.
                    // We need to adapt the BuyCart logic slightly or call it and process its outcome.

                    if (!currentCustomer.Cart.Any())
                    {
                        SendMessage(clientConnection, "RESPONSE_ERROR:Your cart is empty. Nothing to buy.");
                        Console.WriteLine($"[SERVER] Checkout failed for {currentCustomer.Name}: Cart empty.");
                        break;
                    }

                    Console.WriteLine($"[SERVER] Processing checkout for {currentCustomer.Name}...");
                    List<string> purchasedItemSummaries = new List<string>();
                    List<string> failedItemSummaries = new List<string>();
                    decimal totalPurchaseCost = 0;

                    // Create a temporary list of products that were successfully processed
                    List<ShopSystem.Product> successfullyPurchasedProducts = new List<ShopSystem.Product>();


                    // Iterate through a copy of the cart items to avoid issues while modifying the cart
                    var cartItemsToProcess = new Dictionary<ShopSystem.Product, int>(currentCustomer.Cart);

                    foreach (var cartEntry in cartItemsToProcess)
                    {
                        ShopSystem.Product productInCart = cartEntry.Key;
                        int quantityWanted = cartEntry.Value;

                        // Find the product in the shop's main product list (to check current stock)
                        // Use TryGetValue to be safe if a product was removed from the shop AFTER being added to cart
                        if (_myShop.Products.TryGetValue(productInCart.ProductId, out ShopSystem.Product shopProduct))
                        {
                            if (shopProduct.QuantityAvailable >= quantityWanted)
                            {
                                // Sufficient stock - update shop stock
                                shopProduct.UpdateStock(-quantityWanted); // UpdateStock handles the quantity change
                                totalPurchaseCost += shopProduct.Price * quantityWanted;
                                purchasedItemSummaries.Add($"{quantityWanted} x {shopProduct.Name}");
                                Console.WriteLine($"[SERVER] Processed: {quantityWanted} x {shopProduct.Name} for {currentCustomer.Name}");
                                successfullyPurchasedProducts.Add(productInCart); // Add the original cart product object to the list
                            }
                            else
                            {
                                failedItemSummaries.Add($"{quantityWanted} x {shopProduct.Name} (Not enough stock. Available: {shopProduct.QuantityAvailable})");
                                Console.WriteLine($"[SERVER] Checkout failed for {currentCustomer.Name} ({shopProduct.Name}): Not enough stock.");
                            }
                        }
                        else
                        {
                            failedItemSummaries.Add($"{quantityWanted} x {productInCart.Name} (No longer sold)");
                            Console.WriteLine($"[SERVER] Checkout failed for {currentCustomer.Name} ({productInCart.Name}): Product no longer exists in shop.");
                        }
                    }

                    // Now, remove successfully purchased items from the customer's actual cart based on the temporary list
                    foreach (ShopSystem.Product purchasedProductKey in successfullyPurchasedProducts)
                    {
                        currentCustomer.Cart.Remove(purchasedProductKey);
                    }

                    // Construct the response message based on outcomes
                    if (purchasedItemSummaries.Any())
                    {
                        string purchasedSummary = string.Join(", ", purchasedItemSummaries);
                        string successMessage = $"Checkout successful! Purchased: {purchasedSummary}. Total cost: {totalPurchaseCost:C}.";
                        if (failedItemSummaries.Any())
                        {
                            successMessage += $" Some items could not be purchased: {string.Join(", ", failedItemSummaries)}.";
                        }
                        SendMessage(clientConnection, $"RESPONSE_SUCCESS:{successMessage}");
                        Console.WriteLine($"[SERVER] Checkout complete for {currentCustomer.Name}. Total charged: {totalPurchaseCost:C}.");
                    }
                    else if (failedItemSummaries.Any())
                    {
                        string failedSummary = string.Join(", ", failedItemSummaries);
                        SendMessage(clientConnection, $"RESPONSE_ERROR:Checkout failed for all items in your cart. Issues: {failedSummary}");
                        Console.WriteLine($"[SERVER] Checkout failed for {currentCustomer.Name}: All items failed.");
                    }
                    else
                    {
                        // This case should ideally not happen if cart was checked as non-empty, but good for robustness
                        SendMessage(clientConnection, $"RESPONSE_ERROR:An unexpected error occurred during checkout processing.");
                        Console.WriteLine($"[SERVER] Checkout failed for {currentCustomer.Name}: Unexpected processing error.");
                    }

                    // Optionally send updated cart if there are remaining items that weren't purchased
                    if (currentCustomer.Cart.Any())
                    {
                        // Prepare and send the updated cart content
                        var remainingCartItems = currentCustomer.Cart.Select(item => new
                        {
                            id = item.Key.ProductId,
                            name = item.Key.Name,
                            price = item.Key.Price,
                            quantityInCart = item.Value
                        }).ToList();

                        string jsonRemainingCart = JsonSerializer.Serialize(remainingCartItems);
                        // Send this as a separate cart update response
                        SendMessage(clientConnection, $"RESPONSE_CART:{jsonRemainingCart}");
                        Console.WriteLine($"[SERVER] Sent remaining cart content to {currentCustomer.Name}.");
                    }
                    break;


                case "LOGOUT":
                    // Check if the connection is associated with a logged-in customer
                    if (currentCustomer == null)
                    {
                        // Client sent logout command but wasn't logged in on this connection
                        SendMessage(clientConnection, "RESPONSE_ERROR:You are not currently logged in on this connection.");
                        Console.WriteLine($"[SERVER] Logout attempt from not-logged-in connection {clientConnection.IP}:{clientConnection.Port}.");
                    }
                    else
                    {
                        // Remove the association between this connection and the customer
                        _loggedInCustomers.Remove(clientConnection);
                        SendMessage(clientConnection, "RESPONSE_SUCCESS:Logged out successfully. Goodbye!");
                        Console.WriteLine($"[SERVER] Customer '{currentCustomer.Name}' (ID: {currentCustomer.UserId}) logged out from connection {clientConnection.IP}:{clientConnection.Port}.");
                        // Note: The network Connection object itself is NOT closed here. The client can log in again or as a different user.
                    }
                    break;

                default:
                    // Handle unknown commands or commands sent without login that require it
                    if (currentCustomer == null)
                    {
                        SendMessage(clientConnection, "RESPONSE_ERROR:Unknown command or you must login first.");
                    }
                    else
                    {
                        SendMessage(clientConnection, "RESPONSE_ERROR:Unknown command.");
                    }
                    Console.WriteLine($"[SERVER] Received unknown command: '{command}' from client {clientConnection.IP}:{clientConnection.Port}.");
                    break;
            }
        }

        // Helper to send a message to a specific connection
        static void SendMessage(Connection connection, string message)
        {
            // Ensure the connection is valid and still open before sending
            if (connection != null && SplashKit.IsConnectionOpen(connection))
            {
                bool sent = SplashKit.SendMessageTo(message, connection);
                if (!sent)
                {
                    // Log failure, potentially indicating a broken connection
                    uint clientIP_uint = SplashKit.ConnectionIP(connection); // Use the correct method chain
                    string clientAddress = SplashKit.DecToIpv4(clientIP_uint);
                    ushort clientPort = SplashKit.ConnectionPort(connection);
                    Console.WriteLine($"[SERVER] Failed to send message to client {clientAddress}:{clientPort}.");
                    // In a real application, repeated failures might lead to closing the connection.
                }
            }
            else if (connection != null)
            {
                // Log if attempting to send to a connection that is no longer open
                uint clientIP_uint = SplashKit.ConnectionIP(connection); // Use the correct method chain
                string clientAddress = SplashKit.DecToIpv4(clientIP_uint);
                ushort clientPort = SplashKit.ConnectionPort(connection);
                Console.WriteLine($"[SERVER] Attempted to send message to closed connection {clientAddress}:{clientPort}.");
            }
            // If connection is null, do nothing.
        }
    }
}