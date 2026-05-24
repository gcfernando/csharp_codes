# JSON Search Patterns

A .NET 6 console application demonstrating nine JSONPath query patterns using `Newtonsoft.Json`'s `SelectTokens` method. The project serves as a practical reference for navigating complex, nested JSON structures.

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

```
JsonSearchPatterns/
├── Data.json               # Sample person JSON document
├── FileHandler.cs          # Reads Data.json and runs SelectTokens queries
├── Program.cs              # Executes all query patterns and prints results
├── JsonSearchPatterns.csproj
└── JsonSearchPatterns.sln
```
