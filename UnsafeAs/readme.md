# UnsafeAs — Unsafe.As<TFrom, TTo> Demo

A .NET 8 console application demonstrating `System.Runtime.CompilerServices.Unsafe.As<TFrom, TTo>()` — a zero-copy method for reinterpreting the memory of one type as another without any data conversion or allocation.

## What Is Unsafe.As?

`Unsafe.As<TFrom, TTo>(ref TFrom source)` returns a managed reference (`ref TTo`) that points to the same memory as the input. It is a pure compile-time reinterpretation — no copy, no boxing, no conversion. The project enables `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` for the `unsafe` pointer arithmetic in Example 1.

## Examples

### Example 1 — `long` reinterpreted as `int`

Reinterprets a `long` value as a reference to its first 4 bytes as `int`. Modifying through the `int` reference updates the original `long` value's low 32 bits.

```
Original long value: 42
As int (first 4 bytes): 42
As int (second 4 bytes): 0
After modifying through int reference, long value: 100
```

### Example 2 — `PointFloat` reinterpreted as `PointInt`

Reinterprets a `struct PointFloat { float X; float Y; }` as `PointInt { int X; int Y; }`. The displayed integer values are the raw IEEE 754 bit patterns of the float values.

```
Original: (1, 2)
As PointInt: (1065353216, 1073741824)
```

### Example 3 — `byte[]` reinterpreted as `long`

Gets a `ref long` to the first element of a `byte[8]` array and reads or writes all 8 bytes at once through a single 64-bit write.

```
Original bytes: [01, 02, 03, 04, 05, 06, 07, 08]
As long: 578437695752307201
After modification: [01, 02, 03, 04, 05, 06, 07, 08]
```

## Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download)

## Build and Run

```bash
cd UnsafeAs
dotnet run
```

## Project Structure

```
UnsafeAs/
├── Program.cs         # Four Unsafe.As examples
├── PointFloat.cs      # struct PointFloat { float X; float Y; }
├── PointInt.cs        # struct PointInt { int X; int Y; }
├── UnsafeAs.csproj    # AllowUnsafeBlocks = true
└── UnsafeAs.sln
```

## Notes

`Unsafe.As` is intended for performance-critical scenarios such as binary serialisation, network protocol parsing, and SIMD-style operations. Misuse can corrupt memory or produce incorrect results; always ensure source and target types have compatible sizes and alignment.
