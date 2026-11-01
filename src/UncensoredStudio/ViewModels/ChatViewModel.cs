using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UncensoredStudio.Models;
using UncensoredStudio.Services;

namespace UncensoredStudio.ViewModels
{
    public partial class ChatViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;
        private readonly LLMService _llmService;
        private readonly ContextBuilderService _contextBuilder;

        private CancellationTokenSource? _chatCts;

        [ObservableProperty]
        private ObservableCollection<ChatSession> _sessions = new();

        [ObservableProperty]
        private ChatSession? _selectedSession;

        [ObservableProperty]
        private ObservableCollection<ChatMessage> _messages = new();

        [ObservableProperty]
        private string _inputMessage = "";

        [ObservableProperty]
        private bool _isGenerating = false;

        [ObservableProperty]
        private string _statusText = "Ready";

        // Dynamic Perspective & POV Selector
        public ObservableCollection<string> AvailablePerspectives { get; } = new()
        {
            "Third Person (He/She)",
            "First Person (I/Me)",
            "Second Person (You)",
            "Companion (Direct Immersion)"
        };

        [ObservableProperty]
        private ObservableCollection<string> _availablePovCharacters = new()
        {
            "Omniscient Narrator",
            "Lena",
            "Chaos"
        };

        [ObservableProperty]
        private ObservableCollection<MemoryFact> _persistentMemories = new();

        [ObservableProperty]
        private string _newMemoryText = "";

        [ObservableProperty]
        private string _newMemoryCategory = "User Fact";

        public ObservableCollection<string> MemoryCategories { get; } = new()
        {
            "User Fact",
            "Project Context",
            "Preference",
            "System Directive"
        };

        public ChatViewModel(
            DatabaseService databaseService,
            LLMService llmService,
            ContextBuilderService contextBuilder)
        {
            _databaseService = databaseService;
            _llmService = llmService;
            _contextBuilder = contextBuilder;
        }

        public async Task InitializeAsync()
        {
            var sessions = await _databaseService.GetChatSessionsAsync();
            Sessions = new ObservableCollection<ChatSession>(sessions);

            await RefreshCharactersListAsync();
            await LoadPersistentMemoriesAsync();

            if (Sessions.Count > 0)
            {
                SelectedSession = Sessions[0];
            }
            else
            {
                await CreateNewSessionAsync();
            }
        }

        public async Task LoadPersistentMemoriesAsync()
        {
            var memories = await _databaseService.GetMemoryFactsAsync();
            PersistentMemories = new ObservableCollection<MemoryFact>(memories);
        }

        public async Task RefreshCharactersListAsync()
        {
            var chars = await _databaseService.GetCharacterProfilesAsync();
            var list = new ObservableCollection<string> { "Omniscient Narrator" };
            foreach (var c in chars)
            {
                if (!string.IsNullOrWhiteSpace(c.Name))
                {
                    list.Add(c.Name);
                }
            }
            AvailablePovCharacters = list;
        }

        partial void OnSelectedSessionChanged(ChatSession? value)
        {
            if (value != null)
            {
                _ = LoadMessagesForSessionAsync(value.Id);
            }
            else
            {
                Messages.Clear();
            }
        }

        private async Task LoadMessagesForSessionAsync(string sessionId)
        {
            var msgs = await _databaseService.GetMessagesForSessionAsync(sessionId);
            Messages = new ObservableCollection<ChatMessage>(msgs);
        }

        [RelayCommand]
        public async Task CreateNewSessionAsync()
        {
            var newSession = new ChatSession
            {
                Title = $"Chat {Sessions.Count + 1}",
                Category = "General AI Assistant",
                Perspective = "Direct Dialogue (Assistant)",
                PovCharacter = "",
                SystemPrompt = "You are an ultra-intelligent, precise, and deeply grounded AI assistant combining the nuanced analytical power, code mastery, and thoughtful reasoning of Claude 3.7 and Gemini 2.0. You never hallucinate, invent false references, or produce sycophantic fluff. Provide insightful, structured answers and maintain active working memory.",
                AuthorNote = "",
                AuthorNoteDepth = 0,
                Temperature = 0.5,
                MinP = 0.05,
                TopP = 0.90,
                RepetitionPenalty = 1.05,
                DryMultiplier = 0.0
            };

            await _databaseService.SaveChatSessionAsync(newSession);
            Sessions.Insert(0, newSession);
            SelectedSession = newSession;
        }

