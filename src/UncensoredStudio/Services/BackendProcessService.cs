using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace UncensoredStudio.Services
{
    public class BackendProcessService
    {
        private Process? _backendProcess;
        private readonly string _defaultBackendPath;
        private readonly string _tempDirectory;
        private readonly string _cacheDirectory;
        private readonly string _logFilePath;
        private readonly string _defaultModelsDir;

        public bool IsRunning => _backendProcess != null && !_backendProcess.HasExited;
        public int? ProcessId => IsRunning ? _backendProcess?.Id : null;

        public event Action<string>? OutputReceived;
        public event Action<bool>? StateChanged;

        public BackendProcessService()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string localBackend = Path.Combine(baseDir, "backend", "koboldcpp.exe");
            _defaultBackendPath = File.Exists(localBackend) ? localBackend : @"I:\UncensoredAI\backend\koboldcpp.exe";

            string localTemp = Path.Combine(baseDir, "temp");
            _tempDirectory = Directory.Exists(localTemp) ? localTemp : @"I:\UncensoredAI\temp";

            string localCache = Path.Combine(baseDir, "cache");
            _cacheDirectory = Directory.Exists(localCache) ? localCache : @"I:\UncensoredAI\cache";

            string localBackendDir = Path.Combine(baseDir, "backend");
            _logFilePath = Directory.Exists(localBackendDir) ? Path.Combine(localBackendDir, "koboldcpp.log") : @"I:\UncensoredAI\backend\koboldcpp.log";

            string localModels = Path.Combine(baseDir, "models");
            _defaultModelsDir = Directory.Exists(localModels) ? localModels : @"I:\UncensoredAI\models";

            EnsureDriveDirectories();
        }

        private void EnsureDriveDirectories()
        {
            try
            {
                if (!Directory.Exists(_tempDirectory)) Directory.CreateDirectory(_tempDirectory);
                if (!Directory.Exists(_cacheDirectory)) Directory.CreateDirectory(_cacheDirectory);
                if (!Directory.Exists(@"I:\UncensoredAI\models")) Directory.CreateDirectory(@"I:\UncensoredAI\models");
                if (!Directory.Exists(@"I:\UncensoredAI\backend")) Directory.CreateDirectory(@"I:\UncensoredAI\backend");
            }
            catch
            {
            }
        }

        public void KillAllOrphanedKoboldProcesses()
        {
            try
            {
                var procs = Process.GetProcessesByName("koboldcpp");
                foreach (var p in procs)
                {
                    try
                    {
                        p.Kill(true);
                        p.WaitForExit(1500);
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

        public List<string> GetAvailableModels(string? customModelsDir = null)
        {
            var results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(customModelsDir) && Directory.Exists(customModelsDir))
            {
                foreach (var f in Directory.GetFiles(customModelsDir, "*.gguf", SearchOption.AllDirectories))
                    results.Add(f);
            }
            if (Directory.Exists(_defaultModelsDir))
            {
                foreach (var f in Directory.GetFiles(_defaultModelsDir, "*.gguf", SearchOption.AllDirectories))
                    results.Add(f);
            }
            if (Directory.Exists(@"I:\UAI\UncensoredStudio\models"))
            {
                foreach (var f in Directory.GetFiles(@"I:\UAI\UncensoredStudio\models", "*.gguf", SearchOption.AllDirectories))
                    results.Add(f);
            }
            if (Directory.Exists(@"I:\UncensoredAI\models"))
            {
                foreach (var f in Directory.GetFiles(@"I:\UncensoredAI\models", "*.gguf", SearchOption.AllDirectories))
                    results.Add(f);
            }
            return results.ToList();
        }

        public async Task<bool> EnsureDependenciesAsync(IProgress<double>? progress = null)
        {
            EnsureDriveDirectories();
            string dir = Path.GetDirectoryName(_defaultBackendPath) ?? @"I:\UncensoredAI\backend";
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            if (File.Exists(_defaultBackendPath))
            {
                return true;
            }

            string url = "https://github.com/LostRuins/koboldcpp/releases/download/v1.120/koboldcpp-nocuda.exe";
            OutputReceived?.Invoke("[Downloading KoboldCPP Vulkan Backend engine to I:\\UncensoredAI...]");

            try
            {
                using var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

                using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                long total = response.Content.Headers.ContentLength ?? -1L;
                using var inStream = await response.Content.ReadAsStreamAsync();
                using var outStream = new FileStream(_defaultBackendPath, FileMode.Create, FileAccess.Write, FileShare.None);

                var buffer = new byte[81920];
                long totalRead = 0;
                int read;

                while ((read = await inStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await outStream.WriteAsync(buffer, 0, read);
                    totalRead += read;
                    if (total > 0 && progress != null)
                    {
                        progress.Report((double)totalRead / total * 100.0);
                    }
                }
                outStream.Close();
                OutputReceived?.Invoke("[KoboldCPP Vulkan backend engine installed successfully on I: drive!]");
                return true;
            }
            catch (Exception ex)
            {
                OutputReceived?.Invoke($"[Failed to download KoboldCPP engine: {ex.Message}]");
                return false;
            }
        }

        public bool StartKoboldCpp(
            string modelPath,
            int port = 5001,
            int contextSize = 8192,
            int gpuLayers = 99,
            string? customExecutablePath = null)
        {
            EnsureDriveDirectories();
            StopKoboldCpp();

            string exePath = !string.IsNullOrEmpty(customExecutablePath) && File.Exists(customExecutablePath)
                ? customExecutablePath
                : _defaultBackendPath;

            if (!File.Exists(exePath))
            {
                OutputReceived?.Invoke($"[Error: KoboldCPP executable not found at {exePath}]");
                return false;
            }

            if (!File.Exists(modelPath))
            {
                OutputReceived?.Invoke($"[Error: Model file not found at {modelPath}]");
                return false;
            }

            if (contextSize <= 0) contextSize = 8192;
            if (gpuLayers <= 0) gpuLayers = 99;

            try
            {
                string args = $"--usevulkan --gpulayers {gpuLayers} --contextsize {contextSize} --port {port} --model \"{modelPath}\" --skiplauncher --quiet";

                var startInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(exePath)
                };

                // Explicitly preserve and supply critical environment variables so Vulkan & Windows runtime load properly
                foreach (System.Collections.DictionaryEntry env in Environment.GetEnvironmentVariables())
                {
                    string key = env.Key.ToString() ?? "";
                    if (!string.IsNullOrEmpty(key) && !startInfo.Environment.ContainsKey(key))
                    {
                        startInfo.Environment[key] = env.Value?.ToString() ?? "";
                    }
                }

                // Strictly direct temporary extractions and caches to the I: drive
                startInfo.Environment["TMP"] = _tempDirectory;
                startInfo.Environment["TEMP"] = _tempDirectory;
                startInfo.Environment["HF_HOME"] = Path.Combine(_cacheDirectory, "huggingface");
                startInfo.Environment["HF_HUB_CACHE"] = Path.Combine(_cacheDirectory, "huggingface", "hub");
                startInfo.Environment["XDG_CACHE_HOME"] = _cacheDirectory;

                _backendProcess = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

                _backendProcess.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        OutputReceived?.Invoke(e.Data);
                        AppendLog(e.Data);
                    }
                };

                _backendProcess.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        OutputReceived?.Invoke(e.Data);
                        AppendLog($"[ERR] {e.Data}");
                    }
                };

                _backendProcess.Exited += (s, e) =>
                {
                    StateChanged?.Invoke(false);
                    OutputReceived?.Invoke("[KoboldCPP Backend process has stopped]");
                    AppendLog("[KoboldCPP Backend process has stopped]");
                };

                bool started = _backendProcess.Start();
                if (started)
                {
                    _backendProcess.BeginOutputReadLine();
                    _backendProcess.BeginErrorReadLine();
                    StateChanged?.Invoke(true);
                    string msg = $"[KoboldCPP Started on port {port} with Vulkan GPU offload for {Path.GetFileName(modelPath)}]";
                    OutputReceived?.Invoke(msg);
                    AppendLog(msg);
                    return true;
                }
            }
            catch (Exception ex)
            {
                string err = $"[Failed to start backend: {ex.Message}]";
                OutputReceived?.Invoke(err);
                AppendLog(err);
            }
            return false;
        }

        private void AppendLog(string message)
        {
            try
            {
                File.AppendAllText(_logFilePath, $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
            }
            catch
            {
            }
        }

        public void StopKoboldCpp()
        {
            if (_backendProcess != null && !_backendProcess.HasExited)
            {
                try
                {
                    _backendProcess.Kill(true);
                    _backendProcess.WaitForExit(1500);
                }
                catch
                {
                }
                finally
                {
                    try { _backendProcess.Dispose(); } catch { }
                    _backendProcess = null;
                    StateChanged?.Invoke(false);
                }
            }

            KillAllOrphanedKoboldProcesses();
        }

        public async Task<string?> DownloadModelAsync(
            string downloadUrl,
            string targetFileName,
            string? targetDirectory = null,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            string modelsDir = !string.IsNullOrEmpty(targetDirectory) ? targetDirectory : @"I:\UncensoredAI\models";
            if (!Directory.Exists(modelsDir))
            {
                Directory.CreateDirectory(modelsDir);
            }

            string targetPath = Path.Combine(modelsDir, targetFileName);
            string tempPath = targetPath + ".download";

            try
            {
                using var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

                using var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();

                long totalBytes = response.Content.Headers.ContentLength ?? -1L;

                using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

                var buffer = new byte[81920];
                long totalRead = 0;
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                    totalRead += bytesRead;

                    if (totalBytes > 0 && progress != null)
                    {
                        double percentage = (double)totalRead / totalBytes * 100.0;
                        progress.Report(percentage);
                    }
                }

                fileStream.Close();

                if (File.Exists(targetPath))
                {
                    File.Delete(targetPath);
                }
                File.Move(tempPath, targetPath);

                return targetPath;
            }
            catch (Exception ex)
            {
                if (File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); } catch { }
                }
                OutputReceived?.Invoke($"[Download error: {ex.Message}]");
                return null;
            }
        }
    }
}
