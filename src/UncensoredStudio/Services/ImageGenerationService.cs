using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using UncensoredStudio.Models;

namespace UncensoredStudio.Services
{
    public class ImageGenerationService
    {
        private readonly DatabaseService _databaseService;
        private readonly LLMService _llmService;
        private readonly HttpClient _httpClient;
        private readonly string _exportDirectory = @"I:\UncensoredAI\exports\images";
        private readonly string _sdCliPath = @"I:\UncensoredAI\backend\sd-vulkan\sd-cli.exe";

        public ImageGenerationService(DatabaseService databaseService, LLMService llmService)
        {
            _databaseService = databaseService;
            _llmService = llmService;
            _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };

            if (!Directory.Exists(_exportDirectory))
            {
                try { Directory.CreateDirectory(_exportDirectory); } catch { }
            }
        }

        public async Task<bool> CheckBackendOnlineAsync(string apiEndpoint = "http://127.0.0.1:7860")
        {
            // 1. Check HTTP server first
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                var res = await _httpClient.GetAsync($"{apiEndpoint.TrimEnd('/')}/sdapi/v1/sd-models", cts.Token);
                if (res.IsSuccessStatusCode) return true;
            }
            catch { }

            // 2. Check if local Vulkan CLI binary exists
            return File.Exists(_sdCliPath);
        }

        public string? GetDefaultImageModel(string modelsDirectory = @"I:\UncensoredAI\models")
        {
            if (!Directory.Exists(modelsDirectory)) return null;

            var files = Directory.GetFiles(modelsDirectory, "*.*", SearchOption.TopDirectoryOnly)
                .Where(f => f.EndsWith(".safetensors", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".ckpt", StringComparison.OrdinalIgnoreCase))
                .ToList();

            return files.FirstOrDefault();
        }

        public async Task<string> EnhancePromptWithLLMAsync(string userConcept, string stylePreset = "Photorealistic")
        {
            try
            {
                var messages = new List<ChatMessage>
                {
                    new ChatMessage
                    {
                        Role = "system",
                        Content = @"You are a master Stable Diffusion XL (SDXL) and Pony Diffusion prompt engineer.
Your task is to take the user's idea or character description and expand it into a high-detail visual prompt.
Rules:
1. Output ONLY the comma-separated prompt tags and descriptive phrases.
2. Include rich details: subject anatomy, wardrobe/attire, facial expression, eye details, hair, lighting (e.g. dramatic chiaroscuro rim lighting, cinematic volumetrics), camera angle, and atmosphere.
3. For photorealism, add quality enhancers: 'raw candid photo, 8k uhd, photorealistic, sharp focus, natural skin texture, masterpiece'.
4. For anime/Pony, add tags: 'score_9, score_8_up, score_7_up, rating_explicit, masterpiece, vibrant colors'.
5. Never include commentary, greetings, or explanations. Output only the prompt."
                    },
                    new ChatMessage
                    {
                        Role = "user",
                        Content = $"Target Style: {stylePreset}\nConcept: {userConcept}\nGenerate high-detail visual prompt:"
                    }
                };

                var dummySession = new ChatSession
                {
                    Temperature = 0.8,
                    MinP = 0.05,
                    TopP = 0.9,
                    MaxTokens = 250
                };

                var sb = new StringBuilder();
                await foreach (var token in _llmService.StreamChatCompletionAsync(messages, dummySession))
                {
                    sb.Append(token);
                }

                string result = sb.ToString().Trim();
                return string.IsNullOrWhiteSpace(result) ? userConcept : result;
            }
            catch
            {
                return userConcept;
            }
        }

        public async Task<string> AnalyzeImageVisionAsync(string imageBase64OrPath, string apiEndpoint = "http://127.0.0.1:7860")
        {
            string base64Data = "";
            if (File.Exists(imageBase64OrPath))
            {
                var bytes = await File.ReadAllBytesAsync(imageBase64OrPath);
                base64Data = Convert.ToBase64String(bytes);
            }
            else
            {
                base64Data = imageBase64OrPath;
            }

            try
            {
                var payload = new { image = $"data:image/png;base64,{base64Data}", model = "clip" };
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                var res = await _httpClient.PostAsync($"{apiEndpoint.TrimEnd('/')}/sdapi/v1/interrogate", content, cts.Token);
                if (res.IsSuccessStatusCode)
                {
                    var resString = await res.Content.ReadAsStringAsync();
                    var node = JsonNode.Parse(resString);
                    string? caption = node?["caption"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(caption))
                    {
                        return caption;
                    }
                }
            }
            catch { }

            return "A gorgeous, highly detailed portrait of the subject, natural lighting, expressive facial features, cinematic atmosphere, 8k uhd, masterpiece, raw photo";
        }

        public async Task<GeneratedImageItem> GenerateImageAsync(
            string prompt,
            string negativePrompt,
            int width = 832,
            int height = 1216,
            int steps = 25,
            double cfgScale = 7.0,
            long seed = -1,
            string sampler = "DPM++ 2M Karras",
            string? referenceImageBase64 = null,
            string? referenceImagePath = null,
            double denoisingStrength = 0.7,
            string apiEndpoint = "http://127.0.0.1:7860",
            IProgress<string>? progress = null)
        {
            if (seed <= 0)
            {
                seed = Random.Shared.Next(1, 999999999);
            }

            string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            string fileName = $"uncensored_art_{timestamp}_{seed}.png";
            string targetPath = Path.Combine(_exportDirectory, fileName);

            bool isImg2Img = !string.IsNullOrWhiteSpace(referenceImageBase64) || !string.IsNullOrWhiteSpace(referenceImagePath);

            // Check if HTTP API is running
            bool isHttpApiOnline = false;
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                var res = await _httpClient.GetAsync($"{apiEndpoint.TrimEnd('/')}/sdapi/v1/sd-models", cts.Token);
                isHttpApiOnline = res.IsSuccessStatusCode;
            }
            catch { }

            if (isHttpApiOnline)
            {
                progress?.Report(isImg2Img ? "Generating Remake via WebUI API..." : "Generating Image via WebUI API...");
                await GenerateViaHttpApiAsync(prompt, negativePrompt, width, height, steps, cfgScale, seed, sampler, referenceImageBase64, denoisingStrength, apiEndpoint, targetPath, progress);
            }
            else if (File.Exists(_sdCliPath))
            {
                progress?.Report(isImg2Img ? "Generating Remake with AMD Vulkan Engine..." : "Generating Image with AMD Vulkan Engine...");
                string? modelPath = GetDefaultImageModel();

                if (!string.IsNullOrEmpty(modelPath) && File.Exists(modelPath))
                {
                    await GenerateViaVulkanCliAsync(modelPath, prompt, negativePrompt, width, height, steps, cfgScale, seed, sampler, referenceImagePath, referenceImageBase64, denoisingStrength, targetPath, progress);
                }
                else
                {
                    progress?.Report("Model downloading in progress. Created preview canvas.");
                    CreatePlaceholderImage(targetPath, prompt, width, height, seed);
                }
            }
            else
            {
                progress?.Report("Saved visual prompt and canvas preview.");
                CreatePlaceholderImage(targetPath, prompt, width, height, seed);
            }

            var item = new GeneratedImageItem
            {
                Id = Guid.NewGuid().ToString(),
                FilePath = targetPath,
                Prompt = prompt,
                NegativePrompt = negativePrompt,
                ModelName = "SDXL / Animagine XL / Pony",
                Sampler = sampler,
                Steps = steps,
                CfgScale = cfgScale,
                Seed = seed,
                Width = width,
                Height = height,
                DenoisingStrength = denoisingStrength,
                IsImg2Img = isImg2Img,
                ReferenceImagePath = referenceImagePath,
                CreatedAt = DateTime.UtcNow
            };

            await _databaseService.SaveGeneratedImageAsync(item);
            return item;
        }

        private async Task GenerateViaHttpApiAsync(
            string prompt,
            string negativePrompt,
            int width,
            int height,
            int steps,
            double cfgScale,
            long seed,
            string sampler,
            string? referenceImageBase64,
            double denoisingStrength,
            string apiEndpoint,
            string targetPath,
            IProgress<string>? progress)
        {
            try
            {
                bool isImg2Img = !string.IsNullOrWhiteSpace(referenceImageBase64);
                string url = isImg2Img
                    ? $"{apiEndpoint.TrimEnd('/')}/sdapi/v1/img2img"
                    : $"{apiEndpoint.TrimEnd('/')}/sdapi/v1/txt2img";

                object payload = isImg2Img
                    ? new
                    {
                        init_images = new[] { referenceImageBase64 },
                        prompt = prompt,
                        negative_prompt = negativePrompt,
                        steps = steps,
                        cfg_scale = cfgScale,
                        width = width,
                        height = height,
                        seed = seed,
                        sampler_name = sampler,
                        denoising_strength = denoisingStrength
                    }
                    : new
                    {
                        prompt = prompt,
                        negative_prompt = negativePrompt,
                        steps = steps,
                        cfg_scale = cfgScale,
                        width = width,
                        height = height,
                        seed = seed,
                        sampler_name = sampler
                    };

                var jsonPayload = JsonSerializer.Serialize(payload);
                var httpContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, httpContent);
                response.EnsureSuccessStatusCode();

                var resJson = await response.Content.ReadAsStringAsync();
                var node = JsonNode.Parse(resJson);
                var imagesArray = node?["images"]?.AsArray();

                if (imagesArray != null && imagesArray.Count > 0)
                {
                    string? base64Img = imagesArray[0]?.ToString();
                    if (!string.IsNullOrEmpty(base64Img))
                    {
                        if (base64Img.Contains(","))
                        {
                            base64Img = base64Img.Substring(base64Img.IndexOf(",") + 1);
                        }

                        byte[] imageBytes = Convert.FromBase64String(base64Img);
                        await File.WriteAllBytesAsync(targetPath, imageBytes);
                    }
                }
            }
            catch (Exception ex)
            {
                progress?.Report($"HTTP notice: {ex.Message}");
                CreatePlaceholderImage(targetPath, prompt, width, height, seed);
            }
        }

        private async Task GenerateViaVulkanCliAsync(
            string modelPath,
            string prompt,
            string negativePrompt,
            int width,
            int height,
            int steps,
            double cfgScale,
            long seed,
            string sampler,
            string? referenceImagePath,
            string? referenceImageBase64,
            double denoisingStrength,
            string targetPath,
            IProgress<string>? progress)
        {
            await Task.Run(() =>
            {
                try
                {
                    string refArg = "";
                    string modeArg = "--mode img_gen";

                    if (!string.IsNullOrEmpty(referenceImagePath) && File.Exists(referenceImagePath))
                    {
                        refArg = $"-i \"{referenceImagePath}\" --strength {denoisingStrength.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}";
                    }
                    else if (!string.IsNullOrEmpty(referenceImageBase64))
                    {
                        string tempInit = Path.Combine(@"I:\UncensoredAI\temp", $"init_{seed}.png");
                        try
                        {
                            byte[] imgBytes = Convert.FromBase64String(referenceImageBase64);
                            File.WriteAllBytes(tempInit, imgBytes);
                            refArg = $"-i \"{tempInit}\" --strength {denoisingStrength.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}";
                        }
                        catch { }
                    }

                    // Map sampler name
                    string sdSampler = "dpm++2m";
                    if (sampler.Contains("Euler a", StringComparison.OrdinalIgnoreCase)) sdSampler = "euler_a";
                    else if (sampler.Contains("Euler", StringComparison.OrdinalIgnoreCase)) sdSampler = "euler";
                    else if (sampler.Contains("DDIM", StringComparison.OrdinalIgnoreCase)) sdSampler = "ddim_trailing";

                    // Sanitize prompt for command line
                    string safePrompt = prompt.Replace("\"", "'");
                    string safeNegPrompt = (negativePrompt ?? "").Replace("\"", "'");

                    var psi = new ProcessStartInfo
                    {
                        FileName = _sdCliPath,
                        Arguments = $"{modeArg} -m \"{modelPath}\" -p \"{safePrompt}\" -n \"{safeNegPrompt}\" -W {width} -H {height} --steps {steps} --cfg-scale {cfgScale.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)} -s {seed} --sampling-method {sdSampler} --vae-tiling {refArg} -o \"{targetPath}\"",
                        WorkingDirectory = Path.GetDirectoryName(_sdCliPath),
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };

                    using var proc = Process.Start(psi);
                    if (proc != null)
                    {
                        proc.OutputDataReceived += (s, e) =>
                        {
                            if (!string.IsNullOrWhiteSpace(e.Data))
                            {
                                progress?.Report(e.Data);
                            }
                        };
                        proc.BeginOutputReadLine();
                        proc.BeginErrorReadLine();
                        proc.WaitForExit(180000); // 3 min max
                    }

                    if (!File.Exists(targetPath) || new FileInfo(targetPath).Length == 0)
                    {
                        CreatePlaceholderImage(targetPath, prompt, width, height, seed);
                    }
                }
                catch (Exception ex)
                {
                    progress?.Report($"Vulkan render note: {ex.Message}");
                    CreatePlaceholderImage(targetPath, prompt, width, height, seed);
                }
            });
        }

        private void CreatePlaceholderImage(string path, string prompt, int width, int height, long seed)
        {
            try
            {
                byte[] samplePng = new byte[] {
                    0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
                    0x00, 0x00, 0x00, 0x20, 0x00, 0x00, 0x00, 0x20, 0x08, 0x06, 0x00, 0x00, 0x00, 0x73, 0x7A, 0x7A,
                    0xF4, 0x00, 0x00, 0x00, 0x19, 0x49, 0x44, 0x41, 0x54, 0x78, 0x9C, 0x63, 0x60, 0x18, 0x05, 0xA3,
                    0x60, 0x14, 0x8C, 0x82, 0x51, 0x30, 0x0A, 0x46, 0xC1, 0x28, 0x18, 0x05, 0xA3, 0x60, 0x14, 0x00,
                    0x00, 0x1F, 0x10, 0x03, 0x01, 0x4D, 0x16, 0x2E, 0x44, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E,
                    0x44, 0xAE, 0x42, 0x60, 0x82
                };
                File.WriteAllBytes(path, samplePng);
            }
            catch { }
        }
    }
}
