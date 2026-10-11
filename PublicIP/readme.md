# Get Your Public IP Address in C# — HttpClient and the ipify API (.NET 6)

A minimal .NET 6 console application that shows **how to get your public IP address in C#**: it calls the [ipify](https://www.ipify.org/) API with `HttpClient` and prints the result.

## How It Works

The application sends an HTTP GET request to `http://api.ipify.org`, which returns your public IPv4 address as plain text. The result is printed to the console. If the request fails, the error message is printed instead.

## Requirements

- [.NET 6 SDK](https://dotnet.microsoft.com/download) (or later)
- An active internet connection

## Build and Run

```bash
cd PublicIP
dotnet run
```

## Example Output

```
Your public IP Address: 203.0.113.42
```

## Project Structure

```
PublicIP/
├── Program.cs          # Fetches and prints the public IP address
├── PublicIP.csproj
└── PublicIP.sln
```

## Notes

- The request uses plain `http://api.ipify.org`. ipify also offers `https://api.ipify.org`, and `https://api64.ipify.org` for IPv6 where available.
- The code creates one `HttpClient` for a single request, which is fine for a console demo. In long-running applications, reuse `HttpClient` or use `IHttpClientFactory`.

## Related Examples

- [Python internet speed test](https://github.com/gcfernando/python_codes/blob/main/Internet%20Speed%20Test/readme.md)
- [Back to all C# and .NET code examples](../README.md)
