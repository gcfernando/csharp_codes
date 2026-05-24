# PythonBridge

A .NET 8 console application demonstrating how to call Python code from C# using [Python.NET](https://github.com/pythonnet/pythonnet) (`pythonnet` 3.0.5). The project covers four integration scenarios — from basic arithmetic to ML model inference and launching a FastAPI server.

## Overview

Python.NET embeds a Python interpreter (CPython) inside the .NET process. The `Py.GIL()` block acquires the Global Interpreter Lock, after which you can import Python modules, call functions, and exchange objects between C# and Python dynamically.

## Scenarios

### 1. Basic Operations (`Basic.Execute`)

Imports `basic_operations.py` and calls:

| Python Function | Description |
|---|---|
| `add_numbers(10, 20)` | Returns the sum |
| `multiply_numbers(5, 6)` | Returns the product |
| `get_greeting("John")` | Returns a greeting string |

Also reads the module-level constant `PI`.

### 2. Log Formatting (`LogFormat.Execute`)

Imports `logger.py` inside a named Python scope. Passes C# `LogEntry` objects (`Level`, `Message`) to `logger.format_entry(entry)` and prints the formatted output.

### 3. Iris ML Inference (`IrisCode.Execute`)

- If `iris_nb.pkl` does not exist, runs `train_iris_model.py` to train a Naive Bayes classifier on the Iris dataset and save the model
- Loads the saved model with `joblib`, builds a NumPy input array, and prints the predicted class for `[5.1, 3.5, 1.4, 0.2]`
- Uses `PythonEngine.BeginAllowThreads` / `EndAllowThreads` to release the GIL during non-Python work

### 4. FastAPI Server (`ApiCode.Execute`)

Imports `api.py` (which defines a FastAPI `app`) and starts it with `uvicorn` on `127.0.0.1:8000`.

## Python Scripts (`PyScript/`)

| Script | Description |
|---|---|
| `basic_operations.py` | `add_numbers`, `multiply_numbers`, `get_greeting`, and `PI` constant |
| `logger.py` | `format_entry(entry)` — formats a log entry object |
| `train_iris_model.py` | Trains a Naive Bayes classifier on Iris data, saves `iris_nb.pkl` |
| `api.py` | FastAPI application |

## Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- **Python 3.12** installed with `python312.dll` accessible (system PATH or application directory)
- Python packages:

```bash
pip install joblib numpy scikit-learn fastapi uvicorn
```

## NuGet Packages

| Package | Version |
|---|---|
| `pythonnet` | 3.0.5 |

## Build and Run

```bash
cd PythonBridge
dotnet run
```

The application runs all four scenarios sequentially and waits for a key press before exiting.

## Project Structure

```
PythonBridge/
├── PyScript/
│   ├── basic_operations.py      # Arithmetic and greeting functions
│   ├── logger.py                # Log entry formatter
│   ├── train_iris_model.py      # Iris model training script
│   └── api.py                   # FastAPI application
├── PythonCaller/
│   ├── Basic.cs                 # Calls basic_operations.py
│   ├── LogFormat.cs             # Calls logger.py with LogEntry objects
│   ├── IrisCode.cs              # Trains or loads Iris model, runs inference
│   └── ApiCode.cs               # Starts FastAPI server via uvicorn
├── LogEntry.cs                  # Log entry DTO (Level, Message)
├── Program.cs                   # Entry point — runs all four scenarios
├── PythonBridge.csproj
└── PythonBridge.sln
```
