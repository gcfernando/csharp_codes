# FourKWinForm

A Windows Forms application targeting .NET 8 that demonstrates high-DPI awareness. The form collects a name and country from two text boxes, then displays a personalised greeting in a message box.

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

```
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

```
FourKWinForm/
├── HDForm.cs              # Main form logic — button handlers
├── HDForm.Designer.cs     # Auto-generated form layout
├── HDForm.resx            # Form resources
├── Program.cs             # Entry point — initialises ApplicationConfiguration
├── FourKWinForm.csproj
└── FourKWinForm.sln
```
