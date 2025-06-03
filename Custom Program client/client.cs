using System;
using System.Collections.Generic;
using SplashKitSDK; // Assuming your SplashKit C# bindings are here
using System.Text.Json; // Required for JSON deserialization
using System.Linq; // For LINQ extensions like .Any()

namespace ShopClient
{
    public class ClientProgram
    {
        private const string CLIENT_NAME = "ECommerceShopClient";
        private const string SERVER_HOST = "localhost"; // Or the server's IP address if on another machine
        private const ushort SERVER_PORT = 3000; // Must match the server's port

        private static Connection _serverConnection = null;
        private static string _loggedInUserId = null; // Track current user ID
        private static string _loggedInUserName = null; // Track current user name

        // Helper struct/class to match server's simplified product/cart data structure for JSON deserialization
        private class ProductData
        {
            public string id { get; set; }
            public string name { get; set; }
            public decimal price { get; set; }
            public int stock { get; set; } // Used for product list
            public int quantityInCart { get; set; } // Used for cart list
        }

        public static void Main(string[] args)
        {
            Console.WriteLine($"[CLIENT] Attempting to connect to server at {SERVER_HOST}:{SERVER_PORT}...");

            // Open a connection to the server
            _serverConnection = SplashKit.OpenConnection(CLIENT_NAME, SERVER_HOST, SERVER_PORT);

            // Check if the connection was successful and is open
            if (_serverConnection == null || !SplashKit.IsConnectionOpen(_serverConnection))
            {
                Console.WriteLine("[CLIENT] Failed to connect to server. Please ensure the server is running.");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return;
            }

            Console.WriteLine("[CLIENT] Successfully connected to server.");

            // Main client loop - simultaneously handle network messages and user input
            string userInput = "";
            do
            {
                // --- Network Activity Check ---
                SplashKit.CheckNetworkActivity();

                // --- Handle Messages from Server ---
                while (SplashKit.HasMessages(_serverConnection))
                {
                    Message incomingMessage = SplashKit.ReadMessage(_serverConnection);
                    if (incomingMessage != null)
                    {
                        string messageData = SplashKit.MessageData(incomingMessage);
                        ProcessServerResponse(messageData); // Process the server's response
                        SplashKit.CloseMessage(incomingMessage); // Close/Free the message resource
                    }
                }

                // --- Handle User Input ---
                // Only prompt for input if no key is pending and connection is open
                if (!Console.KeyAvailable && SplashKit.IsConnectionOpen(_serverConnection))
                {
                     ShowMenu(); // Display appropriate menu based on login status
                     Console.Write("Enter command: ");
                     userInput = Console.ReadLine()?.Trim();

                    if (!string.IsNullOrWhiteSpace(userInput))
                    {
                        // Process user input and send command to server
                        if (userInput.ToLower() == "quit")
                        {
                            // Send logout command before quitting if logged in
                            if (_loggedInUserId != null)
                            {
                                SendMessage("LOGOUT");
                            }
                            Console.WriteLine("[CLIENT] Disconnecting from server...");
                            break; // Exit loop
                        }
                        SendCommand(userInput); // Send the command based on user input
                    }
                }
                 else if (Console.KeyAvailable) // Consume any lingering key presses from ReadLine()
                 {
                    // This might not be strictly needed with ReadLine after KeyAvailable check,
                    // but can help if input handling logic is more complex.
                 }


                // Add a small delay to prevent high CPU usage, especially when idle
                SplashKit.Delay(10);

                // Continue loop if connection is open and user hasn't typed 'quit'
            } while (SplashKit.IsConnectionOpen(_serverConnection));

            // Loop finished (either disconnected or user typed 'quit')
            if (!SplashKit.IsConnectionOpen(_serverConnection))
            {
                Console.WriteLine("[CLIENT] Connection to server lost.");
            }

            // Final cleanup
            Console.WriteLine("[CLIENT] Closing connection and exiting.");
            if (_serverConnection != null)
            {
                 SplashKit.CloseConnection(_serverConnection); // Close the specific connection object
            }
            SplashKit.CloseAllConnections(); // Ensure all client connections are closed

            Console.WriteLine("[CLIENT] Client stopped.");
            Console.WriteLine("Press any key to close window...");
            Console.ReadKey();
        }

