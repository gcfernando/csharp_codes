using Python.Runtime;

namespace PythonBridge.PythonCaller;
internal static class IrisCode
{
    internal static void Execute()
    {
        Runtime.PythonDLL = "python312.dll";
        PythonEngine.Initialize();
        var mainState = PythonEngine.BeginAllowThreads();

        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var scriptFolder = Path.Combine(baseDir, "PyScript");
        var modelFile = Path.Combine(baseDir, "iris_nb.pkl");

        // Training section still requires GIL
        if (!File.Exists(modelFile))
        {
            Console.WriteLine("Model not found—running training");
            using (Py.GIL())
            {
                dynamic sys = Py.Import("sys");
                sys.path.append(scriptFolder);
                Py.Import("train_iris_model");
            }
        }
        else
        {
            Console.WriteLine("Model already exists.");
        }

        // Inference
        using (Py.GIL())
        {
            dynamic sys = Py.Import("sys");
            sys.path.append(scriptFolder);

            dynamic joblib = Py.Import("joblib");
            dynamic np = Py.Import("numpy");
            dynamic model = joblib.load(modelFile);

            double[,] input = { { 5.1, 3.5, 1.4, 0.2 } };
            dynamic arr = np.array(input);
            dynamic pred = model.predict(arr);
            Console.WriteLine($"Predicted class: {(int)pred[0]}");
        }

        PythonEngine.EndAllowThreads(mainState);
        PythonEngine.Shutdown();
    }
}