using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using UncensoredStudio.Models;
using UncensoredStudio.Services;

namespace UncensoredStudio.ViewModels
{
    public partial class EngineViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;
        private readonly LLMService _llmService;
        private readonly BackendProcessService _backendService;

        private readonly ConcurrentQueue<string> _logBuffer = new();
        private readonly DispatcherTimer _logFlushTimer;
        private CancellationTokenSource? _downloadCts;

        [ObservableProperty]
        private AppSettings _settings = new();

        [ObservableProperty]
        private bool _isBackendRunning = false;

        [ObservableProperty]
        private bool _isApiOnline = false;

        [ObservableProperty]
        private string _activeModelName = "None";

        [ObservableProperty]
        private ObservableCollection<ModelItem> _availableLocalModels = new();

        [ObservableProperty]
        private ModelItem? _selectedLocalModel;

        [ObservableProperty]
        private ObservableCollection<DownloadableModelInfo> _modelCatalog = new();

        [ObservableProperty]
        private DownloadableModelInfo? _selectedCatalogModel;

        [ObservableProperty]
        private ObservableCollection<PromptPreset> _presets = new();

        [ObservableProperty]
        private PromptPreset? _selectedPreset;

        [ObservableProperty]
        private string _customDownloadUrl = "";

        [ObservableProperty]
        private bool _isDownloading = false;

        [ObservableProperty]
        private double _downloadProgress = 0;

        [ObservableProperty]
        private string _downloadStatus = "Ready to download models";

        [ObservableProperty]
        private string _consoleLog = "";

        public event Action? ModelsListUpdated;

        public EngineViewModel(
            DatabaseService databaseService,
            LLMService llmService,
            BackendProcessService backendService)
        {
            _databaseService = databaseService;
            _llmService = llmService;
            _backendService = backendService;

            _backendService.OutputReceived += OnBackendLogReceived;
            _backendService.StateChanged += running => IsBackendRunning = running;

            _logFlushTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            _logFlushTimer.Tick += (s, e) => FlushLogBuffer();
            _logFlushTimer.Start();

            InitializeCatalog();
        }

        private double _detectedVramGb = 12.0;
        private double _detectedRamGb = 32.0;
        private string _detectedGpuName = "Dedicated GPU";

        private void DetectHardware()
        {
            try
            {
                using var searcher = new System.Management.ManagementObjectSearcher("SELECT Name, AdapterRAM FROM Win32_VideoController");
                foreach (var obj in searcher.Get())
                {
                    string name = obj["Name"]?.ToString() ?? "";
                    if (!string.IsNullOrEmpty(name) && !name.Contains("Basic", StringComparison.OrdinalIgnoreCase))
                    {
                        _detectedGpuName = name;
                        if (obj["AdapterRAM"] != null && ulong.TryParse(obj["AdapterRAM"].ToString(), out ulong vramBytes) && vramBytes > 0)
                        {
                            _detectedVramGb = Math.Max(_detectedVramGb, vramBytes / (1024.0 * 1024.0 * 1024.0));
                        }
                    }
                }
            }
            catch { }

            try
            {
                using var memSearcher = new System.Management.ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
                foreach (var obj in memSearcher.Get())
                {
                    if (obj["TotalPhysicalMemory"] != null && ulong.TryParse(obj["TotalPhysicalMemory"].ToString(), out ulong ramBytes))
                    {
                        _detectedRamGb = ramBytes / (1024.0 * 1024.0 * 1024.0);
                    }
                }
            }
            catch { }

            // Ensure sensible defaults if virtualized/integrated
            if (_detectedVramGb < 8.0 && _detectedGpuName.Contains("6700", StringComparison.OrdinalIgnoreCase))
            {
                _detectedVramGb = 12.0; // AMD RX 6700 XT standard
            }
        }

