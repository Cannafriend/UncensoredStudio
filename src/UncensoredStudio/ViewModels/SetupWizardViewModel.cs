using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using UncensoredStudio.Models;
using UncensoredStudio.Services;

namespace UncensoredStudio.ViewModels
{
    public partial class SetupWizardViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;
        private readonly BackendProcessService _backendService;
        private readonly LLMService _llmService;

        private CancellationTokenSource? _downloadCts;

        public event Action? SetupCompleted;

        [ObservableProperty]
        private string _modelsDirectory = @"I:\UncensoredAI\models";

        [ObservableProperty]
        private ObservableCollection<DownloadableModelInfo> _modelCatalog = new();

        [ObservableProperty]
        private DownloadableModelInfo? _selectedModelChoice;

        [ObservableProperty]
        private bool _isDownloading = false;

        [ObservableProperty]
        private double _downloadProgress = 0;

        [ObservableProperty]
        private string _statusMessage = "Ready to set up your creative writing AI suite";

        [ObservableProperty]
        private string _loadedModelName = "Magnum-12B-v2";

        [ObservableProperty]
        private int _currentStep = 1; // 1: Choose Path & Model, 2: Downloading & Verifying, 3: Completed Ready Screen

        public SetupWizardViewModel(
            DatabaseService databaseService,
            BackendProcessService backendService,
            LLMService llmService)
        {
            _databaseService = databaseService;
            _backendService = backendService;
            _llmService = llmService;

            InitializeCatalog();
        }

        private void InitializeCatalog()
        {
            ModelCatalog = new ObservableCollection<DownloadableModelInfo>
            {
                new DownloadableModelInfo
                {
                    Id = "magnum_12b",
                    Name = "Magnum-12B-v2 (Mistral Nemo)",
                    Description = "Top recommendation for high-grade literary prose, romance, adult fiction, vivid sensory detail, and spicy Twitter posts.",
                    RecommendedFor = "⭐ Recommended: Best Prose, Vocabulary & Fiction Quality",
                    SizeDisplay = "~7.5 GB (Q4_K_M)",
                    ParameterCount = "12 Billion",
                    FileName = "magnum-12b-v2-Q4_K_M.gguf",
                    DownloadUrl = "https://huggingface.co/bartowski/magnum-12b-v2-GGUF/resolve/main/magnum-12b-v2-Q4_K_M.gguf"
                },
                new DownloadableModelInfo
                {
                    Id = "stheno_8b",
                    Name = "Llama-3.1-8B-Stheno-v3.2",
                    Description = "Blazing fast inference with sharp character voices, reactive dialogue, and high emotional intelligence.",
                    RecommendedFor = "⚡ High-Speed: Fast Story Progression & RP",
                    SizeDisplay = "~4.9 GB (Q4_K_M)",
                    ParameterCount = "8 Billion",
                    FileName = "L3-8B-Stheno-v3.2-Q4_K_M.gguf",
                    DownloadUrl = "https://huggingface.co/bartowski/L3-8B-Stheno-v3.2-GGUF/resolve/main/L3-8B-Stheno-v3.2-Q4_K_M.gguf"
                },
                new DownloadableModelInfo
                {
                    Id = "both_models",
                    Name = "Download Both (Magnum 12B + Stheno 8B)",
                    Description = "Installs both models so you can switch between deep literary prose and rapid character chat anytime.",
                    RecommendedFor = "👑 Complete Suite: Best of Both Worlds",
                    SizeDisplay = "~12.4 GB Total",
                    ParameterCount = "12B + 8B",
                    FileName = "both",
                    DownloadUrl = "both"
                },
                new DownloadableModelInfo
                {
                    Id = "skip_custom",
                    Name = "Skip Download / Use Existing GGUF",
                    Description = "Select this if you already have downloaded .gguf models on your machine.",
                    RecommendedFor = "Custom: Bring Your Own Models",
                    SizeDisplay = "0 GB",
                    ParameterCount = "Existing",
                    FileName = "",
                    DownloadUrl = ""
                }
            };

            SelectedModelChoice = ModelCatalog[0];
        }

        public async Task LoadSettingsAsync()
        {
            var settings = await _databaseService.GetSettingsAsync();
            if (!string.IsNullOrEmpty(settings.ModelsDirectory))
            {
                ModelsDirectory = settings.ModelsDirectory;
            }
        }

