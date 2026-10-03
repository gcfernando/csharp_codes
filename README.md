<div align="center">
  <h1><span style="color:#ff6b6b">🚀</span> CSharp Projects Collection <span style="color:#4ecdc4">✨</span></h1>
  <p><strong>A colorful mix of .NET experiments, Azure patterns, async learning, and practical coding demos.</strong></p>
</div>

# Overview

This repository is a hands-on playground of C# and .NET experiments. Each project is intentionally small, focused, and designed to explore a specific concept: Azure Functions, dependency injection, async concurrency, database writes, UI development, terminal UX, and AI-powered integrations.

> Think of it as a learning vault where each folder is a mini lab.

## Project Highlights

| Project | Focus | Highlights |
|---|---|---|
| <span style="color:#ff9900">Durable</span> | Azure Durable Functions | Workflow orchestration and function chaining |
| <span style="color:#00c2ff">Fake</span> | Logging + unit testing | Fake data sample with test coverage |
| <span style="color:#7c4dff">FourKWinForm</span> | Windows Forms | High-DPI desktop UI in .NET 8 |
| <span style="color:#00d084">JsonSearchPatterns</span> | JSON processing | Search and transform patterns for JSON data |
| <span style="color:#ff6b6b">KeyedService</span> | Dependency Injection | Keyed scoped services for Azure Functions |
| <span style="color:#ffb703">Mediat</span> | CQRS + MediatR | Clean architecture with mediator pipelines |
| <span style="color:#00b4d8">Middleware</span> | Azure Functions | Request middleware, exception handling, and function interception |
| <span style="color:#2ec4b6">PublicIP</span> | Networking | Fetching the public IP via HTTP |
| <span style="color:#ff6f91">PythonBridge</span> | Interop | Calling Python from .NET |
| <span style="color:#8ac926">SemaphoreSlim</span> | Concurrency | Thread safety and async synchronization |
| <span style="color:#3a86ff">Simple_ChatCompletion</span> | AI integration | OpenAI-style chat completion via package and REST |
| <span style="color:#fb5607">SpectreDemo</span> | Terminal UI | Rich CLI dashboards and progress interactions |
| <span style="color:#ffbe0b">SQLWrites</span> | SQL Server | Bulk, normal, and TVP write strategies |
| <span style="color:#8338ec">TaskErrors</span> | Async error handling | Task exception patterns and debugging |
| <span style="color:#06d6a0">UnsafeAs</span> | Unsafe code | Low-level conversions and memory-oriented techniques |

---

## 🧭 Repository Map

### ☁️ Azure & Cloud Patterns
- `Durable` — orchestration-based Azure Functions demos
- `KeyedService` — keyed dependency injection with Azure Functions
- `Mediat` — CQRS and MediatR flow in Azure Functions
- `Middleware` — function middleware examples in .NET 6 and .NET 8

### 🧠 Core .NET Concepts
- `SemaphoreSlim` — concurrency and async locking patterns
- `TaskErrors` — understanding exceptions in async workflows
- `UnsafeAs` — unsafe conversions and pointer-based operations
- `JsonSearchPatterns` — practical JSON traversal and matching patterns

### 🗄️ Data & Integration
- `SQLWrites` — several SQL Server write strategies and comparison patterns
- `PythonBridge` — bridging C# with Python workflows
- `Simple_ChatCompletion` — AI chat interaction examples

### 🖥️ UI & UX
- `FourKWinForm` — Windows Forms app with DPI awareness
- `SpectreDemo` — polished terminal demos using Spectre.Console

### 🧪 Utilities & Experiments
- `Fake` — fake data generation and test-oriented example
- `PublicIP` — quick public IP lookup sample

---

## ⚙️ Tech Stack

- C# / .NET
- Azure Functions
- ASP.NET Core / Worker model
- SQL Server
- MediatR
- Python interop
- Spectre.Console
- Dependency Injection / Keyed Services

---

## 🚀 Quick Start

Each project is usually self-contained. Pick one folder and run it from there:

```bash
git clone https://github.com/gcfernando/csharp_codes.git
cd csharp_codes

# Example: run a console project
cd SQLWrites
dotnet restore
dotnet run
```

Some projects are Azure Function samples and require:

```bash
func start
```

Or a project-specific solution file may already exist under that folder.

---

## 💡 Notes

- Most projects are learning-oriented and intentionally compact.
- This repo focuses on experiments, demos, and practical patterns rather than production-ready apps.
- Several projects can be opened directly in Visual Studio or run via `dotnet run`.

---

## 🌟 Summary

This is a compact, colorful collection of .NET experiments covering cloud-native patterns, async techniques, AI integration, UI work, database writes, and system-level programming. It is a great sandbox for learning by building small, focused examples.

If you want to explore the repo in a more hands-on way, start with:

1. `Mediat` for architecture patterns
2. `Middleware` for Azure Functions internals
3. `SQLWrites` for database performance patterns
4. `SpectreDemo` for terminal UX magic
5. `PythonBridge` for cross-language integration

<div align="center">
  <p><strong>Happy coding! 💻✨</strong></p>
</div>
