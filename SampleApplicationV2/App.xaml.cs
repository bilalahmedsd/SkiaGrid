using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace SampleApplicationV2
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        // Demo builds write any unhandled exception next to the executable instead of dying
        // silently - a crash in the middle of a live demo is worse than a logged error.
        private static readonly string CrashLog =
            Path.Combine(AppContext.BaseDirectory, "crash.log");

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
                Write("AppDomain.UnhandledException", args.ExceptionObject as Exception);
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Write("DispatcherUnhandledException", e.Exception);
            MessageBox.Show(
                e.Exception.GetType().Name + ": " + e.Exception.Message +
                "\n\nLogged to:\n" + CrashLog,
                "SampleApplicationV2 - recovered from an error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            e.Handled = true;   // keep the demo alive
        }

        private static void Write(string source, Exception? ex)
        {
            try
            {
                File.AppendAllText(CrashLog,
                    $"=== {DateTime.Now:yyyy-MM-dd HH:mm:ss}  {source} ==={Environment.NewLine}" +
                    (ex?.ToString() ?? "(no exception object)") +
                    Environment.NewLine + Environment.NewLine);
            }
            catch
            {
                // nothing useful to do if even logging fails
            }
        }
    }
}