        [RelayCommand]
        public void BrowseDirectory()
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select Directory to Store AI Models",
                InitialDirectory = Directory.Exists(ModelsDirectory) ? ModelsDirectory : @"I:\UncensoredAI"
            };

            if (dialog.ShowDialog() == true)
            {
                ModelsDirectory = dialog.FolderName;
            }
        }

        [RelayCommand]
        public async Task StartSetupProcessAsync()
        {
            if (SelectedModelChoice == null) return;

            if (!Directory.Exists(ModelsDirectory))
            {
                try
                {
                    Directory.CreateDirectory(ModelsDirectory);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not create directory: {ex.Message}", "Invalid Directory", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            var settings = await _databaseService.GetSettingsAsync();
            settings.ModelsDirectory = ModelsDirectory;
            await _databaseService.SaveSettingsAsync(settings);

            CurrentStep = 2;
            IsDownloading = true;
            _downloadCts = new CancellationTokenSource();

            // 1. Verify dependencies
            StatusMessage = "Checking backend engine dependencies...";
            bool depsOk = await _backendService.EnsureDependenciesAsync();
            if (!depsOk)
            {
                StatusMessage = "Failed to verify backend engine.";
                IsDownloading = false;
                return;
            }

            string primaryModelPath = "";

            // Check if model already exists locally
            string targetCheck = Path.Combine(ModelsDirectory, SelectedModelChoice.FileName);
            if (!string.IsNullOrEmpty(SelectedModelChoice.FileName) && File.Exists(targetCheck))
            {
                primaryModelPath = targetCheck;
                DownloadProgress = 100;
                StatusMessage = "Model already downloaded!";
            }
            else if (SelectedModelChoice.Id == "magnum_12b")
            {
                StatusMessage = "Downloading Magnum-12B-v2 (~7.5 GB)...";
                var progress = new Progress<double>(p =>
                {
                    DownloadProgress = p;
                    StatusMessage = $"Downloading Magnum-12B-v2: {p:F1}%";
                });
                string? path = await _backendService.DownloadModelAsync(
                    SelectedModelChoice.DownloadUrl,
                    SelectedModelChoice.FileName,
                    ModelsDirectory,
                    progress,
                    _downloadCts.Token);
                primaryModelPath = path ?? targetCheck;
            }
            else if (SelectedModelChoice.Id == "stheno_8b")
            {
                StatusMessage = "Downloading Llama-3.1-8B-Stheno-v3.2 (~4.9 GB)...";
                var progress = new Progress<double>(p =>
                {
                    DownloadProgress = p;
                    StatusMessage = $"Downloading Stheno-v3.2: {p:F1}%";
                });
                string? path = await _backendService.DownloadModelAsync(
                    SelectedModelChoice.DownloadUrl,
                    SelectedModelChoice.FileName,
                    ModelsDirectory,
                    progress,
                    _downloadCts.Token);
                primaryModelPath = path ?? targetCheck;
            }
            else if (SelectedModelChoice.Id == "both_models")
            {
                string sthenoTarget = Path.Combine(ModelsDirectory, "L3-8B-Stheno-v3.2-Q4_K_M.gguf");
                if (!File.Exists(sthenoTarget))
                {
                    StatusMessage = "[1/2] Downloading Llama-3.1-8B-Stheno-v3.2 (~4.9 GB)...";
                    var prog1 = new Progress<double>(p =>
                    {
                        DownloadProgress = p / 2.0;
                        StatusMessage = $"[1/2] Downloading Stheno-v3.2: {p:F1}%";
                    });
                    await _backendService.DownloadModelAsync(
                        "https://huggingface.co/bartowski/L3-8B-Stheno-v3.2-GGUF/resolve/main/L3-8B-Stheno-v3.2-Q4_K_M.gguf",
                        "L3-8B-Stheno-v3.2-Q4_K_M.gguf",
                        ModelsDirectory,
                        prog1,
                        _downloadCts.Token);
                }

                string magnumTarget = Path.Combine(ModelsDirectory, "magnum-12b-v2-Q4_K_M.gguf");
                if (!File.Exists(magnumTarget))
                {
                    StatusMessage = "[2/2] Downloading Magnum-12B-v2 (~7.5 GB)...";
                    var prog2 = new Progress<double>(p =>
                    {
                        DownloadProgress = 50.0 + (p / 2.0);
                        StatusMessage = $"[2/2] Downloading Magnum-12B-v2: {p:F1}%";
                    });
                    string? path2 = await _backendService.DownloadModelAsync(
                        "https://huggingface.co/bartowski/magnum-12b-v2-GGUF/resolve/main/magnum-12b-v2-Q4_K_M.gguf",
                        "magnum-12b-v2-Q4_K_M.gguf",
                        ModelsDirectory,
                        prog2,
                        _downloadCts.Token);
                    primaryModelPath = path2 ?? magnumTarget;
                }
                else
                {
                    primaryModelPath = magnumTarget;
                }
            }
            else
            {
                var existing = _backendService.GetAvailableModels(ModelsDirectory);
                if (existing.Count > 0)
                {
                    primaryModelPath = existing[0];
                }
            }

            StatusMessage = "Starting AMD Vulkan GPU engine...";

            if (string.IsNullOrEmpty(primaryModelPath))
            {
                var available = _backendService.GetAvailableModels(ModelsDirectory);
                if (available.Count > 0)
                {
                    primaryModelPath = available[0];
                }
            }

            if (!string.IsNullOrEmpty(primaryModelPath) && File.Exists(primaryModelPath))
            {
                LoadedModelName = Path.GetFileNameWithoutExtension(primaryModelPath);
                settings.SelectedModel = primaryModelPath;
                settings.IsFirstRunCompleted = true;
                await _databaseService.SaveSettingsAsync(settings);

                // Start backend if not already active
                if (!_backendService.IsRunning && !await _llmService.CheckHealthAsync())
                {
                    _backendService.StartKoboldCpp(primaryModelPath, port: 5001, contextSize: settings.ContextSize, gpuLayers: settings.GpuLayers);
                }

                for (int i = 0; i < 15; i++)
                {
                    StatusMessage = $"Connecting to AI Engine... ({i + 1}s)";
                    if (await _llmService.CheckHealthAsync())
                    {
                        break;
                    }
                    await Task.Delay(1000);
                }
            }
            else
            {
                settings.IsFirstRunCompleted = true;
                await _databaseService.SaveSettingsAsync(settings);
            }

            IsDownloading = false;
            DownloadProgress = 100;
            StatusMessage = "Setup Complete! Model is loaded and connected.";
            CurrentStep = 3;
        }

        [RelayCommand]
        public async Task CompleteAndEnterStudioAsync()
        {
            var settings = await _databaseService.GetSettingsAsync();
            settings.IsFirstRunCompleted = true;
            await _databaseService.SaveSettingsAsync(settings);

            SetupCompleted?.Invoke();
        }

        [RelayCommand]
        public void CancelSetup()
        {
            _downloadCts?.Cancel();
            IsDownloading = false;
            CurrentStep = 1;
            StatusMessage = "Setup cancelled.";
        }
    }
}