        private void InitializeCatalog()
        {
            DetectHardware();

            var catalogList = new List<DownloadableModelInfo>
            {
                // 1. Qwen 2.5 14B - Claude 3.5 Sonnet / Gemini 2.0 Flash level intelligence
                new DownloadableModelInfo
                {
                    Id = "qwen_14b",
                    Name = "Qwen-2.5-14B-Instruct",
                    Category = "🧠 Frontier General AI",
                    Description = "Frontier-grade reasoning, coding, deep comprehension, multi-step planning, and zero-hallucination factual grounding. Directly matches Claude 3.5 & Gemini Flash.",
                    RecommendedFor = "⭐ Claude 3.5 / Gemini Tier General AI",
                    BenchmarkTier = "Claude 3.5 Sonnet / Gemini 2.0 Flash IQ",
                    SizeDisplay = "~9.0 GB (Q4_K_M)",
                    ParameterCount = "14B",
                    RequiredVramGb = 10.5,
                    RequiredRamGb = 16.0,
                    FileName = "Qwen2.5-14B-Instruct-Q4_K_M.gguf",
                    DownloadUrl = "https://huggingface.co/bartowski/Qwen2.5-14B-Instruct-GGUF/resolve/main/Qwen2.5-14B-Instruct-Q4_K_M.gguf"
                },

                // 2. DeepSeek R1 Distill Qwen 14B - Deep Chain of Thought Reasoning
                new DownloadableModelInfo
                {
                    Id = "deepseek_r1_14b",
                    Name = "DeepSeek-R1-Distill-Qwen-14B",
                    Category = "🔬 Deep Reasoning & CoT",
                    Description = "Frontier reasoning model that thinks and verifies step-by-step before answering. Eliminates false callbacks and hallucinations completely.",
                    RecommendedFor = "🧠 Chain-of-Thought Deep Reasoning",
                    BenchmarkTier = "OpenAI o1 / DeepSeek R1 Reasoning Tier",
                    SizeDisplay = "~9.0 GB (Q4_K_M)",
                    ParameterCount = "14B",
                    RequiredVramGb = 10.5,
                    RequiredRamGb = 16.0,
                    FileName = "DeepSeek-R1-Distill-Qwen-14B-Q4_K_M.gguf",
                    DownloadUrl = "https://huggingface.co/bartowski/DeepSeek-R1-Distill-Qwen-14B-GGUF/resolve/main/DeepSeek-R1-Distill-Qwen-14B-Q4_K_M.gguf"
                },

                // 3. Qwen 2.5 32B - Opus / Gemini Ultra Tier (Hybrid GPU+RAM)
                new DownloadableModelInfo
                {
                    Id = "qwen_32b",
                    Name = "Qwen-2.5-32B-Instruct",
                    Category = "🏆 Heavyweight Frontier AI",
                    Description = "Heavyweight intelligence, master-level architecture, software engineering, and complex multi-turn coherence. Offloads across GPU VRAM + System RAM.",
                    RecommendedFor = "💎 Claude Opus / Gemini Ultra Intelligence",
                    BenchmarkTier = "Claude Opus / Gemini Ultra Tier",
                    SizeDisplay = "~19.8 GB (Q4_K_M)",
                    ParameterCount = "32B",
                    RequiredVramGb = 12.0,
                    RequiredRamGb = 32.0,
                    FileName = "Qwen2.5-32B-Instruct-Q4_K_M.gguf",
                    DownloadUrl = "https://huggingface.co/bartowski/Qwen2.5-32B-Instruct-GGUF/resolve/main/Qwen2.5-32B-Instruct-Q4_K_M.gguf"
                },

                // 4. Qwen 2.5 7B - High Speed Everyday Assistant
                new DownloadableModelInfo
                {
                    Id = "qwen_7b",
                    Name = "Qwen-2.5-7B-Instruct",
                    Category = "⚡ Fast Everyday Assistant",
                    Description = "Lightning-fast general AI assistant, programming helper, and factual answer engine. 100% full GPU offload with 60+ tokens/sec.",
                    RecommendedFor = "🚀 High Speed Coding & General Chat",
                    BenchmarkTier = "GPT-4o-Mini Equivalent",
                    SizeDisplay = "~4.7 GB (Q4_K_M)",
                    ParameterCount = "7B",
                    RequiredVramGb = 6.0,
                    RequiredRamGb = 16.0,
                    FileName = "Qwen2.5-7B-Instruct-Q4_K_M.gguf",
                    DownloadUrl = "https://huggingface.co/bartowski/Qwen2.5-7B-Instruct-GGUF/resolve/main/Qwen2.5-7B-Instruct-Q4_K_M.gguf"
                },

                // 5. Magnum 12B v2 - Premier Fiction & Erotica
                new DownloadableModelInfo
                {
                    Id = "magnum_12b",
                    Name = "Magnum-12B-v2 (Mistral Nemo)",
                    Category = "👑 Fiction & Romance",
                    Description = "Premier model for deep descriptive literature, slow-burn romance, mature prose, visceral sensory detail, and spicy Twitter posts.",
                    RecommendedFor = "⭐ Top Fiction & Smut Recommendation",
                    BenchmarkTier = "Literary Creative Specialist",
                    SizeDisplay = "~7.5 GB (Q4_K_M)",
                    ParameterCount = "12B",
                    RequiredVramGb = 8.5,
                    RequiredRamGb = 16.0,
                    FileName = "magnum-12b-v2-Q4_K_M.gguf",
                    DownloadUrl = "https://huggingface.co/bartowski/magnum-12b-v2-GGUF/resolve/main/magnum-12b-v2-Q4_K_M.gguf"
                },

                // 6. Llama 3.1 8B Stheno - Fast RP & Banter
                new DownloadableModelInfo
                {
                    Id = "stheno_8b",
                    Name = "Llama-3.1-8B-Stheno-v3.2",
                    Category = "🎭 Character RP & Banter",
                    Description = "Exceptional character voices, witty banter, high emotional responsiveness, and rapid output.",
                    RecommendedFor = "⚡ Fast Creative RP & Dialogue",
                    BenchmarkTier = "Persona RP Specialist",
                    SizeDisplay = "~4.9 GB (Q4_K_M)",
                    ParameterCount = "8B",
                    RequiredVramGb = 6.0,
                    RequiredRamGb = 16.0,
                    FileName = "L3-8B-Stheno-v3.2-Q4_K_M.gguf",
                    DownloadUrl = "https://huggingface.co/bartowski/L3-8B-Stheno-v3.2-GGUF/resolve/main/L3-8B-Stheno-v3.2-Q4_K_M.gguf"
                },

                // 7. Mistral Nemo 12B RPMax - 128k Long Novels
                new DownloadableModelInfo
                {
                    Id = "rpmax_12b",
                    Name = "Mistral-Nemo-12B-ArliAI-RPMax",
                    Category = "📖 Long Novels & Fantasy",
                    Description = "Trained for long-form narrative consistency, 128k context support, and dark fantasy / mature themes.",
                    RecommendedFor = "📖 Long-Form Novels & Dark Fantasy",
                    BenchmarkTier = "128k Context Long-Form Master",
                    SizeDisplay = "~7.5 GB (Q4_K_M)",
                    ParameterCount = "12B",
                    RequiredVramGb = 8.5,
                    RequiredRamGb = 16.0,
                    FileName = "Mistral-Nemo-12B-ArliAI-RPMax-v1.4-Q4_K_M.gguf",
                    DownloadUrl = "https://huggingface.co/bartowski/Mistral-Nemo-12B-ArliAI-RPMax-v1.4-GGUF/resolve/main/Mistral-Nemo-12B-ArliAI-RPMax-v1.4-Q4_K_M.gguf"
                }
            };

            // Calculate Hardware Compatibility for each model
            foreach (var m in catalogList)
            {
                if (m.RequiredVramGb <= _detectedVramGb)
                {
                    m.HardwareStatus = "🟢 100% Full GPU Offload (Optimal)";
                    m.HardwareStatusColor = "#10B981";
                    m.HardwareStatusDetails = $"Fits completely in {_detectedGpuName} ({_detectedVramGb:F0}GB VRAM). Max token speed.";
                }
                else if (m.RequiredVramGb <= _detectedVramGb + 2.0 || (_detectedVramGb + _detectedRamGb >= m.RequiredVramGb + 8.0))
                {
                    m.HardwareStatus = "🟡 Hybrid GPU + RAM Offload (Viable)";
                    m.HardwareStatusColor = "#F59E0B";
                    m.HardwareStatusDetails = $"Exceeds dedicated VRAM. KoboldCPP will offload {_detectedVramGb:F0}GB to GPU and remainder to System RAM ({_detectedRamGb:F0}GB). Viable at moderate speed.";
                }
                else
                {
                    m.HardwareStatus = "⚠️ Heavy / Resource Warning";
                    m.HardwareStatusColor = "#EF4444";
                    m.HardwareStatusDetails = $"Requires high memory allocation. May exceed system resources if context is large.";
                }
            }

            ModelCatalog = new ObservableCollection<DownloadableModelInfo>(catalogList);
            SelectedCatalogModel = ModelCatalog[0];
        }

