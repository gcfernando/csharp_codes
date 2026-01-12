using System;
using System.Threading;
using System.Windows.Forms;

namespace Spectrum
{
    internal static class Program
    {
        private static Mutex _mutex;

        [STAThread]
        private static void Main()
        {
            bool createdNew;
            _mutex = new Mutex(true, "Spectrum_SingleInstance_Mutex", out createdNew);

            if (!createdNew)
            {
                MessageBox.Show(
                    "Application is already running.",
                    "Spectrum",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            Application.SetCompatibleTextRenderingDefault(false);

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

            Application.ThreadException += (s, e) =>
            {
                MessageBox.Show(
                    e.Exception.ToString(),
                    "Unexpected error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            };

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                MessageBox.Show(
                    e.ExceptionObject?.ToString() ?? "Unhandled exception",
                    "Fatal error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            };

            Application.Run(new FormAudioSpectrum());
            _mutex.ReleaseMutex();
        }
    }
}