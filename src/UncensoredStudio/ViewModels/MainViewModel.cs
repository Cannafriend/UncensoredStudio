using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UncensoredStudio.Models;
using UncensoredStudio.Services;

namespace UncensoredStudio.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;
        private readonly LLMService _llmService;
        private readonly ContextBuilderService _contextBuilder;
        private readonly BackendProcessService _backendService;
        private readonly TwitterService _twitterService;
        private readonly ExportService _exportService;

        private DispatcherTimer? _statusTimer;

        [ObservableProperty]
        private string _activeTab = "Novel"; // Novel, Chat, Twitter, Lorebook, Engine, Settings

        [ObservableProperty]
        private object? _currentViewModel;

        [ObservableProperty]
        private string _appFontFamily = "Segoe UI, sans-serif";

        [ObservableProperty]
        private bool _isFirstRun = false;

        [ObservableProperty]
        private bool _showStartupModelPicker = false;

        [ObservableProperty]
        private bool _alwaysPromptOnStartup = true;

        [ObservableProperty]
        private SetupWizardViewModel _setupWizardVm;

        [ObservableProperty]
        private NovelViewModel _novelVm;

        [ObservableProperty]
        private ChatViewModel _chatVm;

        [ObservableProperty]
        private RoleplayViewModel _roleplayVm;

        [ObservableProperty]
        private TwitterViewModel _twitterVm;

        [ObservableProperty]
        private LorebookViewModel _lorebookVm;

        [ObservableProperty]
        private EngineViewModel _engineVm;

        [ObservableProperty]
        private ImageStudioViewModel _imageStudioVm;

        [ObservableProperty]
        private SettingsViewModel _settingsVm;

        [ObservableProperty]
        private bool _isBackendConnected = false;

        [ObservableProperty]
        private string _activeModelSummary = "Offline";

        [ObservableProperty]
        private ObservableCollection<ModelItem> _headerModelsList = new();

        [ObservableProperty]
        private ModelItem? _selectedHeaderModel;

        private bool _isSwitchingModel = false;
        private readonly ImageGenerationService _imageService;

        public MainViewModel()
        {
            _databaseService = new DatabaseService();
            _llmService = new LLMService();
            _contextBuilder = new ContextBuilderService(_databaseService);
            _backendService = new BackendProcessService();
            _twitterService = new TwitterService();
            _exportService = new ExportService();
            _imageService = new ImageGenerationService(_databaseService, _llmService);

            NovelVm = new NovelViewModel(_databaseService, _llmService, _contextBuilder, _exportService);
            ChatVm = new ChatViewModel(_databaseService, _llmService, _contextBuilder);
            RoleplayVm = new RoleplayViewModel(_databaseService, _llmService, _contextBuilder);
            TwitterVm = new TwitterViewModel(_databaseService, _llmService, _contextBuilder, _twitterService);
            LorebookVm = new LorebookViewModel(_databaseService, _contextBuilder);
            EngineVm = new EngineViewModel(_databaseService, _llmService, _backendService);
            ImageStudioVm = new ImageStudioViewModel(_databaseService, _imageService, _llmService);
            SetupWizardVm = new SetupWizardViewModel(_databaseService, _backendService, _llmService);
            SettingsVm = new SettingsViewModel(_databaseService, _backendService);

            // Default current view
            CurrentViewModel = NovelVm;

            SetupWizardVm.SetupCompleted += () =>
            {
                Application.Current.Dispatcher.BeginInvoke(async () =>
                {
                    IsFirstRun = false;
                    ShowStartupModelPicker = false;
                    await RefreshStateAfterSetupAsync();
                });
            };

            EngineVm.ModelsListUpdated += UpdateHeaderModels;

            SettingsVm.FontFamilyChanged += font =>
            {
                AppFontFamily = font;
            };

            SettingsVm.MemoryCleared += async () =>
            {
                await NovelVm.InitializeAsync();
                await ChatVm.InitializeAsync();
                await RoleplayVm.InitializeAsync();
                await TwitterVm.InitializeAsync();
                await LorebookVm.InitializeAsync();
                await SettingsVm.InitializeAsync();
            };
        }

        public async Task InitializeAsync()
        {
            var settings = await _databaseService.GetSettingsAsync();
            if (!string.IsNullOrEmpty(settings.SelectedFontFamily))
            {
                AppFontFamily = settings.SelectedFontFamily;
            }

            var availablePaths = _backendService.GetAvailableModels(settings.ModelsDirectory);
            var modelItems = availablePaths.Select(ModelItem.FromPath).ToList();

            HeaderModelsList = new ObservableCollection<ModelItem>(modelItems);

            if (modelItems.Count == 0)
            {
                // No models downloaded yet: show initial downloader wizard
                IsFirstRun = true;
                ShowStartupModelPicker = false;
                await SetupWizardVm.LoadSettingsAsync();
            }
            else
            {
                IsFirstRun = false;

                _isSwitchingModel = true;
                // Pick selected model
                SelectedHeaderModel = modelItems.FirstOrDefault(m => m.FullPath == settings.SelectedModel) ?? modelItems[0];
                _isSwitchingModel = false;

                // Auto-show model picker on startup
                ShowStartupModelPicker = true;
            }

            await NovelVm.InitializeAsync();
            await ChatVm.InitializeAsync();
            await RoleplayVm.InitializeAsync();
            await TwitterVm.InitializeAsync();
            await LorebookVm.InitializeAsync();
            await EngineVm.InitializeAsync();
            await ImageStudioVm.InitializeAsync();
            await SettingsVm.InitializeAsync();

            UpdateHeaderModels();

            _statusTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            _statusTimer.Tick += async (s, e) => await CheckBackendStatusAsync();
            _statusTimer.Start();

            await CheckBackendStatusAsync();
        }

        [RelayCommand]
        public void ConfirmModelAndEnterStudio()
        {
            // 1. Instantly dismiss modal so UI enters workspace immediately
            ShowStartupModelPicker = false;
            ActiveTab = "Novel";
            CurrentViewModel = NovelVm;

            if (SelectedHeaderModel != null)
            {
                ActiveModelSummary = $"Loading {SelectedHeaderModel.DisplayName}...";

                _ = Task.Run(async () =>
                {
                    var settings = await _databaseService.GetSettingsAsync();
                    settings.SelectedModel = SelectedHeaderModel.FullPath;
                    await _databaseService.SaveSettingsAsync(settings);

                    if (!_backendService.IsRunning || !await _llmService.CheckHealthAsync())
                    {
                        await EngineVm.SwitchModelDirectlyAsync(SelectedHeaderModel.FullPath);
                    }
                    await CheckBackendStatusAsync();
                });
            }
        }

        [RelayCommand]
        public void OpenModelHubFromPicker()
        {
            ShowStartupModelPicker = false;
            ActiveTab = "Engine";
            CurrentViewModel = EngineVm;
        }

        public async Task RefreshStateAfterSetupAsync()
        {
            await EngineVm.InitializeAsync();
            UpdateHeaderModels();
            await CheckBackendStatusAsync();
        }

        private void UpdateHeaderModels()
        {
            var models = EngineVm.AvailableLocalModels;
            if (models == null || models.Count == 0) return;

            _isSwitchingModel = true;
            try
            {
                if (HeaderModelsList.Count != models.Count)
                {
                    HeaderModelsList = new ObservableCollection<ModelItem>(models);
                }

                string currentTarget = EngineVm.SelectedLocalModel?.FullPath ?? "";
                var match = HeaderModelsList.FirstOrDefault(m => m.FullPath.Equals(currentTarget, StringComparison.OrdinalIgnoreCase)) 
                            ?? HeaderModelsList.FirstOrDefault();

                if (SelectedHeaderModel?.FullPath != match?.FullPath)
                {
                    SelectedHeaderModel = match;
                }
            }
            finally
            {
                _isSwitchingModel = false;
            }
        }

        partial void OnSelectedHeaderModelChanged(ModelItem? value)
        {
            if (_isSwitchingModel || value == null || string.IsNullOrEmpty(value.FullPath)) return;

            // Only switch if the user explicitly picked a DIFFERENT model than what is already running
            string activeTarget = EngineVm.SelectedLocalModel?.FullPath ?? "";
            if (activeTarget.Equals(value.FullPath, StringComparison.OrdinalIgnoreCase) && IsBackendConnected) return;

            _ = Task.Run(async () =>
            {
                await EngineVm.SwitchModelDirectlyAsync(value.FullPath);
            });
        }

        private async Task CheckBackendStatusAsync()
        {
            IsBackendConnected = await _llmService.CheckHealthAsync();
            if (IsBackendConnected)
            {
                string raw = await _llmService.GetActiveModelNameAsync();
                ActiveModelSummary = ModelItem.FromPath(raw).DisplayName;
            }
            else
            {
                if (ActiveModelSummary.StartsWith("Loading", StringComparison.OrdinalIgnoreCase))
                {
                    // Keep loading status while KoboldCPP allocates VRAM
                }
                else
                {
                    ActiveModelSummary = "Offline / Disconnected";
                }
            }
        }

        [RelayCommand]
        public void SwitchTab(string tabName)
        {
            ActiveTab = tabName;
            switch (tabName.ToLowerInvariant())
            {
                case "novel":
                    CurrentViewModel = NovelVm;
                    break;
                case "chat":
                    CurrentViewModel = ChatVm;
                    break;
                case "roleplay":
                case "rp":
                    CurrentViewModel = RoleplayVm;
                    break;
                case "twitter":
                    CurrentViewModel = TwitterVm;
                    break;
                case "lorebook":
                    CurrentViewModel = LorebookVm;
                    break;
                case "image":
                case "imagestudio":
                    CurrentViewModel = ImageStudioVm;
                    break;
                case "engine":
                    CurrentViewModel = EngineVm;
                    break;
                case "settings":
                    CurrentViewModel = SettingsVm;
                    break;
                default:
                    CurrentViewModel = NovelVm;
                    break;
            }
        }

        [RelayCommand]
        public void OpenSetupWizard()
        {
            IsFirstRun = true;
        }

        [RelayCommand]
        public void OpenStartupPicker()
        {
            ShowStartupModelPicker = true;
        }

        [RelayCommand]
        public void OpenSettings()
        {
            SwitchTab("Settings");
        }
    }
}
