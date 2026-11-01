using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UncensoredStudio.Models;
using UncensoredStudio.Services;

namespace UncensoredStudio.ViewModels
{
    public partial class ImageStudioViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;
        private readonly ImageGenerationService _imageService;
        private readonly LLMService _llmService;

        [ObservableProperty]
        private string _prompt = "A striking, highly detailed portrait of Chaos, tall athletic build, dark tousled hair, piercing steel-grey eyes, wearing a dark bespoke tailored shirt, standing in a luxury penthouse under dramatic cinematic chiaroscuro rim lighting, masterpiece, raw candid photo, 8k uhd, photorealistic, sharp focus";

        [ObservableProperty]
        private string _negativePrompt = "low quality, blurry, deformed anatomy, bad hands, missing fingers, extra limbs, bad eyes, mutated, watermark, signature, poorly drawn face, text, bad proportions, jpeg artifacts";

        [ObservableProperty]
        private string _selectedStyle = "📸 Hyper-Photorealistic Cinema";

        public ObservableCollection<string> AvailableStyles { get; } = new()
        {
            "📸 Hyper-Photorealistic Cinema",
            "✨ Anime / Manhwa (Pony Style)",
            "🎨 Dark Fantasy Oil Painting",
            "🌃 Cyberpunk / Neon Noir",
            "🩸 Gothic Victorian / Dark Romance",
            "💄 Erotic Boudoir & Candid Intimacy"
        };

        [ObservableProperty]
        private string _selectedResolution = "Portrait (832x1216 - SDXL)";

        public ObservableCollection<string> AvailableResolutions { get; } = new()
        {
            "Portrait (832x1216 - SDXL)",
            "Landscape (1216x832 - SDXL)",
            "Square (1024x1024 - SDXL)",
            "Portrait (512x768 - SD 1.5)",
            "Square (512x512 - SD 1.5)"
        };

        [ObservableProperty]
        private int _steps = 25;

        [ObservableProperty]
        private double _cfgScale = 7.0;

        [ObservableProperty]
        private long _seed = -1;

        [ObservableProperty]
        private bool _isRandomSeed = true;

        [ObservableProperty]
        private string _sampler = "DPM++ 2M Karras";

        public ObservableCollection<string> AvailableSamplers { get; } = new()
        {
            "DPM++ 2M Karras",
            "Euler a",
            "Euler",
            "DPM++ SDE Karras",
            "DDIM"
        };

        // Reference / Img2Img properties
        [ObservableProperty]
        private string? _referenceImageBase64 = null;

        [ObservableProperty]
        private string? _referenceImagePath = null;

        [ObservableProperty]
        private bool _hasReferenceImage = false;

        [ObservableProperty]
        private double _denoisingStrength = 0.70;

        [ObservableProperty]
        private GeneratedImageItem? _activeImage;

        [ObservableProperty]
        private ObservableCollection<GeneratedImageItem> _galleryImages = new();

        [ObservableProperty]
        private bool _isGenerating = false;

        [ObservableProperty]
        private bool _isEnhancingPrompt = false;

        [ObservableProperty]
        private bool _isAnalyzingImage = false;

        [ObservableProperty]
        private string _statusText = "Ready to generate";

        [ObservableProperty]
        private bool _backendOnline = false;

        [ObservableProperty]
        private string _sdApiEndpoint = "http://127.0.0.1:7860";

        public ImageStudioViewModel(DatabaseService databaseService, ImageGenerationService imageService, LLMService llmService)
        {
            _databaseService = databaseService;
            _imageService = imageService;
            _llmService = llmService;
        }

        public async Task InitializeAsync()
        {
            var settings = await _databaseService.GetSettingsAsync();
            if (!string.IsNullOrWhiteSpace(settings.SdApiEndpoint))
            {
                SdApiEndpoint = settings.SdApiEndpoint;
            }

            var images = await _databaseService.GetGeneratedImagesAsync();
            GalleryImages = new ObservableCollection<GeneratedImageItem>(images);
            ActiveImage = GalleryImages.FirstOrDefault();

            _ = CheckEngineStatusAsync();
        }

        [RelayCommand]
        public async Task CheckEngineStatusAsync()
        {
            BackendOnline = await _imageService.CheckBackendOnlineAsync(SdApiEndpoint);
            StatusText = BackendOnline
                ? "⚡ Native AMD Vulkan Engine Ready (RX 6700 XT 12GB)"
                : "⚙️ Standalone Mode (Connect WebUI/Forge on port 7860 or run local sd.cpp)";
        }

        [RelayCommand]
        public async Task EnhancePromptAsync()
        {
            if (string.IsNullOrWhiteSpace(Prompt) || IsEnhancingPrompt) return;

            IsEnhancingPrompt = true;
            StatusText = "AI is expanding visual prompt...";

            try
            {
                string enhanced = await _imageService.EnhancePromptWithLLMAsync(Prompt, SelectedStyle);
                Prompt = enhanced;
                StatusText = "Prompt enhanced with lighting and quality tokens!";
            }
            finally
            {
                IsEnhancingPrompt = false;
            }
        }

