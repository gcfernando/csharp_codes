# FourKWinForm — High-DPI Windows Forms Example in .NET 8 (C#)

A minimal C# Windows Forms application targeting .NET 8 that demonstrates **high-DPI awareness** for 4K and other high-resolution displays. It enables `HighDpiMode.SystemAware` through the project's `ApplicationHighDpiMode` setting, so text and controls scale with the Windows display scaling instead of appearing tiny or blurry. The form collects a name and country from two text boxes, then displays a personalised greeting in a message box.

Use it as a starting point when you want to see how a modern .NET 8 WinForms project configures DPI scaling with `ApplicationConfiguration.Initialize()`.

## Features

- High-DPI support via `ApplicationHighDpiMode.SystemAware`
- Two text inputs: **Name** and **Country**
- **Message** button — shows a greeting dialog using the entered values
- **Exit** button — closes the application cleanly

## Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download) (Windows target)
- Windows OS (Windows Forms is Windows-only)

## Build and Run

```bash
cd FourKWinForm
dotnet build
dotnet run
```

Or open `FourKWinForm.sln` in Visual Studio and press **F5**.

## Usage

1. Launch the application.
2. Enter your name and country in the text boxes.
3. Click **Message** to display:

```text
Hello my name is <name>,
I am from <country>
```

4. Click **Exit** to close the application.

## Project Configuration

| Setting | Value |
|---|---|
| Target Framework | `net8.0-windows` |
| Output Type | `WinExe` |
| DPI Mode | `SystemAware` |
| Designer DPI Unaware | `true` |

## Project Structure

```text
FourKWinForm/
├── HDForm.cs              # Main form logic — button handlers
├── HDForm.Designer.cs     # Auto-generated form layout
├── HDForm.resx            # Form resources
├── Program.cs             # Entry point — initialises ApplicationConfiguration
├── FourKWinForm.csproj
└── FourKWinForm.sln
```

## Related

- [Glass.Message](https://github.com/gcfernando/glass) — a DPI-aware, themed `MessageBox` replacement for Windows Forms that could replace the standard greeting dialog
- [Back to the C# and .NET code examples](../README.md)

---

## Documentation links

[Read all C# and .NET Code Examples guides online](https://gcfernando.github.io/csharp_codes/documentation/) · [Project overview](https://gcfernando.github.io/csharp_codes/) · [All developer projects](https://gcfernando.github.io/gcfernando/projects/)
