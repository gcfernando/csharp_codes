# Simple Chat Completion — Azure OpenAI Demo

A .NET 8 console application that demonstrates two ways to call an Azure OpenAI chat completion endpoint: via the **`Azure.AI.OpenAI` NuGet package** and via a **raw HTTP REST call**. Both approaches use the same system prompt, which instructs the model to extract a product code and attributes from a natural-language query and return structured JSON.

## Overview

The application runs two test queries against each connection method:

1. A product-related query: `"Can you help me to find the values of Sealing, Matched arrangement in 6205?"`
2. An unrelated query: `"What is the capital of Sri Lanka?"`

The system message instructs the model to extract product codes (e.g. `6205`) and attributes from the user query and return:

```json
{ "product": "<product_code>", "attributes": ["<attr1>", "<attr2>"] }
```

For queries unrelated to products, the model returns a polite refusal.

## Connection Methods

### PackageDemo — `Azure.AI.OpenAI` SDK

Uses `AzureOpenAIClient` and `GetChatClient` to send a `ChatCompletionOptions` request with `Temperature = 0.7f` and `MaxOutputTokenCount = 100`.

### RestDemo — Raw HTTP

Uses `HttpClient` to POST directly to the Azure OpenAI REST endpoint, with a `Newtonsoft.Json`-serialised request body, extracting the response text from `choices[0].message.content`.

## Configuration

All connection settings are defined in `Configuration.cs`:

| Setting | Description |
|---|---|
| `EndPoint` | Azure OpenAI resource URL |
| `Key` | API key |
| `Model` | Deployment name (`gpt-4o-mini`) |
| `Version` | API version (`2024-08-01-preview`) |
| `SystemMessage` | System prompt for product code extraction |

> **Note:** The API key is stored in source code for this demo. In production, use environment variables or Azure Key Vault.

## Sample Data

`Data/6205.json` contains SKF bearing specifications for part number 6205, used as reference context for the product query demo.

## Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- An Azure OpenAI resource with a `gpt-4o-mini` deployment

## NuGet Packages

| Package | Version |
|---|---|
| `Azure.AI.OpenAI` | 2.1.0 |
| `Newtonsoft.Json` | 13.0.3 |

## Build and Run

```bash
cd Simple_ChatCompletion
dotnet run
```

## Expected Output

```
** Package Demo **
{"product": "6205", "attributes": ["Sealing", "Matched arrangement"]}
I am sorry, I cannot help with your request. Your question is not related to products.

** Rest Demo **
{"product": "6205", "attributes": ["Sealing", "Matched arrangement"]}
I am sorry, I cannot help with your request. Your question is not related to products.
```

## Project Structure

```
Simple_ChatCompletion/
├── ConnectByPackage/
│   └── PackageDemo.cs       # SDK-based chat completion
├── ConnectByRest/
│   └── RestDemo.cs          # Raw HTTP REST chat completion
├── Data/
│   └── 6205.json            # SKF bearing 6205 specification data
├── Configuration.cs         # Endpoint, key, model, and system message constants
├── Program.cs               # Runs both demos with two test queries
├── Simple_ChatCompletion.csproj
└── Simple_ChatCompletion.sln
```