        [RelayCommand]
        public async Task GenerateImageAsync()
        {
            if (string.IsNullOrWhiteSpace(Prompt) || IsGenerating) return;

            IsGenerating = true;
            StatusText = HasReferenceImage ? "Remaking image from reference..." : "Generating artwork...";

            int w = 832, h = 1216;
            if (SelectedResolution.Contains("Landscape")) { w = 1216; h = 832; }
            else if (SelectedResolution.Contains("1024x1024")) { w = 1024; h = 1024; }
            else if (SelectedResolution.Contains("512x768")) { w = 512; h = 768; }
            else if (SelectedResolution.Contains("512x512")) { w = 512; h = 512; }

            long targetSeed = IsRandomSeed ? -1 : Seed;

            var progress = new Progress<string>(msg => StatusText = msg);

            try
            {
                var result = await _imageService.GenerateImageAsync(
                    prompt: Prompt,
                    negativePrompt: NegativePrompt,
                    width: w,
                    height: h,
                    steps: Steps,
                    cfgScale: CfgScale,
                    seed: targetSeed,
                    sampler: Sampler,
                    referenceImageBase64: HasReferenceImage ? ReferenceImageBase64 : null,
                    denoisingStrength: DenoisingStrength,
                    apiEndpoint: SdApiEndpoint,
                    progress: progress
                );

                GalleryImages.Insert(0, result);
                ActiveImage = result;
                StatusText = "Artwork generated successfully!";
            }
            catch (Exception ex)
            {
                StatusText = $"Generation notice: {ex.Message}";
            }
            finally
            {
                IsGenerating = false;
            }
        }

        [RelayCommand]
        public void PasteImageFromClipboard()
        {
            try
            {
                if (Clipboard.ContainsImage())
                {
                    var bitmap = Clipboard.GetImage();
                    if (bitmap != null)
                    {
                        using var memoryStream = new MemoryStream();
                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(bitmap));
                        encoder.Save(memoryStream);
                        byte[] bytes = memoryStream.ToArray();

                        string tempRefPath = Path.Combine(@"I:\UncensoredAI\temp", $"ref_{DateTime.UtcNow.Ticks}.png");
                        try
                        {
                            if (!Directory.Exists(@"I:\UncensoredAI\temp")) Directory.CreateDirectory(@"I:\UncensoredAI\temp");
                            File.WriteAllBytes(tempRefPath, bytes);
                        }
                        catch { }

                        ReferenceImagePath = tempRefPath;
                        ReferenceImageBase64 = Convert.ToBase64String(bytes);
                        HasReferenceImage = true;
                        StatusText = "Pasted image from clipboard! Ready for Vision Analysis or Img2Img Remake.";
                    }
                }
                else
                {
                    MessageBox.Show("No image found on clipboard. Copy an image (Ctrl+C) first, then click Paste.", "Clipboard Empty", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                StatusText = $"Paste error: {ex.Message}";
            }
        }

        [RelayCommand]
        public void ClearReferenceImage()
        {
            ReferenceImageBase64 = null;
            ReferenceImagePath = null;
            HasReferenceImage = false;
            StatusText = "Cleared reference image. Switched to pure Text-to-Image.";
        }

        [RelayCommand]
        public async Task AnalyzeAndRemakeAsync()
        {
            if (!HasReferenceImage || string.IsNullOrEmpty(ReferenceImageBase64) || IsAnalyzingImage) return;

            IsAnalyzingImage = true;
            StatusText = "Vision AI is analyzing image details...";

            try
            {
                string breakdown = await _imageService.AnalyzeImageVisionAsync(ReferenceImageBase64, SdApiEndpoint);
                string enhancedPrompt = await _imageService.EnhancePromptWithLLMAsync(breakdown, SelectedStyle);
                Prompt = enhancedPrompt;
                StatusText = "Image analyzed! Prompt updated with subject details and ready to remake.";
            }
            catch (Exception ex)
            {
                StatusText = $"Analysis notice: {ex.Message}";
            }
            finally
            {
                IsAnalyzingImage = false;
            }
        }

        [RelayCommand]
        public void SetAsActive(GeneratedImageItem? item)
        {
            if (item != null)
            {
                ActiveImage = item;
                Prompt = item.Prompt;
                if (!string.IsNullOrEmpty(item.NegativePrompt)) NegativePrompt = item.NegativePrompt;
            }
        }

        [RelayCommand]
        public void CopyImageToClipboard()
        {
            if (ActiveImage == null || !File.Exists(ActiveImage.FilePath)) return;

            try
            {
                var uri = new Uri(ActiveImage.FilePath, UriKind.Absolute);
                var bmp = new BitmapImage(uri);
                Clipboard.SetImage(bmp);
                StatusText = "Image copied to clipboard!";
            }
            catch (Exception ex)
            {
                StatusText = $"Copy error: {ex.Message}";
            }
        }

        [RelayCommand]
        public void OpenExportsFolder()
        {
            string path = @"I:\UncensoredAI\exports\images";
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }

        [RelayCommand]
        public async Task DeleteActiveImageAsync()
        {
            if (ActiveImage == null) return;

            var res = MessageBox.Show("Delete this artwork from your gallery?", "Delete Image", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res == MessageBoxResult.Yes)
            {
                try
                {
                    if (File.Exists(ActiveImage.FilePath))
                    {
                        File.Delete(ActiveImage.FilePath);
                    }
                }
                catch { }

                await _databaseService.DeleteGeneratedImageAsync(ActiveImage.Id);
                GalleryImages.Remove(ActiveImage);
                ActiveImage = GalleryImages.FirstOrDefault();
                StatusText = "Artwork deleted";
            }
        }
    }
}
