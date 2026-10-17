# Unsafe.As in C# — Reinterpret Memory Without Copying (.NET 8 Examples)

A .NET 8 console application demonstrating `System.Runtime.CompilerServices.Unsafe.As<TFrom, TTo>()` — a **zero-copy way to reinterpret memory in C#**, treating the bytes of one type as another without any data conversion or allocation.

## What Is Unsafe.As?

`Unsafe.As<TFrom, TTo>(ref TFrom source)` returns a managed reference (`ref TTo`) that points to the same memory as the input. It is a pure compile-time reinterpretation — no copy, no boxing, no conversion. The project enables `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` for the `unsafe` pointer arithmetic in Example 1.

## Examples

`Program.cs` contains four numbered examples; the first two (reading and then modifying a `long` through an `int` reference) are shown together below.

### Example 1 — `long` reinterpreted as `int`

Reinterprets a `long` value as a reference to its first 4 bytes as `int`. Modifying through the `int` reference updates the original `long` value's low 32 bits.

```text
Original long value: 42
As int (first 4 bytes): 42
As int (second 4 bytes): 0
After modifying through int reference, long value: 100
```

### Example 2 — `PointFloat` reinterpreted as `PointInt`

Reinterprets a `struct PointFloat { float X; float Y; }` as `PointInt { int X; int Y; }`. The displayed integer values are the raw IEEE 754 bit patterns of the float values.

```text
Original: (1, 2)
As PointInt: (1065353216, 1073741824)
```

### Example 3 — `byte[]` reinterpreted as `long`

Gets a `ref long` to the first element of a `byte[8]` array and reads or writes all 8 bytes at once through a single 64-bit write.

```text
Original bytes: [01, 02, 03, 04, 05, 06, 07, 08]
As long: 578437695752307201
After modification: [01, 02, 03, 04, 05, 06, 07, 08]
```

The bytes look unchanged after the modification because the code writes `0x0807060504030201`, which is the same value: on a little-endian CPU (x86/x64 and Arm64 on Windows), the lowest byte `01` is stored first. Writing a different value would visibly change the array.

## Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download)

## Build and Run

```bash
cd UnsafeAs
dotnet run
```

## Project Structure

```text
UnsafeAs/
├── Program.cs         # Four Unsafe.As examples (shown as three sections above)
├── PointFloat.cs      # struct PointFloat { float X; float Y; }
├── PointInt.cs        # struct PointInt { int X; int Y; }
├── UnsafeAs.csproj    # AllowUnsafeBlocks = true
└── UnsafeAs.sln
```

## Notes

`Unsafe.As` is intended for performance-critical scenarios such as binary serialisation, network protocol parsing, and SIMD-style operations. Misuse can corrupt memory or produce incorrect results; always ensure source and target types have compatible sizes and alignment.

The outputs shown above were confirmed by running the project.

## Related Examples

- [Nilog](https://github.com/gcfernando/nilog) — typed `ILogger` extensions that reduce call-site allocations in performance-sensitive .NET code
- [Back to all C# and .NET code examples](../README.md)

---

## Documentation links

[Read all C# and .NET Code Examples guides online](https://gcfernando.github.io/csharp_codes/documentation/) · [Project overview](https://gcfernando.github.io/csharp_codes/) · [All developer projects](https://gcfernando.github.io/gcfernando/projects/)
