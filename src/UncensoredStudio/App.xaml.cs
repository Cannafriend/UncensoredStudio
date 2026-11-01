using System;
using System.Diagnostics;
using System.Windows;
using UncensoredStudio.Services;

namespace UncensoredStudio
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            this.DispatcherUnhandledException += (s, args) =>
            {
                try
                {
                    string logPath = @"I:\UncensoredAI\crash.log";
                    System.IO.File.AppendAllText(logPath, $"[{DateTime.UtcNow:O}] UI EXCEPTION: {args.Exception}\n\n");
                    MessageBox.Show($"Uncensored Studio Error:\n{args.Exception.Message}\n\nStack:\n{args.Exception.StackTrace}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch { }
                args.Handled = true;
            };

            // Clean up any stale engine processes from prior sessions before launch
            CleanupBackendProcesses();

            AppDomain.CurrentDomain.ProcessExit += (s, args) => CleanupBackendProcesses();
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                try
                {
                    string logPath = @"I:\UncensoredAI\crash.log";
                    System.IO.File.AppendAllText(logPath, $"[{DateTime.UtcNow:O}] DOMAIN EXCEPTION: {args.ExceptionObject}\n\n");
                }
                catch { }
                CleanupBackendProcesses();
            };
        }

        protected override void OnExit(ExitEventArgs e)
        {
            CleanupBackendProcesses();
            base.OnExit(e);
        }

        private static void CleanupBackendProcesses()
        {
            try
            {
                var procs = Process.GetProcessesByName("koboldcpp");
                foreach (var p in procs)
                {
                    try
                    {
                        p.Kill(true);
                        p.WaitForExit(1000);
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
        }
    }
}
