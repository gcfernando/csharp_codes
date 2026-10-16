# JSONPath Query Examples in C# — Newtonsoft.Json SelectTokens Patterns (.NET 6)

A .NET 6 console application demonstrating 14 **JSONPath query patterns in C#** using **Newtonsoft.Json**'s `JObject.SelectTokens` method — wildcards, array indexes, recursive descent, filter expressions with `&&` and `||`, and regular-expression matches. The project serves as a practical reference for navigating complex, nested JSON structures.

## Overview

JSONPath (via `JObject.SelectTokens`) is more expressive than XPath for JSON because it natively understands JSON arrays, nested objects, and filter expressions. This project runs several distinct queries against a sample person record (`Data.json`) and prints the results to the console.

## Query Patterns Demonstrated

| Pattern | Description |
|---|---|
| `$.*` | All root-level elements |
| `$.education` | Specific property at the root level |
| `$.parents[*]` | All elements of the `parents` array |
| `$.parents[?(@.relationship == 'father')]` | Array filter by property equality |
| `$.parents[?(@.relationship == 'father' && @.age == 40)]` | Array filter with AND condition |
| `$..*` | All elements at any depth (recursive descent) |
| `$..location` | All `location` properties at any depth |
| `$.communication.addresses.location[*]` | All elements of a nested array, reached by an explicit path |
| `$.parents..relationship` | Recursive descent within the `parents` subtree |
| `$..personal..number` | Nested recursive search |
| `$.parents[0]` | First element of an array by index |
| `$..location[0]` | First location at any depth |
| `$.parents[?(@.name =~ /^J/)]` | Array filter using a regular expression |
| `$.parents[?(@.occupation == 'Teacher' \|\| @.age >= 40)]` | Array filter with OR condition |

## Sample Data (`Data.json`)

The JSON file describes a person with:

- `name`, `age`
- `communication` — email addresses, location (city, coordinates, postal code), phone numbers
- `education` — institution, degrees, major
- `parents` — array of two objects (father and mother) with name, age, occupation, email, and relationship
- `miscellaneous` — array of hobby entries
- `messageHeader` — id and created date

## Requirements

- [.NET 6 SDK](https://dotnet.microsoft.com/download) (or later)

## NuGet Packages

| Package | Version |
|---|---|
| `Newtonsoft.Json` | 13.0.3 |

## Build and Run

```bash
cd JsonSearchPatterns
dotnet run
```

The application prints the results of each query to the console, separated by blank lines.

## Project Structure

```text
JsonSearchPatterns/
├── Data.json               # Sample person JSON document
├── FileHandler.cs          # Reads Data.json and runs SelectTokens queries
├── Program.cs              # Executes all query patterns and prints results
├── JsonSearchPatterns.csproj
└── JsonSearchPatterns.sln
```

## FAQ

**What is the difference between `SelectToken` and `SelectTokens`?**
`SelectToken` returns a single matching token (or `null`), while `SelectTokens` returns every match as an `IEnumerable<JToken>`. This project uses `SelectTokens` so that wildcard, recursive, and filter queries can return several results.

**Can I run the same queries with System.Text.Json?**
These examples rely on Newtonsoft.Json's built-in JSONPath support through `SelectTokens`; System.Text.Json does not offer this method.

## Related Examples

- [Python.NET interop — exchanging data between C# and Python](../PythonBridge/readme.md)
- [Back to all C# and .NET code examples](../README.md)

---

## Documentation links

[Read all C# and .NET Code Examples guides online](https://gcfernando.github.io/csharp_codes/documentation/) · [Project overview](https://gcfernando.github.io/csharp_codes/) · [All developer projects](https://gcfernando.github.io/gcfernando/projects/)