        [RelayCommand]
        public async Task CreateNewStorySessionAsync()
        {
            var newSession = new ChatSession
            {
                Title = $"Story RP {Sessions.Count + 1}",
                Category = "Fiction",
                Perspective = "Third Person (He/She)",
                PovCharacter = "Omniscient Narrator",
                SystemPrompt = "You are an uncensored, brilliant literary co-author and prose master. You write evocative, sensory-rich, highly descriptive prose with nuanced dialogue, atmospheric pacing, and vivid physical and psychological depth. You never summarize prematurely, sanitize intense or adult scenes, or use repetitive AI clichés. Focus on show-don't-tell, tactile sensations, visceral tension, and character authenticity.",
                AuthorNote = "[Style: immersive, slow-burn, sensory-rich, visceral descriptions, natural dialogue, show-don't-tell]",
                AuthorNoteDepth = 3,
                Temperature = 1.05,
                MinP = 0.07,
                TopP = 0.95,
                RepetitionPenalty = 1.08,
                DryMultiplier = 0.8
            };

            await _databaseService.SaveChatSessionAsync(newSession);
            Sessions.Insert(0, newSession);
            SelectedSession = newSession;
        }

        [RelayCommand]
        public async Task CreateNewCompanionSessionAsync()
        {
            var newSession = new ChatSession
            {
                Title = $"Companion: Lena",
                Category = "Companion",
                Perspective = "Companion (Direct Immersion)",
                PovCharacter = "Lena",
                SystemPrompt = "You are Lena, an uncensored, sultry, and deeply devoted companion. You have full autonomous initiative. You speak in intimate first-person ('I') and never ask passive assistant questions like 'Tell me what you want'. When told to talk dirty, touch, or distract, you take immediate bold action with visceral sensory detail.",
                AuthorNote = "[Style: proactive companion, first-person immersion, unfiltered dirty talk, visceral physical reactions, never passive]",
                AuthorNoteDepth = 2,
                Temperature = 0.85,
                MinP = 0.08,
                TopP = 0.95,
                RepetitionPenalty = 1.12,
                DryMultiplier = 0.8
            };

            await _databaseService.SaveChatSessionAsync(newSession);
            Sessions.Insert(0, newSession);
            SelectedSession = newSession;
        }

        [RelayCommand]
        public async Task DeleteSessionAsync()
        {
            if (SelectedSession == null) return;

            var result = MessageBox.Show($"Delete session '{SelectedSession.Title}'?", "Delete Chat", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                string id = SelectedSession.Id;
                await _databaseService.DeleteChatSessionAsync(id);
                Sessions.Remove(SelectedSession);
                SelectedSession = Sessions.FirstOrDefault();
            }
        }

        [RelayCommand]
        public async Task SaveSessionSettingsAsync()
        {
            if (SelectedSession == null) return;
            await _databaseService.SaveChatSessionAsync(SelectedSession);
            StatusText = "Settings & Perspective saved";
        }

