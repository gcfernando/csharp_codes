using System.Runtime.CompilerServices;
using UnsafeAs;

Console.WriteLine("Built-in Unsafe.As<> Demo");
Console.WriteLine("-----------------------");

// Example 1: Basic usage with primitive types
long longValue = 42;
ref var intRef = ref Unsafe.As<long, int>(ref longValue);
Console.WriteLine($"Original long value: {longValue}");

unsafe
{
    // No need for fixed here since longValue is already pinned as a local
    var pLong = &longValue;
    Console.WriteLine($"As int (first 4 bytes): {intRef}");
    Console.WriteLine($"As int (second 4 bytes): {*(((int*)pLong) + 1)}");
}

// Example 2: Changing the value through the reference
intRef = 100;
Console.WriteLine($"After modifying through int reference, long value: {longValue}");
Console.WriteLine();

// Example 3: With custom struct types
var pointFloat = new PointFloat { X = 1.0f, Y = 2.0f };
ref var pointInt = ref Unsafe.As<PointFloat, PointInt>(ref pointFloat);
Console.WriteLine($"Original: ({pointFloat.X}, {pointFloat.Y})");
Console.WriteLine($"As PointInt: ({pointInt.X}, {pointInt.Y})");

// Example 4: With arrays
Console.WriteLine("\nArray example:");
var bytes = new byte[8];
for (var i = 0; i < bytes.Length; i++)
{
    bytes[i] = (byte)(i + 1);
}

PrintByteArray("Original bytes", bytes);

// Get a reference to the first element
ref var firstByte = ref bytes[0];

// Reinterpret as a reference to a long
ref var longRef = ref Unsafe.As<byte, long>(ref firstByte);
Console.WriteLine($"As long: {longRef}");

// Modify through the long reference
longRef = 0x0807060504030201;
PrintByteArray("After modification", bytes);

Console.WriteLine("\nPress any key to exit...");
Console.ReadKey();

static void PrintByteArray(string label, byte[] array)
{
    Console.Write($"{label}: [");
    for (var i = 0; i < array.Length; i++)
    {
        Console.Write($"{array[i]:X2}");
        if (i < array.Length - 1)
        {
            Console.Write(", ");
        }
    }
    Console.WriteLine("]");
}