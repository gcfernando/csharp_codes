<div align="center">
  <h1>🚀 C# and .NET Code Examples — Azure Functions, Async, SQL Server, MediatR and More ✨</h1>
  <p><strong>Small, runnable C#/.NET projects that each demonstrate one practical pattern: Azure Functions, dependency injection, async concurrency, SQL Server writes, Windows Forms, terminal UI, and AI integration.</strong></p>
</div>

# Overview

This repository is a hands-on collection of **C# code examples** for **.NET 6, .NET 8, and .NET 10**. Each project is intentionally small and focused on one concept, so you can read the code, run it, and reuse the pattern in your own application.

Topics include **Azure Functions isolated worker middleware**, **Durable Functions orchestration**, **keyed dependency injection**, **MediatR / CQRS**, **`SemaphoreSlim` throttling**, **`Task.WhenAll` exception handling**, **SQL Server bulk copy and table-valued parameter upserts**, **`FakeLogger` unit testing**, **JSONPath queries**, **`Unsafe.As`**, **Spectre.Console**, **Python.NET interop**, and **Azure OpenAI chat completion**.

> Think of it as a learning vault where each folder is a mini lab.

## Project Highlights

| Project | Focus | What it demonstrates | Target |
|---|---|---|---|
| [Durable](Durable/) | Azure Durable Functions | Stateful, chained orchestration pipelines in the isolated worker model | .NET 8 |
| [Fake](Fake/) | Logging + unit testing | Testing `ILogger` code with `FakeLogger<T>` from `Microsoft.Extensions.Diagnostics.Testing` and MSTest | .NET 8 |
| [FourKWinForm](FourKWinForm/) | Windows Forms | High-DPI (`SystemAware`) desktop form | .NET 8 |
| [JsonSearchPatterns](JsonSearchPatterns/) | JSON processing | Nine JSONPath query patterns with Newtonsoft.Json `SelectTokens` | .NET 6 |
| [KeyedService](KeyedService/) | Dependency injection | Keyed scoped services (`AddKeyedScoped`, `[FromKeyedServices]`) in Azure Functions | .NET 8 |
| [Mediat](Mediat/) | CQRS + MediatR | Multi-layer Azure Functions solution with commands, queries, and pipeline behaviours | .NET 8 |
| [Middleware](Middleware/) | Azure Functions | `IFunctionsWorkerMiddleware` request transformation and exception handling | .NET 6 / .NET 8 |
| [PublicIP](PublicIP/) | Networking | Fetching the public IP address from the ipify API with `HttpClient` | .NET 6 |
| [PythonBridge](PythonBridge/) | Interop | Calling Python from C# with Python.NET (`pythonnet`) | .NET 8 |
| [SemaphoreSlim](SemaphoreSlim/) | Concurrency | Limiting concurrent async downloads with `SemaphoreSlim` | .NET 8 |
| [Simple_ChatCompletion](Simple_ChatCompletion/) | AI integration | Azure OpenAI chat completion via the `Azure.AI.OpenAI` package and via raw REST | .NET 8 |
| [SpectreDemo](SpectreDemo/) | Terminal UI | Live panels, multi-task progress bars, and prompts with Spectre.Console | .NET 8 |
| [SQLWrites](SQLWrites/) | SQL Server | Three upsert strategies — per-row transactions, table-valued parameters, and bulk copy — with retry and optimistic concurrency | .NET 10 |
| [TaskErrors](TaskErrors/) | Async error handling | Capturing **all** exceptions from `Task.WhenAll`, not just the first | .NET 8 |
| [UnsafeAs](UnsafeAs/) | Low-level .NET | Zero-copy reinterpretation with `Unsafe.As<TFrom, TTo>` and unsafe pointers | .NET 8 |

Every project folder has its own readme with detailed explanations, configuration, and sample requests.

---

## 🧭 Repository Map

### ☁️ Azure Functions & Cloud Patterns
- [`Durable`](Durable/) — Durable Functions orchestration and function chaining (isolated worker, Functions v4)
- [`KeyedService`](KeyedService/) — keyed dependency injection with Azure Functions
- [`Mediat`](Mediat/) — CQRS and MediatR flow in Azure Functions
- [`Middleware`](Middleware/) — Azure Functions middleware examples for .NET 6 and .NET 8

### 🧠 Core .NET Concepts
- [`SemaphoreSlim`](SemaphoreSlim/) — throttling concurrent async work
- [`TaskErrors`](TaskErrors/) — understanding exceptions in async workflows
- [`UnsafeAs`](UnsafeAs/) — unsafe conversions and pointer-based operations
- [`JsonSearchPatterns`](JsonSearchPatterns/) — practical JSONPath traversal and filtering