        // Displays the menu based on login state
        static void ShowMenu()
        {
             Console.WriteLine("\n--- Shop Menu ---");
            if (_loggedInUserId == null)
            {
                // Not logged in
                Console.WriteLine("REGISTER <id> <name> - Register as a new customer");
                Console.WriteLine("LOGIN <id>           - Login as a customer");
                Console.WriteLine("LIST_PRODUCTS        - View available products"); // Allow browsing before login
                Console.WriteLine("QUIT                 - Exit");
            }
            else
            {
                // Logged in as customer
                Console.WriteLine($"Logged in as: {_loggedInUserName} (ID: {_loggedInUserId})");
                Console.WriteLine("LIST_PRODUCTS      - View available products");
                Console.WriteLine("ADD_TO_CART <id> <qty> - Add product to your cart");
                Console.WriteLine("VIEW_CART          - View items in your cart");
                Console.WriteLine("CHECKOUT           - Purchase items in your cart");
                Console.WriteLine("LOGOUT             - Logout");
                Console.WriteLine("QUIT               - Exit");
            }
             Console.WriteLine("-----------------");
        }

        // Parses user input and sends the corresponding command to the server
        static void SendCommand(string input)
        {
            string[] parts = input.Trim().Split(' ');
            if (parts.Length == 0) return;

            string command = parts[0].ToUpper();
            string messageToSend = "";

            // Handle commands that don't require login first
            switch (command)
            {
                case "REGISTER":
                    if (_loggedInUserId != null) { Console.WriteLine("You are already logged in."); return; }
                    if (parts.Length >= 3)
                    {
                         // Reconstruct name in case it had spaces
                         string customerId = parts[1];
                         string customerName = string.Join(" ", parts.Skip(2));
                        messageToSend = $"REGISTER_CUSTOMER:{customerId}:{customerName}";
                    }
                    else
                    {
                        Console.WriteLine("Invalid usage. Use: REGISTER <id> <name>");
                        return;
                    }
                    break;
                case "LOGIN":
                     if (_loggedInUserId != null) { Console.WriteLine("You are already logged in."); return; }
                    if (parts.Length >= 2)
                    {
                         // Reconstruct id in case it had spaces (unlikely, but safe)
                         string customerId = string.Join(" ", parts.Skip(1));
                        messageToSend = $"LOGIN_CUSTOMER:{customerId}";
                    }
                    else
                    {
                        Console.WriteLine("Invalid usage. Use: LOGIN <id>");
                        return;
                    }
                    break;
                 case "LIST_PRODUCTS":
                    // Allow this command without login
                     messageToSend = "LIST_PRODUCTS";
                     break;

                // Handle commands that require login
                case "ADD_TO_CART":
                    if (_loggedInUserId == null) { Console.WriteLine("You must be logged in to use this command."); return; }
                    if (parts.Length >= 3 && int.TryParse(parts[2], out int quantity) && quantity > 0)
                    {
                         // Reconstruct id in case it had spaces
                         string productId = string.Join(" ", parts.Skip(1).Take(parts.Length - 2));
                        messageToSend = $"ADD_TO_CART:{productId}:{quantity}";
                    }
                    else
                    {
                        Console.WriteLine("Invalid usage. Use: ADD_TO_CART <productId> <quantity>");
                        return;
                    }
                    break;
                case "VIEW_CART":
                    if (_loggedInUserId == null) { Console.WriteLine("You must be logged in to use this command."); return; }
                    messageToSend = "VIEW_CART";
                    break;
                case "CHECKOUT":
                     if (_loggedInUserId == null) { Console.WriteLine("You must be logged in to use this command."); return; }
                    messageToSend = "CHECKOUT";
                    break;
                case "LOGOUT":
                     if (_loggedInUserId == null) { Console.WriteLine("You are not logged in."); return; }
                    messageToSend = "LOGOUT";
                    break;

                default:
                    Console.WriteLine($"Unknown command: {command}. Type 'QUIT' or see menu options.");
                    return; // Don't send anything
            }

            // Send the constructed message to the server
            if (!string.IsNullOrWhiteSpace(messageToSend))
            {
                bool sent = SplashKit.SendMessageTo(messageToSend, _serverConnection);
                if (sent)
                {
                    Console.WriteLine($"[CLIENT] Sent: '{messageToSend}'");
                }
                else
                {
                    Console.WriteLine("[CLIENT] Failed to send message. Server connection may be lost.");
                }
            }
        }

