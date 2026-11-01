using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace UncensoredStudio.Models
{
    public class ModelItem : ObservableObject
    {
        public string DisplayName { get; set; } = "";
        public string FullPath { get; set; } = "";
        public string Description { get; set; } = "";
        public string Tag { get; set; } = "";
        public string SizeDisplay { get; set; } = "";

        public override string ToString() => string.IsNullOrWhiteSpace(DisplayName) ? FullPath : DisplayName;

        public static ModelItem FromPath(string fullPath)
        {
            string fileName = Path.GetFileNameWithoutExtension(fullPath);
            string name = fileName;
            string desc = "Local GGUF model";
            string tag = "Local";

            if (fileName.Contains("deepseek", StringComparison.OrdinalIgnoreCase) || fileName.Contains("r1", StringComparison.OrdinalIgnoreCase))
            {
                name = "DeepSeek-R1-Distill-Qwen-14B (Reasoning)";
                desc = "Deep chain-of-thought verification, complex logic, step-by-step reasoning.";
                tag = "🔬 Deep Reasoning";
            }
            else if (fileName.Contains("qwen", StringComparison.OrdinalIgnoreCase))
            {
                if (fileName.Contains("32b", StringComparison.OrdinalIgnoreCase))
                {
                    name = "Qwen-2.5-32B-Instruct (Opus / Ultra Tier)";
                    desc = "Frontier-grade reasoning, coding, deep architecture & long-term coherence.";
                    tag = "🏆 Ultra Frontier";
                }
                else if (fileName.Contains("14b", StringComparison.OrdinalIgnoreCase))
                {
                    name = "Qwen-2.5-14B-Instruct (Claude 3.5 / Gemini Tier)";
                    desc = "Premier general reasoning, nuanced coding & factual zero-hallucination assistant.";
                    tag = "🧠 Frontier AI";
                }
                else
                {
                    name = "Qwen-2.5-7B-Instruct (Fast Assistant)";
                    desc = "Blazing fast general assistant, smart reasoning, coding & zero fluff.";
                    tag = "⚡ High Speed AI";
                }
            }
            else if (fileName.Contains("magnum", StringComparison.OrdinalIgnoreCase))
            {
                name = "Magnum-12B-v2 (Mistral Nemo)";
                desc = "Best for deep descriptive prose, slow-burn romance, adult fiction & Twitter teasers.";
                tag = "👑 Top Literary";
            }
            else if (fileName.Contains("stheno", StringComparison.OrdinalIgnoreCase))
            {
                name = "Llama-3.1-8B-Stheno-v3.2";
                desc = "Blazing fast character dialogue, reactive banter & expressive roleplay.";
                tag = "⚡ High Speed RP";
            }
            else if (fileName.Contains("rpmax", StringComparison.OrdinalIgnoreCase))
            {
                name = "Mistral-Nemo-12B-RPMax";
                desc = "128k context support, dark fantasy & long-form narrative coherence.";
                tag = "📖 Long Novels";
            }

            string sizeStr = "";
            try
            {
                if (File.Exists(fullPath))
                {
                    var fi = new FileInfo(fullPath);
                    sizeStr = $"{fi.Length / (1024.0 * 1024.0 * 1024.0):F1} GB";
                }
            }
            catch { }

            return new ModelItem
            {
                DisplayName = name,
                FullPath = fullPath,
                Description = desc,
                Tag = tag,
                SizeDisplay = sizeStr
            };
        }
    }

    public class CharacterProfile : ObservableObject
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string? ProjectId { get; set; } = null; // null = Global Character
        public string? SessionId { get; set; } = null;

        private string _name = "Chaos";
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        private string _aliases = "Chaos, Sir Chaos";
        public string Aliases
        {
            get => _aliases;
            set => SetProperty(ref _aliases, value);
        }

        private string _ageRole = "32, Enigmatic Recluse";
        public string AgeRole
        {
            get => _ageRole;
            set => SetProperty(ref _ageRole, value);
        }

        private string _appearance = "Tall, athletic lean build, dark tousled hair, sharp jawline, striking steel-grey eyes. Often smells of cedarwood and faint amber. Moves with calculated, predatory stillness.";
        public string Appearance
        {
            get => _appearance;
            set => SetProperty(ref _appearance, value);
        }

        private string _personality = "Calm, intensely perceptive, dominant presence, guarded emotional vulnerability, dry sarcasm under tension. Speaks in a low, measured baritone.";
        public string Personality
        {
            get => _personality;
            set => SetProperty(ref _personality, value);
        }

        private string _kinksAndDesires = "Slow-burn psychological tension, sensory deprivation, teasing control, passionate possessiveness, deep emotional bonding under intense intimacy.";
        public string KinksAndDesires
        {
            get => _kinksAndDesires;
            set => SetProperty(ref _kinksAndDesires, value);
        }

        private string _dialogueStyle = "Deep, deliberate cadence. Rarely raises voice. Uses sharp, teasing remarks.";
        public string DialogueStyle
        {
            get => _dialogueStyle;
            set => SetProperty(ref _dialogueStyle, value);
        }

        private bool _isEnabled = true;
        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetProperty(ref _isEnabled, value);
        }

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public class Project : ObservableObject
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        private string _title = "Untitled Novel";
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }
        private string _genre = "Dark Romance / Erotic Fiction";
        public string Genre
        {
            get => _genre;
            set => SetProperty(ref _genre, value);
        }
        private string _synopsis = "";
        public string Synopsis
        {
            get => _synopsis;
            set => SetProperty(ref _synopsis, value);
        }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        private int _totalWordCount;
        public int TotalWordCount
        {
            get => _totalWordCount;
            set => SetProperty(ref _totalWordCount, value);
        }
    }

    public class Chapter : ObservableObject
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ProjectId { get; set; } = "";
        private string _title = "Chapter 1";
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }
        public int OrderIndex { get; set; } = 0;
        private string _content = "";
        public string Content
        {
            get => _content;
            set
            {
                if (SetProperty(ref _content, value))
                {
                    WordCount = CalculateWordCount(value);
                }
            }
        }
        private string _notes = "";
        public string Notes
        {
            get => _notes;
            set => SetProperty(ref _notes, value);
        }
        private int _wordCount;
        public int WordCount
        {
            get => _wordCount;
            set => SetProperty(ref _wordCount, value);
        }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public static int CalculateWordCount(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;
            return text.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
        }
    }

    public class ChatSession : ObservableObject
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        private string _title = "New Story Session";
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }
        private string _category = "Fiction";
        public string Category
        {
            get => _category;
            set => SetProperty(ref _category, value);
        }
        private string _systemPrompt = "You are an uncensored, brilliant literary co-author and prose master. You write evocative, sensory-rich, highly descriptive prose with nuanced dialogue, atmospheric pacing, and vivid physical and psychological depth. You never summarize prematurely, sanitize intense or adult scenes, or use repetitive AI clichés. Focus on show-don't-tell, tactile sensations, visceral tension, and character authenticity.";
        public string SystemPrompt
        {
            get => _systemPrompt;
            set => SetProperty(ref _systemPrompt, value);
        }
        private string _authorNote = "[Style: immersive, slow-burn, sensory-rich, visceral descriptions, natural dialogue, show-don't-tell]";
        public string AuthorNote
        {
            get => _authorNote;
            set => SetProperty(ref _authorNote, value);
        }
        public int AuthorNoteDepth { get; set; } = 3;

        private string _perspective = "Third Person (He/She)"; // Third Person (He/She), First Person (I/Me), Second Person (You)
        public string Perspective
        {
            get => _perspective;
            set => SetProperty(ref _perspective, value);
        }

        private string _povCharacter = "Omniscient Narrator";
        public string PovCharacter
        {
            get => _povCharacter;
            set => SetProperty(ref _povCharacter, value);
        }

        // Sampler parameters
        public double Temperature { get; set; } = 1.05;
        public double MinP { get; set; } = 0.07;
        public double TopP { get; set; } = 0.95;
        public double RepetitionPenalty { get; set; } = 1.08;
        public double DryMultiplier { get; set; } = 0.8;
        public double DryBase { get; set; } = 1.75;
        public int DryAllowedLength { get; set; } = 2;
        public int MaxTokens { get; set; } = 1024;
        public int ContextLimit { get; set; } = 8192;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public class ChatMessage : ObservableObject
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string SessionId { get; set; } = "";
        public string Role { get; set; } = "user";
        private string _content = "";
        public string Content
        {
            get => _content;
            set => SetProperty(ref _content, value);
        }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int TokenCount { get; set; } = 0;
        public bool IsPinned { get; set; } = false;

        public bool IsUser => Role.Equals("user", StringComparison.OrdinalIgnoreCase);
        public bool IsAssistant => Role.Equals("assistant", StringComparison.OrdinalIgnoreCase);
        public bool IsSystem => Role.Equals("system", StringComparison.OrdinalIgnoreCase);
    }

    public class LoreEntry : ObservableObject
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string? SessionId { get; set; } = null;
        private string _title = "New Entity";
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }
        private string _keys = "";
        public string Keys
        {
            get => _keys;
            set => SetProperty(ref _keys, value);
        }
        private string _content = "";
        public string Content
        {
            get => _content;
            set => SetProperty(ref _content, value);
        }
        private string _category = "Character";
        public string Category
        {
            get => _category;
            set => SetProperty(ref _category, value);
        }
        private bool _isEnabled = true;
        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetProperty(ref _isEnabled, value);
        }
        public int InsertionOrder { get; set; } = 100;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public class TwitterDraft : ObservableObject
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        private string _title = "New Spicy Tweet";
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }
        private string _category = "Micro-Smut";
        public string Category
        {
            get => _category;
            set => SetProperty(ref _category, value);
        }
        private string _rawContent = "";
        public string RawContent
        {
            get => _rawContent;
            set => SetProperty(ref _rawContent, value);
        }
        public string TweetsJson { get; set; } = "[]";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public class TweetCard : ObservableObject
    {
        public int Index { get; set; } = 1;
        private string _text = "";
        public string Text
        {
            get => _text;
            set
            {
                if (SetProperty(ref _text, value))
                {
                    CharCount = value.Length;
                    IsOverLimit = value.Length > 280;
                }
            }
        }
        private int _charCount = 0;
        public int CharCount
        {
            get => _charCount;
            set => SetProperty(ref _charCount, value);
        }
        private bool _isOverLimit = false;
        public bool IsOverLimit
        {
            get => _isOverLimit;
            set => SetProperty(ref _isOverLimit, value);
        }
    }

    public class PromptPreset
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string SystemPrompt { get; set; } = "";
        public string AuthorNote { get; set; } = "";
        public double Temperature { get; set; } = 1.0;
        public double MinP { get; set; } = 0.07;
        public double TopP { get; set; } = 0.95;
        public double RepetitionPenalty { get; set; } = 1.08;
        public double DryMultiplier { get; set; } = 0.8;
    }

    public class MemoryFact : ObservableObject
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Category { get; set; } = "General"; // User Fact, Preference, Project Context, Grounding Rule
        
        private string _fact = "";
        public string Fact
        {
            get => _fact;
            set => SetProperty(ref _fact, value);
        }

        private bool _isActive = true;
        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class DownloadableModelInfo : ObservableObject
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Category { get; set; } = "General AI";
        public string Description { get; set; } = "";
        public string RecommendedFor { get; set; } = "";
        public string BenchmarkTier { get; set; } = "";
        public string SizeDisplay { get; set; } = "";
        public string FileName { get; set; } = "";
        public string DownloadUrl { get; set; } = "";
        public string ParameterCount { get; set; } = "";
        
        public double RequiredVramGb { get; set; } = 8.0;
        public double RequiredRamGb { get; set; } = 16.0;

        private string _hardwareStatus = "Checking hardware compatibility...";
        public string HardwareStatus
        {
            get => _hardwareStatus;
            set => SetProperty(ref _hardwareStatus, value);
        }

        private string _hardwareStatusColor = "#10B981";
        public string HardwareStatusColor
        {
            get => _hardwareStatusColor;
            set => SetProperty(ref _hardwareStatusColor, value);
        }

        private string _hardwareStatusDetails = "";
        public string HardwareStatusDetails
        {
            get => _hardwareStatusDetails;
            set => SetProperty(ref _hardwareStatusDetails, value);
        }

        private bool _isInstalled = false;
        public bool IsInstalled
        {
            get => _isInstalled;
            set => SetProperty(ref _isInstalled, value);
        }

        private string _localPath = "";
        public string LocalPath
        {
            get => _localPath;
            set => SetProperty(ref _localPath, value);
        }
    }

    public class AppSettings : ObservableObject
    {
        public string Id { get; set; } = "default";
        private string _apiEndpoint = "http://127.0.0.1:5001";
        public string ApiEndpoint
        {
            get => _apiEndpoint;
            set => SetProperty(ref _apiEndpoint, value);
        }
        private string _selectedModel = "";
        public string SelectedModel
        {
            get => _selectedModel;
            set => SetProperty(ref _selectedModel, value);
        }
        private string _modelsDirectory = @"I:\UncensoredAI\models";
        public string ModelsDirectory
        {
            get => _modelsDirectory;
            set => SetProperty(ref _modelsDirectory, value);
        }
        private string _backendPath = @"I:\UncensoredAI\backend\koboldcpp.exe";
        public string BackendPath
        {
            get => _backendPath;
            set => SetProperty(ref _backendPath, value);
        }
        private bool _autoStartBackend = true;
        public bool AutoStartBackend
        {
            get => _autoStartBackend;
            set => SetProperty(ref _autoStartBackend, value);
        }
        private bool _isFirstRunCompleted = false;
        public bool IsFirstRunCompleted
        {
            get => _isFirstRunCompleted;
            set => SetProperty(ref _isFirstRunCompleted, value);
        }
        private bool _alwaysPromptModelPicker = true;
        public bool AlwaysPromptModelPicker
        {
            get => _alwaysPromptModelPicker;
            set => SetProperty(ref _alwaysPromptModelPicker, value);
        }
        private int _contextSize = 8192;
        public int ContextSize
        {
            get => _contextSize;
            set => SetProperty(ref _contextSize, value);
        }
        private int _gpuLayers = 99;
        public int GpuLayers
        {
            get => _gpuLayers;
            set => SetProperty(ref _gpuLayers, value);
        }
        private string _theme = "Obsidian";
        public string Theme
        {
            get => _theme;
            set => SetProperty(ref _theme, value);
        }
        private string _selectedFontFamily = "Segoe UI, sans-serif";
        public string SelectedFontFamily
        {
            get => _selectedFontFamily;
            set => SetProperty(ref _selectedFontFamily, value);
        }
        private string _sdApiEndpoint = "http://127.0.0.1:7860";
        public string SdApiEndpoint
        {
            get => _sdApiEndpoint;
            set => SetProperty(ref _sdApiEndpoint, value);
        }
        private string _imageExportDirectory = @"I:\UncensoredAI\exports\images";
        public string ImageExportDirectory
        {
            get => _imageExportDirectory;
            set => SetProperty(ref _imageExportDirectory, value);
        }
    }

    public class GeneratedImageItem : ObservableObject
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string FilePath { get; set; } = "";
        public string Prompt { get; set; } = "";
        public string NegativePrompt { get; set; } = "";
        public string ModelName { get; set; } = "SDXL / Pony";
        public string Sampler { get; set; } = "DPM++ 2M Karras";
        public int Steps { get; set; } = 25;
        public double CfgScale { get; set; } = 7.0;
        public long Seed { get; set; } = -1;
        public int Width { get; set; } = 832;
        public int Height { get; set; } = 1216;
        public double DenoisingStrength { get; set; } = 0.7;
        public bool IsImg2Img { get; set; } = false;
        public string? ReferenceImagePath { get; set; } = null;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class FontOption
    {
        public string DisplayName { get; set; } = "";
        public string FontFamilyValue { get; set; } = "";
        public string Category { get; set; } = "";
        public string PreviewText { get; set; } = "The quick brown fox jumps over the lazy dog";
    }
}
