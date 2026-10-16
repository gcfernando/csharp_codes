# Spectre.Console Example in C# — Live Panels, Progress Bars and Prompts (.NET 8)

A .NET 8 console application demonstrating how to build a **rich terminal UI in C#** with the [Spectre.Console](https://spectreconsole.net/) library. The app simulates an interactive data collection workflow with live panels, multi-task progress bars, and a selection prompt — all with coloured text and animated UI elements.

## Features

- **Live panel** — updates in-place through four stages: Connecting, Fetching Data, Processing, and Complete
- **Multi-task progress bar** — three parallel tasks (Fetching User Data, Downloading Reports, Analyzing Logs) with percentage, spinner, and remaining-time columns
- **Selection prompt** — asks whether to restart the workflow or exit
- **Continuous loop** — the entire workflow repeats until the user selects "No"

## Workflow

1. Live panel cycles through four status messages with brief delays
2. Progress bar advances three tasks to completion (each at a different increment rate)
3. User is prompted to restart or exit

## Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download)

## NuGet Packages

| Package | Version |
|---|---|
| `Spectre.Console` | 0.49.1 |
| `Spectre.Console.Cli` | 0.49.1 |

## Build and Run

```bash
cd SpectreDemo
dotnet run
```

## Project Structure

```text
SpectreDemo/
├── Program.cs          # Full application — live panel, progress bar, selection prompt
├── SpectreDemo.csproj
└── SpectreDemo.sln
```

## Spectre.Console APIs Used

| API | Used for |
|---|---|
| `AnsiConsole.Live` with `Panel` and `UpdateTarget` | Updating a panel in place |
| `AnsiConsole.Progress` with `AddTask` and `Increment` | Several progress bars advancing together |
| `SelectionPrompt<string>` with `AddChoices` | The restart / exit prompt |
| Markup such as `[green]...[/]` | Coloured and styled text |

`Spectre.Console.Cli` is referenced in the project file but not used by `Program.cs`.

## Related Examples

- [SemaphoreSlim concurrency example](../SemaphoreSlim/readme.md)
- [Back to all C# and .NET code examples](../README.md)

---

## Documentation links

[Read all C# and .NET Code Examples guides online](https://gcfernando.github.io/csharp_codes/documentation/) · [Project overview](https://gcfernando.github.io/csharp_codes/) · [All developer projects](https://gcfernando.github.io/gcfernando/projects/)