        // Processes messages received from the server
        static void ProcessServerResponse(string response)
        {
            string[] parts = response.Split(':');
            if (parts.Length == 0) return;

            string responseType = parts[0].ToUpper();
            string responseData = parts.Length > 1 ? string.Join(":", parts.Skip(1)) : ""; // Get everything after the type

            switch (responseType)
            {
                case "RESPONSE_SUCCESS":
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"[SERVER] SUCCESS: {responseData}");
                    Console.ResetColor();
                    break;

                case "RESPONSE_ERROR":
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[SERVER] ERROR: {responseData}");
                    Console.ResetColor();
                    break;

                case "RESPONSE_LOGIN_SUCCESS":
                    if (parts.Length >= 3)
                    {
                        _loggedInUserId = parts[1];
                        _loggedInUserName = parts[2];
                         // Reconstruct name in case it had colons (though unlikely with simple names)
                         if (parts.Length > 3)
                         {
                             _loggedInUserName = string.Join(":", parts.Skip(2));
                         }
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"[SERVER] Login successful. Welcome, {_loggedInUserName}!");
                        Console.ResetColor();
                    } else {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"[SERVER] Login success response format error: {response}");
                        Console.ResetColor();
                    }
                    break;

                case "RESPONSE_PRODUCTS":
                    // Deserialize the JSON data and display products
                    try
                    {
                        var products = JsonSerializer.Deserialize<List<ProductData>>(responseData);
                        Console.WriteLine("\n--- Available Products ---");
                        if (products == null || !products.Any())
                        {
                            Console.WriteLine("No products available.");
                        }
                        else
                        {
                            // Sort for consistent display
                             foreach (var p in products.OrderBy(p => p.id))
                            {
                                Console.WriteLine($"ID: {p.id}, Name: {p.name}, Price: {p.price:C}, Stock: {p.stock}");
                            }
                        }
                        Console.WriteLine("--------------------------");
                    }
                    catch (JsonException ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"[CLIENT] Error parsing product data: {ex.Message}");
                        Console.ResetColor();
                        // Console.WriteLine(responseData); // Uncomment to see raw JSON
                    }
                    break;

                case "RESPONSE_CART":
                    // Deserialize the JSON data and display cart items
                    try
                    {
                        var cartItems = JsonSerializer.Deserialize<List<ProductData>>(responseData);
                        Console.WriteLine($"\n--- Your Cart ({_loggedInUserName}) ---");
                        if (cartItems == null || !cartItems.Any())
                        {
                            Console.WriteLine("Your cart is empty.");
                        }
                        else
                        {
                            decimal totalCost = 0;
                            // Sort for consistent display
                            foreach (var item in cartItems.OrderBy(item => item.id))
                            {
                                decimal itemCost = item.price * item.quantityInCart;
                                totalCost += itemCost;
                                Console.WriteLine($"- {item.name} (ID: {item.id}): {item.quantityInCart} x {item.price:C} = {itemCost:C}");
                            }
                             Console.WriteLine($"Total potential cost: {totalCost:C}");
                        }
                        Console.WriteLine("----------------------");
                    }
                    catch (JsonException ex)
                    {
                         Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"[CLIENT] Error parsing cart data: {ex.Message}");
                         Console.ResetColor();
                        // Console.WriteLine(responseData); // Uncomment to see raw JSON
                    }
                    break;

                default:
                    // Received an unexpected message type
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"[SERVER] Received unexpected response type: {responseType}");
                     Console.WriteLine($"Raw: {response}");
                    Console.ResetColor();
                    break;
            }
        }

         // Helper to send a command message to the server
        static void SendMessage(string message)
        {
             if (_serverConnection != null && SplashKit.IsConnectionOpen(_serverConnection))
            {
                SplashKit.SendMessageTo(message, _serverConnection);
            }
        }
    }
}