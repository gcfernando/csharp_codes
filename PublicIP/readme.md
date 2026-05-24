# PublicIP

A minimal .NET 6 console application that fetches and displays your public IP address using the [ipify](https://www.ipify.org/) API.

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
