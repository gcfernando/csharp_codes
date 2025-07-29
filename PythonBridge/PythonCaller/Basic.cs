using Python.Runtime;

namespace PythonBridge.PythonCaller;
internal static class Basic
{
    internal static void Execute()
    {
        Runtime.PythonDLL = "python312.dll";
        PythonEngine.Initialize();

        using (Py.GIL()) // Acquire Global Interpreter Lock
        {
            dynamic sys = Py.Import("sys");
            var scriptFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PyScript");
            sys.path.append(scriptFolder);

            // Import our Python module
            dynamic basicOps = Py.Import("basic_operations");

            // Call Python functions
            var sum = basicOps.add_numbers(10, 20);
            var product = basicOps.multiply_numbers(5, 6);
            var greeting = basicOps.get_greeting("John");

            // Access module-level variable
            var piValue = basicOps.PI;

            // Display results
            Console.WriteLine($"Sum: {sum}");
            Console.WriteLine($"Product: {product}");
            Console.WriteLine($"Greeting: {greeting}");
            Console.WriteLine($"PI Value: {piValue}");
        }

        PythonEngine.Shutdown();
    }
}
