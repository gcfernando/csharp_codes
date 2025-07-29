using Python.Runtime;

namespace PythonBridge.PythonCaller;
internal static class ApiCode
{
    internal static void Execute()
    {
        Runtime.PythonDLL = "python312.dll";
        PythonEngine.Initialize();

        using (Py.GIL())
        {
            // Import sys and set the script folder path
            dynamic sys = Py.Import("sys");
            var scriptFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PyScript");
            sys.path.append(scriptFolder);

            // Import the FastAPI app from api.py
            dynamic api = Py.Import("api");
            var app = api.app;

            try
            {
                // Import uvicorn and run the FastAPI app
                dynamic uvicorn = Py.Import("uvicorn");
                uvicorn.run(app, host: "127.0.0.1", port: 8000, log_level: "info");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error running server: {ex.Message}");
                Console.WriteLine("Make sure FastAPI and uvicorn are installed.");
            }
        }

        PythonEngine.Shutdown();
    }
}