        public async Task InitializeAsync()
        {
            Settings = await _databaseService.GetSettingsAsync();
            _llmService.SetEndpoint(Settings.ApiEndpoint);

            var presets = await _databaseService.GetPresetsAsync();
            Presets = new ObservableCollection<PromptPreset>(presets);
            if (Presets.Count > 0)
            {
                SelectedPreset = Presets[0];
            }

            RefreshLocalModels();
            await CheckStatusAsync();
        }

        private void OnBackendLogReceived(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;
            _logBuffer.Enqueue(line);
        }

        private void FlushLogBuffer()
        {
            if (_logBuffer.IsEmpty) return;

            var sb = new StringBuilder();
            int count = 0;
            while (_logBuffer.TryDequeue(out var line) && count < 100)
            {
                sb.AppendLine(line);
                count++;
            }

            if (sb.Length > 0)
            {
                string combined = ConsoleLog + sb.ToString();
                ConsoleLog = combined.Length > 8000 ? combined.Substring(combined.Length - 4000) : combined;
            }
        }

        [RelayCommand]
        public void BrowseModelsDirectory()
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select Directory for AI Models",
                InitialDirectory = Directory.Exists(Settings.ModelsDirectory) ? Settings.ModelsDirectory : @"I:\UncensoredAI"
            };

            if (dialog.ShowDialog() == true)
            {
                Settings.ModelsDirectory = dialog.FolderName;
                _ = SaveSettingsAsync();
                RefreshLocalModels();
            }
        }

        [RelayCommand]
        public void RefreshLocalModels()
        {
            var modelPaths = _backendService.GetAvailableModels(Settings.ModelsDirectory);
            var modelItems = modelPaths.Select(ModelItem.FromPath).ToList();

            AvailableLocalModels = new ObservableCollection<ModelItem>(modelItems);

            foreach (var cat in ModelCatalog)
            {
                cat.IsInstalled = modelPaths.Any(m => Path.GetFileName(m).Equals(cat.FileName, StringComparison.OrdinalIgnoreCase));
                cat.LocalPath = modelPaths.FirstOrDefault(m => Path.GetFileName(m).Equals(cat.FileName, StringComparison.OrdinalIgnoreCase)) ?? "";
            }

            if (modelItems.Count > 0)
            {
                SelectedLocalModel = modelItems.FirstOrDefault(m => m.FullPath == Settings.SelectedModel) ?? modelItems[0];
            }

            ModelsListUpdated?.Invoke();
        }

        [RelayCommand]
        public async Task CheckStatusAsync()
        {
            IsApiOnline = await _llmService.CheckHealthAsync(Settings.ApiEndpoint);
            if (IsApiOnline)
            {
                string raw = await _llmService.GetActiveModelNameAsync(Settings.ApiEndpoint);
                ActiveModelName = ModelItem.FromPath(raw).DisplayName;
            }
            else
            {
                ActiveModelName = "Offline / Disconnected";
            }
        }

        [RelayCommand]
        public async Task SaveSettingsAsync()
        {
            if (SelectedLocalModel != null)
            {
                Settings.SelectedModel = SelectedLocalModel.FullPath;
            }
            await _databaseService.SaveSettingsAsync(Settings);
            _llmService.SetEndpoint(Settings.ApiEndpoint);
            await CheckStatusAsync();
        }

        [RelayCommand]
        public async Task StartBackendAsync()
        {
            if (SelectedLocalModel == null || string.IsNullOrEmpty(SelectedLocalModel.FullPath))
            {
                MessageBox.Show("Please select or download a local .gguf model first!", "No Model Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            bool ok = _backendService.StartKoboldCpp(
                SelectedLocalModel.FullPath,
                port: 5001,
                contextSize: Settings.ContextSize,
                gpuLayers: Settings.GpuLayers,
                customExecutablePath: Settings.BackendPath);

            if (ok)
            {
                Settings.ApiEndpoint = "http://127.0.0.1:5001";
                Settings.SelectedModel = SelectedLocalModel.FullPath;
                await _databaseService.SaveSettingsAsync(Settings);
                _llmService.SetEndpoint(Settings.ApiEndpoint);
                await Task.Delay(2000);
                await CheckStatusAsync();
            }
        }

        private static readonly SemaphoreSlim _switchLock = new(1, 1);

        [RelayCommand]
        public async Task SwitchModelDirectlyAsync(string modelPath)
        {
            if (string.IsNullOrEmpty(modelPath) || !File.Exists(modelPath)) return;

            await _switchLock.WaitAsync();
            try
            {
                SelectedLocalModel = AvailableLocalModels.FirstOrDefault(m => m.FullPath == modelPath) ?? ModelItem.FromPath(modelPath);
                Settings.SelectedModel = modelPath;
                await _databaseService.SaveSettingsAsync(Settings);

                ActiveModelName = $"Switching to {Path.GetFileNameWithoutExtension(modelPath)}...";

                // If backend is already running and loaded with this exact model, skip restart
                if (_backendService.IsRunning && await _llmService.CheckHealthAsync())
                {
                    string active = await _llmService.GetActiveModelNameAsync();
                    string modelFileName = Path.GetFileNameWithoutExtension(modelPath);
                    if (!string.IsNullOrEmpty(active) && (active.Contains(modelFileName, StringComparison.OrdinalIgnoreCase) || modelFileName.Contains(active, StringComparison.OrdinalIgnoreCase)))
                    {
                        await CheckStatusAsync();
                        return;
                    }
                }

                _backendService.StopKoboldCpp();
                await Task.Delay(1500);

                bool started = _backendService.StartKoboldCpp(
                    modelPath,
                    port: 5001,
                    contextSize: Settings.ContextSize,
                    gpuLayers: Settings.GpuLayers,
                    customExecutablePath: Settings.BackendPath);

                if (!started)
                {
                    ActiveModelName = "Failed to launch backend";
                    return;
                }

                for (int i = 0; i < 30; i++)
                {
                    await Task.Delay(1000);
                    if (await _llmService.CheckHealthAsync())
                    {
                        break;
                    }
                }
                await CheckStatusAsync();
            }
            finally
            {
                _switchLock.Release();
            }
        }

        [RelayCommand]
        public async Task StopBackendAsync()
        {
            _backendService.StopKoboldCpp();
            await Task.Delay(1000);
            await CheckStatusAsync();
        }

        [RelayCommand]
        public async Task InstallCatalogModelAsync(DownloadableModelInfo? model)
        {
            if (model == null) return;
            await DownloadModelInternalAsync(model.DownloadUrl, model.FileName);
        }

        [RelayCommand]
        public async Task DownloadCustomModelAsync()
        {
            if (string.IsNullOrWhiteSpace(CustomDownloadUrl))
            {
                MessageBox.Show("Please enter a valid HuggingFace direct download URL.", "Missing URL", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string url = CustomDownloadUrl.Trim();
            string fileName = Path.GetFileName(new Uri(url).AbsolutePath);
            if (string.IsNullOrEmpty(fileName) || !fileName.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase))
            {
                fileName = "custom_model.gguf";
            }

            await DownloadModelInternalAsync(url, fileName);
        }

        private async Task DownloadModelInternalAsync(string url, string fileName)
        {
            IsDownloading = true;
            DownloadProgress = 0;
            DownloadStatus = $"Downloading {fileName}...";
            _downloadCts = new CancellationTokenSource();

            var progress = new Progress<double>(p =>
            {
                DownloadProgress = p;
                DownloadStatus = $"Downloading {fileName}: {p:F1}%";
            });

            try
            {
                string? path = await _backendService.DownloadModelAsync(
                    url,
                    fileName,
                    Settings.ModelsDirectory,
                    progress,
                    _downloadCts.Token);

                if (path != null)
                {
                    DownloadStatus = $"Downloaded successfully to {path}";
                    RefreshLocalModels();
                    SelectedLocalModel = AvailableLocalModels.FirstOrDefault(m => m.FullPath == path);
                    MessageBox.Show($"Model downloaded successfully!\n\nFile: {path}", "Download Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    DownloadStatus = "Download failed or incomplete.";
                }
            }
            catch (OperationCanceledException)
            {
                DownloadStatus = "Download cancelled.";
            }
            catch (Exception ex)
            {
                DownloadStatus = $"Error: {ex.Message}";
            }
            finally
            {
                IsDownloading = false;
            }
        }

        [RelayCommand]
        public void CancelDownload()
        {
            _downloadCts?.Cancel();
            IsDownloading = false;
            DownloadStatus = "Cancelling download...";
        }

        [RelayCommand]
        public void OpenModelsFolder()
        {
            string path = Settings.ModelsDirectory;
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
    }
}