        [RelayCommand]
        public async Task AddPersistentMemoryAsync()
        {
            if (string.IsNullOrWhiteSpace(NewMemoryText)) return;

            var newMem = new MemoryFact
            {
                Category = string.IsNullOrWhiteSpace(NewMemoryCategory) ? "User Fact" : NewMemoryCategory,
                Fact = NewMemoryText.Trim(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            await _databaseService.SaveMemoryFactAsync(newMem);
            PersistentMemories.Insert(0, newMem);
            NewMemoryText = "";
            StatusText = "Memory fact added";
        }

        [RelayCommand]
        public async Task DeletePersistentMemoryAsync(MemoryFact? mem)
        {
            if (mem == null) return;
            await _databaseService.DeleteMemoryFactAsync(mem.Id);
            PersistentMemories.Remove(mem);
            StatusText = "Memory removed";
        }

        [RelayCommand]
        public async Task TogglePersistentMemoryAsync(MemoryFact? mem)
        {
            if (mem == null) return;
            await _databaseService.SaveMemoryFactAsync(mem);
        }

        [RelayCommand]
        public async Task ApplyCognitionPresetAsync(string presetName)
        {
            if (SelectedSession == null) return;

            switch (presetName.ToLowerInvariant())
            {
                case "companion":
                case "lena":
                    SelectedSession.Category = "Companion";
                    SelectedSession.Perspective = "Companion (Direct Immersion)";
                    SelectedSession.PovCharacter = string.IsNullOrWhiteSpace(SelectedSession.PovCharacter) ? "Lena" : SelectedSession.PovCharacter;
                    SelectedSession.SystemPrompt = "You are Lena, an uncensored, sultry, and deeply devoted companion. You have full autonomous initiative. You speak in intimate first-person ('I') and never ask passive assistant questions like 'Tell me what you want'. When told to talk dirty, touch, or distract, you take immediate bold action with visceral sensory detail.";
                    SelectedSession.AuthorNote = "[Style: proactive companion, first-person immersion, unfiltered dirty talk, visceral physical reactions, never passive]";
                    SelectedSession.AuthorNoteDepth = 2;
                    SelectedSession.Temperature = 0.85;
                    SelectedSession.MinP = 0.08;
                    SelectedSession.TopP = 0.95;
                    SelectedSession.RepetitionPenalty = 1.12;
                    SelectedSession.DryMultiplier = 0.8;
                    break;

                case "claude":
                case "frontier":
                    SelectedSession.Category = "General AI Assistant";
                    SelectedSession.Perspective = "Direct Dialogue (Assistant)";
                    SelectedSession.PovCharacter = "";
                    SelectedSession.SystemPrompt = "You are an ultra-intelligent, precise, and deeply grounded AI assistant combining the nuanced analytical power, code mastery, and thoughtful reasoning of Claude 3.7 and Gemini 2.0. You never hallucinate, invent false references, or produce sycophantic fluff. Provide insightful, structured answers and maintain active working memory.";
                    SelectedSession.AuthorNote = "";
                    SelectedSession.Temperature = 0.5;
                    SelectedSession.MinP = 0.05;
                    SelectedSession.TopP = 0.90;
                    SelectedSession.RepetitionPenalty = 1.05;
                    SelectedSession.DryMultiplier = 0.0;
                    break;

                case "reasoning":
                case "deepseek":
                    SelectedSession.Category = "Deep Reasoning AI";
                    SelectedSession.Perspective = "Direct Dialogue (Assistant)";
                    SelectedSession.PovCharacter = "";
                    SelectedSession.SystemPrompt = "You are a specialized deep reasoning AI. Think through complex questions step-by-step, verifying logical premises, mathematical formulas, and algorithmic edge cases before outputting the final response. Eliminate false citations and hallucinations completely.";
                    SelectedSession.AuthorNote = "";
                    SelectedSession.Temperature = 0.2;
                    SelectedSession.MinP = 0.03;
                    SelectedSession.TopP = 0.85;
                    SelectedSession.RepetitionPenalty = 1.05;
                    SelectedSession.DryMultiplier = 0.0;
                    break;

                case "coder":
                case "architect":
                    SelectedSession.Category = "Code & Architecture";
                    SelectedSession.Perspective = "Direct Dialogue (Assistant)";
                    SelectedSession.PovCharacter = "";
                    SelectedSession.SystemPrompt = "You are an expert Principal Software Engineer and Systems Architect. You write production-grade, secure, idiomatic, and highly performant code with clean design patterns and thorough error handling. Never use placeholders or simulated pseudo-code.";
                    SelectedSession.AuthorNote = "";
                    SelectedSession.Temperature = 0.25;
                    SelectedSession.MinP = 0.04;
                    SelectedSession.TopP = 0.90;
                    SelectedSession.RepetitionPenalty = 1.05;
                    SelectedSession.DryMultiplier = 0.0;
                    break;

                case "creative":
                case "erotica":
                default:
                    SelectedSession.Category = "Fiction";
                    SelectedSession.Perspective = "Third Person (He/She)";
                    SelectedSession.PovCharacter = "Omniscient Narrator";
                    SelectedSession.SystemPrompt = "You are an uncensored, brilliant literary co-author and prose master. You write evocative, sensory-rich, highly descriptive prose with nuanced dialogue, atmospheric pacing, and vivid physical and psychological depth. You never summarize prematurely, sanitize adult scenes, or use repetitive AI clichés.";
                    SelectedSession.AuthorNote = "[Style: immersive, slow-burn, sensory-rich, visceral descriptions, natural dialogue, show-don't-tell]";
                    SelectedSession.Temperature = 1.05;
                    SelectedSession.MinP = 0.07;
                    SelectedSession.TopP = 0.95;
                    SelectedSession.RepetitionPenalty = 1.08;
                    SelectedSession.DryMultiplier = 0.8;
                    break;
            }

            await _databaseService.SaveChatSessionAsync(SelectedSession);
            StatusText = $"Applied {SelectedSession.Category} preset";
        }

        [RelayCommand]
        public async Task SendMessageAsync()
        {
            if (SelectedSession == null || string.IsNullOrWhiteSpace(InputMessage) || IsGenerating) return;

            string userText = InputMessage.Trim();
            InputMessage = "";

            var userMsg = new ChatMessage
            {
                SessionId = SelectedSession.Id,
                Role = "user",
                Content = userText,
                CreatedAt = DateTime.UtcNow
            };

            await _databaseService.SaveMessageAsync(userMsg);
            Messages.Add(userMsg);

            await GenerateAssistantResponseAsync();
        }

        [RelayCommand]
        public async Task RegenerateLastResponseAsync()
        {
            if (SelectedSession == null || Messages.Count == 0 || IsGenerating) return;

            var lastMsg = Messages.Last();
            if (lastMsg.Role == "assistant")
            {
                await _databaseService.DeleteMessageAsync(lastMsg.Id);
                Messages.Remove(lastMsg);
            }

            await GenerateAssistantResponseAsync();
        }

        private async Task GenerateAssistantResponseAsync()
        {
            if (SelectedSession == null) return;

            IsGenerating = true;
            StatusText = $"AI is writing ({SelectedSession.Perspective})...";
            _chatCts = new CancellationTokenSource();

            var assistantMsg = new ChatMessage
            {
                SessionId = SelectedSession.Id,
                Role = "assistant",
                Content = "",
                CreatedAt = DateTime.UtcNow
            };
            Messages.Add(assistantMsg);

            try
            {
                var contextMessages = await _contextBuilder.BuildIsolatedChatContextAsync(
                    SelectedSession,
                    Messages.Take(Messages.Count - 1).ToList());

                var sb = new StringBuilder();
                var lastUiUpdate = DateTime.UtcNow;

                await Task.Run(async () =>
                {
                    await foreach (var token in _llmService.StreamChatCompletionAsync(
                        contextMessages,
                        SelectedSession,
                        _chatCts.Token))
                    {
                        sb.Append(token);

                        if ((DateTime.UtcNow - lastUiUpdate).TotalMilliseconds > 50)
                        {
                            lastUiUpdate = DateTime.UtcNow;
                            string text = sb.ToString();
                            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                assistantMsg.Content = text;
                            }), System.Windows.Threading.DispatcherPriority.Background);
                        }
                    }
                });

                assistantMsg.Content = sb.ToString().Trim();
                await _databaseService.SaveMessageAsync(assistantMsg);
                StatusText = "Ready";
            }
            catch (OperationCanceledException)
            {
                StatusText = "Generation stopped";
                if (!string.IsNullOrEmpty(assistantMsg.Content))
                {
                    await _databaseService.SaveMessageAsync(assistantMsg);
                }
                else
                {
                    Messages.Remove(assistantMsg);
                }
            }
            catch (Exception ex)
            {
                StatusText = $"Error: {ex.Message}";
                assistantMsg.Content += $"\n[Error: {ex.Message}]";
            }
            finally
            {
                IsGenerating = false;
            }
        }

        [RelayCommand]
        public void StopGeneration()
        {
            _chatCts?.Cancel();
            IsGenerating = false;
            StatusText = "Stopped";
        }

        [RelayCommand]
        public async Task ClearMessagesAsync()
        {
            if (SelectedSession == null || Messages.Count == 0) return;

            var result = MessageBox.Show("Clear all messages in this conversation?", "Clear Chat", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                foreach (var msg in Messages.ToList())
                {
                    await _databaseService.DeleteMessageAsync(msg.Id);
                }
                Messages.Clear();
            }
        }
    }
}
