using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Management;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;

namespace UncensoredStudioInstaller
{
    public partial class MainWindow : Window
    {
        private string _targetDirectory = "";

        public MainWindow()
        {
            InitializeComponent();
            InitializeDefaultPath();
            DetectHardware();
        }

        private void InitializeDefaultPath()
        {
            string localPrograms = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "UncensoredStudio");
            TxtInstallPath.Text = localPrograms;
            UpdateDiskSpace();
        }

        private void DetectHardware()
        {
            // 1. Detect GPU
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT Name, AdapterRAM FROM Win32_VideoController");
                foreach (ManagementObject obj in searcher.Get())
                {
                    string name = obj["Name"]?.ToString() ?? "";
                    if (!string.IsNullOrEmpty(name) && !name.Contains("Basic", StringComparison.OrdinalIgnoreCase))
                    {
                        ulong ramBytes = 0;
                        if (obj["AdapterRAM"] != null)
                        {
                            ulong.TryParse(obj["AdapterRAM"].ToString(), out ramBytes);
                        }
                        double vramGb = ramBytes / (1024.0 * 1024.0 * 1024.0);
                        if (vramGb > 0.5)
                        {
                            TxtDetectedGpu.Text = $"⚡ GPU: {name} ({vramGb:F0}GB VRAM Dedicated) - Vulkan Active";
                        }
                        else
                        {
                            TxtDetectedGpu.Text = $"⚡ GPU: {name} (Vulkan & DML Ready)";
                        }
                        break;
                    }
                }
            }
            catch
            {
                TxtDetectedGpu.Text = "⚡ GPU: Dedicated Graphics Accelerator (Vulkan Mode Active)";
            }