### 🗄️ Data & Integration
- [`SQLWrites`](SQLWrites/) — SQL Server upsert strategies compared (normal, TVP, bulk)
- [`PythonBridge`](PythonBridge/) — bridging C# with Python
- [`Simple_ChatCompletion`](Simple_ChatCompletion/) — Azure OpenAI chat completion examples

### 🖥️ UI & UX
- [`FourKWinForm`](FourKWinForm/) — Windows Forms app with DPI awareness
- [`SpectreDemo`](SpectreDemo/) — polished terminal demos using Spectre.Console

### 🧪 Testing & Utilities
- [`Fake`](Fake/) — unit testing log output with `FakeLogger<T>`
- [`PublicIP`](PublicIP/) — quick public IP lookup sample

---

## ⚙️ Tech Stack

- C# / .NET 6, .NET 8, .NET 10
- Azure Functions v4 (isolated worker) and Durable Functions
- SQL Server (`Microsoft.Data.SqlClient`)
- MediatR
- Python.NET (`pythonnet`)
- Azure OpenAI (`Azure.AI.OpenAI`)
- Spectre.Console
- Newtonsoft.Json
- MSTest and `Microsoft.Extensions.Diagnostics.Testing`
- Windows Forms

---

## 🚀 Quick Start

Each project is self-contained. Install the [.NET SDK](https://dotnet.microsoft.com/download) version that the project targets (see the table above), pick one folder, and run it from there:

```bash
git clone https://github.com/gcfernando/csharp_codes.git
cd csharp_codes

# Example: run a single console project
cd SpectreDemo
dotnet run
```

Every project includes a Visual Studio solution (`.sln`) file. Folders that contain several projects (for example `SQLWrites`, `Mediat`, and `Fake`) need you to choose which project to run — open the solution in Visual Studio, or pass the project path from the repository root, for example:

```bash
dotnet run --project SQLWrites/UseNormal
```

The Azure Functions samples (`Durable`, `KeyedService`, `Mediat`, `Middleware`) require [Azure Functions Core Tools](https://learn.microsoft.com/azure/azure-functions/functions-run-local) and are started from the function project folder with:

```bash
func start
```

Some samples need external resources — for example a SQL Server database (`SQLWrites`), an Azure OpenAI endpoint and key (`Simple_ChatCompletion`), or a local Python installation (`PythonBridge`). The project readme lists what is required.

---

## ❓ FAQ

**What is this repository for?**
It is a reference of focused C#/.NET examples. Each folder answers one practical question, such as “how do I add middleware to an Azure Functions isolated worker app?” or “how do I catch every exception from `Task.WhenAll`?”.

**Which .NET version do I need?**
It depends on the project: most target .NET 8, `JsonSearchPatterns`, `PublicIP`, and the `Middleware/Dotnet6` samples target .NET 6, and `SQLWrites` targets .NET 10. `FourKWinForm` requires Windows.

**Are these production-ready libraries?**
No. They are learning-oriented demos. For production-grade packages, see [Nilog](https://github.com/gcfernando/nilog) (zero-allocation `ILogger` extensions) and [Glass.Message](https://github.com/gcfernando/glass) (a modern WinForms `MessageBox` replacement).

**Where can I find similar examples in Python?**
See the companion [Python projects collection](https://github.com/gcfernando/python_codes) — its `LoadLibraries` project shows the reverse direction of `PythonBridge`: calling a .NET library from Python.

---

## 💡 Notes

- Most projects are learning-oriented and intentionally compact.
- This repo focuses on experiments, demos, and practical patterns rather than production-ready apps.
- Several projects can be opened directly in Visual Studio or run via `dotnet run`.

---

## 🌟 Summary

This is a compact, colorful collection of .NET experiments covering cloud-native patterns, async techniques, AI integration, UI work, database writes, and system-level programming. It is a great sandbox for learning by building small, focused examples.

If you want to explore the repo in a more hands-on way, start with:

1. [`Mediat`](Mediat/) for architecture patterns
2. [`Middleware`](Middleware/) for Azure Functions internals
3. [`SQLWrites`](SQLWrites/) for database performance patterns
4. [`SpectreDemo`](SpectreDemo/) for terminal UX magic
5. [`PythonBridge`](PythonBridge/) for cross-language integration

## 🔗 Related Projects

- [Python projects](https://github.com/gcfernando/python_codes) — standalone Python scripts and apps
- [VB.NET Windows Forms examples](https://github.com/gcfernando/vb_codes) — Visual Basic desktop experiments
- [Developer knowledge base](https://github.com/gcfernando/document) — includes a complete C#/.NET OOP guide and an Azure cheat sheet

<div align="center">
  <p><strong>Happy coding! 💻✨</strong></p>
</div>

---

## Documentation links

[Read all C# and .NET Code Examples guides online](https://gcfernando.github.io/csharp_codes/documentation/) · [Project overview](https://gcfernando.github.io/csharp_codes/) · [All developer projects](https://gcfernando.github.io/gcfernando/projects/)
