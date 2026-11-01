using System;
using System.Diagnostics;
using System.Windows;
using UncensoredStudio.ViewModels;

namespace UncensoredStudio
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm;

        public MainWindow()
        {
            InitializeComponent();
            _vm = new MainViewModel();
            DataContext = _vm;

            Loaded += async (s, e) =>
            {
                await _vm.InitializeAsync();
            };

            Closing += (s, e) =>
            {
                // Explicitly terminate backend when window closes
                try
                {
                    var procs = Process.GetProcessesByName("koboldcpp");
                    foreach (var p in procs)
                    {
                        try { p.Kill(true); } catch { }
                    }
                }
                catch { }
            };
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
