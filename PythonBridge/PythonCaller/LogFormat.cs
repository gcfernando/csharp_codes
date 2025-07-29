using Python.Runtime;

namespace PythonBridge.PythonCaller;
internal static class LogFormat
{
    internal static void Execute()
    {
        Runtime.PythonDLL = "python312.dll";
        PythonEngine.Initialize();

        using (Py.GIL())
        {
            dynamic sys = Py.Import("sys");
            var scriptFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PyScript");
            sys.path.append(scriptFolder);

            using var scope = Py.CreateScope("loggerScope");
            scope.Import("logger");

            var entries = new[]
            {
                new LogEntry { Level = "INFO", Message = "Started" },
                new LogEntry { Level = "INFO", Message = "Completed" }
            };

            foreach (var entry in entries)
            {
                scope.Set("entry", entry.ToPython());
                scope.Exec("out = logger.format_entry(entry)");
                WriteOut(scope);
            }
        }

        PythonEngine.Shutdown();
    }

    private static void WriteOut(PyModule scope)
    {
        dynamic outValue = scope.Get("out");
        Console.WriteLine((string)outValue);
    }
}
