# Azure OpenAI Chat Completion in C# — Azure.AI.OpenAI SDK vs REST API (.NET 8)

A .NET 8 console application that demonstrates two ways to call an **Azure OpenAI chat completion** endpoint from C#: via the **`Azure.AI.OpenAI` NuGet package** and via a **raw HTTP REST call**. Both approaches use the same system prompt, which instructs the model to extract a product code and attributes from a natural-language query and return structured JSON.

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

> **Security note:** Do not commit a real API key. Keep `Key` empty or a placeholder in source control, and supply the key at run time — for example from an environment variable, .NET user secrets, or Azure Key Vault — or use Microsoft Entra ID authentication instead of a key. If a real key has ever been committed, rotate it in the Azure portal.

## Sample Data

`Data/6205.json` contains SKF bearing specifications for part number 6205. It is copied to the build output as sample reference data, but the current code does not read it or send it to the model — the model only extracts the product code and attributes from the question.

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

## Example Output

Model responses can vary between runs and deployments; a typical run looks like this:

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

## FAQ

**Should I use the SDK or the REST API?**
The `Azure.AI.OpenAI` SDK gives typed requests and responses and handles authentication options for you. The raw REST call shows exactly what goes over the wire and works from any HTTP client; it is useful for debugging or when you cannot take an SDK dependency.

**What is the `Model` setting?**
In Azure OpenAI, requests target a *deployment name* that you create in your resource. This demo expects a deployment named `gpt-4o-mini`.

## Related Examples

- [Local AI chat app with Ollama and Streamlit](https://github.com/gcfernando/document/blob/main/local_ai_learning_lab.md)
- [Back to all C# and .NET code examples](../README.md)

---

## Documentation links

[Read all C# and .NET Code Examples guides online](https://gcfernando.github.io/csharp_codes/documentation/) · [Project overview](https://gcfernando.github.io/csharp_codes/) · [All developer projects](https://gcfernando.github.io/gcfernando/projects/)
