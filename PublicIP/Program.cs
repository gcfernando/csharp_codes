// Developer ::=> Gehan Fernando

// Calls the FetchIpAddressAsync method to retrieve and display the public IP address.
await FetchIpAddressAsync();

// Waits for the user to press a key before closing the application.
Console.Read();

// Asynchronous method that fetches and displays the public IP address.
static async Task FetchIpAddressAsync()
{
    // Initializes a new instance of the HttpClient class to send HTTP requests.
    using var client = new HttpClient();
    try
    {
        // Sends an asynchronous GET request to the specified URL to fetch the public IP address.
        var ipAddress = await client.GetStringAsync("http://api.ipify.org");

        // Prints the fetched IP address to the console.
        Console.WriteLine($"Your public IP Address: {ipAddress}");
    }
    catch (Exception ex)
    {
        // Prints the error message to the console.
        Console.WriteLine($"Error fetching IP Address: {ex.Message}");
    }
}