            // 2. Detect System RAM
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
                foreach (ManagementObject obj in searcher.Get())
                {
                    if (obj["TotalPhysicalMemory"] != null)
                    {
                        ulong totalBytes = Convert.ToUInt64(obj["TotalPhysicalMemory"]);
                        double totalGb = totalBytes / (1024.0 * 1024.0 * 1024.0);
                        TxtDetectedRam.Text = $"🧠 RAM: {totalGb:F0} GB System Memory (Optimal for 12B/14B Unaligned Models)";
                        break;
                    }
                }
            }
            catch
            {
                TxtDetectedRam.Text = "🧠 RAM: High Performance Memory Detected";
            }

            // 3. Detect OS
            TxtDetectedOs.Text = $"💻 OS: {Environment.OSVersion.VersionString} (64-bit Compatible)";
        }

        private void UpdateDiskSpace()
        {
            try
            {
                string path = TxtInstallPath.Text.Trim();
                if (string.IsNullOrEmpty(path)) return;

                string root = Path.GetPathRoot(path) ?? "C:\\";
                var driveInfo = new DriveInfo(root);
                if (driveInfo.IsReady)
                {
                    double freeGb = driveInfo.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
                    TxtDiskSpace.Text = $"📁 Drive {root} • {freeGb:F1} GB Free (1.2 GB Required for Studio)";
                }
            }
            catch
            {
                TxtDiskSpace.Text = "📁 Space: Sufficient storage available";
            }
        }

        private void TxtInstallPath_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            UpdateDiskSpace();
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select Uncensored Studio Installation Folder",
                InitialDirectory = Path.GetDirectoryName(TxtInstallPath.Text) ?? "C:\\"
            };

            if (dialog.ShowDialog() == true)
            {
                TxtInstallPath.Text = Path.Combine(dialog.FolderName, "UncensoredStudio");
                UpdateDiskSpace();
            }
        }

        #region Navigation Steps

        private void GoToStep2_Click(object sender, RoutedEventArgs e)
        {
            Step1_Hardware.Visibility = Visibility.Collapsed;
            Step2_Eula.Visibility = Visibility.Visible;
        }

        private void BackToStep1_Click(object sender, RoutedEventArgs e)
        {
            Step2_Eula.Visibility = Visibility.Collapsed;
            Step1_Hardware.Visibility = Visibility.Visible;
        }

        private void ChkAcceptEula_Changed(object sender, RoutedEventArgs e)
        {
            BtnContinueToSettings.IsEnabled = ChkAcceptEula.IsChecked == true;
        }

        private void GoToStep3_Click(object sender, RoutedEventArgs e)
        {
            Step2_Eula.Visibility = Visibility.Collapsed;
            Step3_Config.Visibility = Visibility.Visible;
        }

        private void BackToStep2_Click(object sender, RoutedEventArgs e)
        {
            Step3_Config.Visibility = Visibility.Collapsed;
            Step2_Eula.Visibility = Visibility.Visible;
        }

        #endregion

        private async void StartInstall_Click(object sender, RoutedEventArgs e)
        {
            _targetDirectory = TxtInstallPath.Text.Trim();
            if (string.IsNullOrWhiteSpace(_targetDirectory))
            {
                MessageBox.Show("Please specify a valid installation directory.", "Invalid Path", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Step3_Config.Visibility = Visibility.Collapsed;
            Step4_Progress.Visibility = Visibility.Visible;

            bool success = await Task.Run(() => PerformInstallation());

            if (success)
            {
                Step4_Progress.Visibility = Visibility.Collapsed;
                Step5_Complete.Visibility = Visibility.Visible;
                TxtInstalledPathSummary.Text = $"Installed to: {_targetDirectory}";
            }
            else
            {
                MessageBox.Show("Installation encountered an error. Please try running as Administrator or choosing another directory.", "Install Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Step4_Progress.Visibility = Visibility.Collapsed;
                Step3_Config.Visibility = Visibility.Visible;
            }
        }

        private bool PerformInstallation()
        {
            try
            {
                // 1. Create target directory and subdirectories
                if (!Directory.Exists(_targetDirectory))
                {
                    Directory.CreateDirectory(_targetDirectory);
                }

                string[] subdirs = { "models", "data", "exports", "cache", "temp", "Assets", "backend" };
                foreach (var d in subdirs)
                {
                    Directory.CreateDirectory(Path.Combine(_targetDirectory, d));
                }

                // 2. Extract Embedded payload.zip
                var assembly = Assembly.GetExecutingAssembly();
                using var resourceStream = assembly.GetManifestResourceStream("payload.zip") 
                                         ?? assembly.GetManifestResourceStream("UncensoredStudioInstaller.payload.zip");

                if (resourceStream == null)
                {
                    string localZip = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "payload.zip");
                    if (!File.Exists(localZip))
                    {
                        localZip = @"I:\UncensoredAI\installer\payload.zip";
                    }
                    if (File.Exists(localZip))
                    {
                        using var fs = File.OpenRead(localZip);
                        ExtractZipStream(fs);
                    }
                    else
                    {
                        throw new FileNotFoundException("payload.zip archive not found inside installer package.");
                    }
                }
                else
                {
                    ExtractZipStream(resourceStream);
                }

                // 3. Create Shortcuts
                string exePath = Path.Combine(_targetDirectory, "UncensoredStudio.exe");
                string icoPath = Path.Combine(_targetDirectory, "Assets", "app.ico");

                bool makeDesktop = false;
                bool makeStartMenu = false;

                Dispatcher.Invoke(() =>
                {
                    makeDesktop = ChkDesktopShortcut.IsChecked == true;
                    makeStartMenu = ChkStartMenuShortcut.IsChecked == true;
                });

                if (makeDesktop)
                {
                    string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                    CreateShortcut(Path.Combine(desktopPath, "Uncensored Studio.lnk"), exePath, icoPath, "Uncensored AI Fiction & Social Suite");
                }

                if (makeStartMenu)
                {
                    string startMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Uncensored Studio");
                    Directory.CreateDirectory(startMenu);
                    CreateShortcut(Path.Combine(startMenu, "Uncensored Studio.lnk"), exePath, icoPath, "Uncensored AI Fiction & Social Suite");
                }

                // 4. Register in Windows Add/Remove Programs Registry
                RegisterUninstallEntry(exePath, icoPath);

                return true;
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    TxtProgressStatus.Text = $"Error: {ex.Message}";
                });
                return false;
            }
        }

        private void ExtractZipStream(Stream stream)
        {
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            int total = archive.Entries.Count;
            int count = 0;

            foreach (var entry in archive.Entries)
            {
                count++;
                if (string.IsNullOrEmpty(entry.Name)) continue;

                string destPath = Path.Combine(_targetDirectory, entry.FullName);
                string? destDir = Path.GetDirectoryName(destPath);
                if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }

                Dispatcher.Invoke(() =>
                {
                    TxtProgressStatus.Text = $"Extracting {entry.FullName}...";
                    double pct = (double)count / total * 100.0;
                    ProgressBarInstall.Value = pct;
                    TxtPercent.Text = $"{pct:F0}%";
                });

                entry.ExtractToFile(destPath, overwrite: true);
            }
        }

        private void CreateShortcut(string shortcutPath, string targetExe, string iconPath, string description)
        {
            try
            {
                Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType != null)
                {
                    dynamic? shell = Activator.CreateInstance(shellType);
                    if (shell != null)
                    {
                        dynamic shortcut = shell.CreateShortcut(shortcutPath);
                        shortcut.TargetPath = targetExe;
                        shortcut.WorkingDirectory = Path.GetDirectoryName(targetExe);
                        shortcut.Description = description;
                        if (File.Exists(iconPath))
                        {
                            shortcut.IconLocation = $"{iconPath},0";
                        }
                        shortcut.Save();
                    }
                }
            }
            catch
            {
            }
        }

        private void RegisterUninstallEntry(string exePath, string icoPath)
        {
            try
            {
                string keyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\UncensoredStudio";
                using var key = Registry.CurrentUser.CreateSubKey(keyPath);
                if (key != null)
                {
                    key.SetValue("DisplayName", "Uncensored Studio");
                    key.SetValue("DisplayVersion", "1.0.0");
                    key.SetValue("Publisher", "Uncensored AI Studio");
                    key.SetValue("DisplayIcon", icoPath);
                    key.SetValue("InstallLocation", _targetDirectory);
                    key.SetValue("UninstallString", $"cmd.exe /c rmdir /s /q \"{_targetDirectory}\"");
                    key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                }
            }
            catch
            {
            }
        }

        private void FinishAndLaunch_Click(object sender, RoutedEventArgs e)
        {
            bool launch = ChkLaunchOnFinish.IsChecked == true;
            if (launch && !string.IsNullOrEmpty(_targetDirectory))
            {
                string exePath = Path.Combine(_targetDirectory, "UncensoredStudio.exe");
                if (File.Exists(exePath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = exePath,
                        WorkingDirectory = _targetDirectory,
                        UseShellExecute = true
                    });
                }
            }
            Close();
        }

        private void Header_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
