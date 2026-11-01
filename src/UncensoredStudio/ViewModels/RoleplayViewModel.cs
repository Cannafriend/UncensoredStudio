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
    public partial class RoleplayViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;
        private readonly LLMService _llmService;
        private readonly ContextBuilderService _contextBuilder;

        private CancellationTokenSource? _rpCts;

        [ObservableProperty]
        private ObservableCollection<ChatSession> _sessions = new();

        [ObservableProperty]
        private ChatSession? _selectedSession;

        [ObservableProperty]
        private ObservableCollection<ChatMessage> _messages = new();

        [ObservableProperty]
        private ObservableCollection<CharacterProfile> _activeCast = new();

        [ObservableProperty]
        private ObservableCollection<string> _speakerOptions = new();

        [ObservableProperty]
        private string _selectedSpeaker = "You (Protagonist)";

        [ObservableProperty]
        private string _inputMessage = "";

        [ObservableProperty]
        private string _scenarioSetting = "Midnight at a luxury rooftop penthouse overlooking a rain-soaked metropolis. Tension is thick as secrets and dangerous motives begin to surface.";

        [ObservableProperty]
        private string _dynamicMood = "Slow-Burn Sensory Romance & Psychological Tension";

        public ObservableCollection<string> AvailableMoods { get; } = new()
        {
            "Slow-Burn Sensory Romance & Psychological Tension",
            "High-Stakes Confrontation & Dangerous Suspense",
            "Dark Cyberpunk / Mafia Underworld Noir",
            "Supernatural Mystery & Uncensored Intrigue",
            "Sharp Sarcastic Banter & Seductive Power Play",
            "Action Combat & High-Octane Escape"
        };

        [ObservableProperty]
        private bool _isGenerating = false;

        [ObservableProperty]
        private bool _isAutoPlaying = false;

        [ObservableProperty]
        private int _autoPlayRemaining = 0;

        [ObservableProperty]
        private string _statusText = "Ready for Roleplay";

        // Quick Character Creator properties
        [ObservableProperty]
        private string _newCharName = "";

        [ObservableProperty]
        private string _newCharRole = "";

        [ObservableProperty]
        private string _newCharPersonality = "";

        [ObservableProperty]
        private string _newCharDialogue = "";

        [ObservableProperty]
        private bool _showAddCharModal = false;

        public RoleplayViewModel(
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
            var allSessions = await _databaseService.GetChatSessionsAsync();
            var rpSessions = allSessions.Where(s => s.Category.Equals("Roleplay", StringComparison.OrdinalIgnoreCase) ||
                                                    s.Category.Equals("Chat RP", StringComparison.OrdinalIgnoreCase)).ToList();

            Sessions = new ObservableCollection<ChatSession>(rpSessions);

            await LoadCastProfilesAsync();

            if (Sessions.Count > 0)
            {
                SelectedSession = Sessions[0];
            }
            else
            {
                await CreateNewSceneAsync();
            }
        }

        public async Task LoadCastProfilesAsync()
        {
            var chars = await _databaseService.GetCharacterProfilesAsync();
            ActiveCast = new ObservableCollection<CharacterProfile>(chars);
            UpdateSpeakerOptions();
        }

        private void UpdateSpeakerOptions()
        {
            var list = new ObservableCollection<string>
            {
                "You (Protagonist)",
                "🎬 Director / Scene Event",
                "Omniscient Narrator"
            };

            foreach (var c in ActiveCast.Where(c => c.IsEnabled))
            {
                if (!string.IsNullOrWhiteSpace(c.Name) && !list.Contains(c.Name))
                {
                    list.Add(c.Name);
                }
            }

            SpeakerOptions = list;
            if (!SpeakerOptions.Contains(SelectedSpeaker))
            {
                SelectedSpeaker = SpeakerOptions.FirstOrDefault() ?? "You (Protagonist)";
            }
        }

        partial void OnSelectedSessionChanged(ChatSession? value)
        {
            if (value != null)
            {
                if (!string.IsNullOrWhiteSpace(value.SystemPrompt))
                {
                    ScenarioSetting = value.SystemPrompt;
                }
                if (!string.IsNullOrWhiteSpace(value.Perspective))
                {
                    DynamicMood = value.Perspective;
                }

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
        public async Task CreateNewSceneAsync()
        {
            var newSession = new ChatSession
            {
                Title = $"Scene {Sessions.Count + 1}: Uncensored Encounter",
                Category = "Chat RP",
                Perspective = DynamicMood,
                SystemPrompt = ScenarioSetting,
                AuthorNote = "[Style: immersive multi-character dialogue, tactile sensations, physical body language, authentic character depth]",
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
        public async Task DeleteSceneAsync()
        {
            if (SelectedSession == null) return;

            var result = MessageBox.Show($"Delete scene '{SelectedSession.Title}'?", "Delete Scene", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                string id = SelectedSession.Id;
                await _databaseService.DeleteChatSessionAsync(id);
                Sessions.Remove(SelectedSession);
                SelectedSession = Sessions.FirstOrDefault();
            }
        }

        [RelayCommand]
        public async Task SaveSceneSettingsAsync()
        {
            if (SelectedSession == null) return;

            SelectedSession.SystemPrompt = ScenarioSetting;
            SelectedSession.Perspective = DynamicMood;
            await _databaseService.SaveChatSessionAsync(SelectedSession);

            foreach (var c in ActiveCast)
            {
                await _databaseService.SaveCharacterProfileAsync(c);
            }

            UpdateSpeakerOptions();
            StatusText = "Scene settings & character cast saved";
        }

        [RelayCommand]
        public void ToggleCharacterInScene(CharacterProfile? c)
        {
            if (c == null) return;
            c.IsEnabled = !c.IsEnabled;
            _ = _databaseService.SaveCharacterProfileAsync(c);
            UpdateSpeakerOptions();
        }

        [RelayCommand]
        public void OpenAddCharacterModal()
        {
            NewCharName = "";
            NewCharRole = "";
            NewCharPersonality = "";
            NewCharDialogue = "";
            ShowAddCharModal = true;
        }

        [RelayCommand]
        public void CloseAddCharacterModal()
        {
            ShowAddCharModal = false;
        }

        [RelayCommand]
        public async Task SaveNewCharacterAsync()
        {
            if (string.IsNullOrWhiteSpace(NewCharName)) return;

            var newChar = new CharacterProfile
            {
                Name = NewCharName.Trim(),
                Aliases = NewCharName.Trim(),
                AgeRole = string.IsNullOrWhiteSpace(NewCharRole) ? "Character" : NewCharRole.Trim(),
                Personality = string.IsNullOrWhiteSpace(NewCharPersonality) ? "Dynamic and expressive." : NewCharPersonality.Trim(),
                DialogueStyle = string.IsNullOrWhiteSpace(NewCharDialogue) ? "Natural and sharp." : NewCharDialogue.Trim(),
                Appearance = "Distinct presence and attire.",
                KinksAndDesires = "Driven by deep internal motives.",
                IsEnabled = true,
                UpdatedAt = DateTime.UtcNow
            };

            await _databaseService.SaveCharacterProfileAsync(newChar);
            ActiveCast.Add(newChar);
            UpdateSpeakerOptions();
            ShowAddCharModal = false;
            StatusText = $"Added character '{newChar.Name}' to cast";
        }

        [RelayCommand]
        public async Task SendTurnAsync()
        {
            if (SelectedSession == null || string.IsNullOrWhiteSpace(InputMessage) || IsGenerating) return;

            string text = InputMessage.Trim();
            InputMessage = "";

            string formattedContent;
            string role = "user";

            if (SelectedSpeaker.Contains("Director", StringComparison.OrdinalIgnoreCase))
            {
                formattedContent = $"[🎬 DIRECTOR / SCENE EVENT: {text}]";
            }
            else if (SelectedSpeaker.Contains("Narrator", StringComparison.OrdinalIgnoreCase))
            {
                formattedContent = $"**[Narrator]**: {text}";
            }
            else if (SelectedSpeaker.StartsWith("You", StringComparison.OrdinalIgnoreCase))
            {
                formattedContent = $"**[You]**: {text}";
            }
            else
            {
                formattedContent = $"**[{SelectedSpeaker}]**: {text}";
            }

            var msg = new ChatMessage
            {
                SessionId = SelectedSession.Id,
                Role = role,
                Content = formattedContent,
                CreatedAt = DateTime.UtcNow
            };

            await _databaseService.SaveMessageAsync(msg);
            Messages.Add(msg);

            // Trigger AI characters to react to this turn
            await AdvanceCharacterTurnAsync();
        }

        [RelayCommand]
        public async Task AdvanceNextTurnAsync()
        {
            if (SelectedSession == null || IsGenerating) return;
            await AdvanceCharacterTurnAsync();
        }

        [RelayCommand]
        public async Task StartContinuousAutoPlayAsync()
        {
            await StartAutoPlayAsync(-1);
        }

        [RelayCommand]
        public async Task StartAutoPlayAsync(object? param = null)
        {
            if (SelectedSession == null || IsGenerating || IsAutoPlaying) return;

            int turns = 3;
            bool isContinuous = false;

            if (param is int i)
            {
                if (i == -1 || i > 1000) isContinuous = true;
                else turns = i;
            }
            else if (param is string s)
            {
                if (s.Equals("infinite", StringComparison.OrdinalIgnoreCase) || 
                    s.Equals("continuous", StringComparison.OrdinalIgnoreCase) ||
                    s.Equals("-1"))
                {
                    isContinuous = true;
                }
                else if (int.TryParse(s, out int parsed))
                {
                    if (parsed == -1 || parsed > 1000) isContinuous = true;
                    else turns = parsed;
                }
            }

            IsAutoPlaying = true;
            AutoPlayRemaining = isContinuous ? int.MaxValue : turns;
            int turnCounter = 0;

            try
            {
                while (AutoPlayRemaining > 0 && IsAutoPlaying)
                {
                    turnCounter++;
                    StatusText = isContinuous
                        ? $"🎭 Infinite Auto-Play Active (Turn #{turnCounter}). Click '⏹ Stop' anytime..."
                        : $"⚡ Auto-Play Active (Turn {turns - AutoPlayRemaining + 1}/{turns})...";

                    await AdvanceCharacterTurnAsync();

                    if (!isContinuous)
                    {
                        AutoPlayRemaining--;
                    }

                    if (AutoPlayRemaining > 0 && IsAutoPlaying)
                    {
                        // Small natural reading pause between back-and-forth turns
                        await Task.Delay(800);
                    }
                }
            }
            finally
            {
                IsAutoPlaying = false;
                AutoPlayRemaining = 0;
                StatusText = "Auto-Play stopped";
            }
        }

        [RelayCommand]
        public void StopAutoPlay()
        {
            IsAutoPlaying = false;
            AutoPlayRemaining = 0;
            _rpCts?.Cancel();
            IsGenerating = false;
            StatusText = "Roleplay stopped";
        }

        private async Task AdvanceCharacterTurnAsync()
        {
            if (SelectedSession == null) return;

            IsGenerating = true;
            StatusText = "AI characters are interacting...";
            _rpCts = new CancellationTokenSource();

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
                var enabledCast = ActiveCast.Where(c => c.IsEnabled).ToList();
                var promptInstruction = Messages.Count <= 1
                    ? "[Opening Scene: Introduce the scene setting and have the lead characters make their opening interaction with dialogue, physical presence, and sensory detail.]"
                    : "[Next Turn: Advance the conversation and interactions from the active cast naturally. The responding character speaks and reacts with rich body language, micro-expressions, and dialogue.]";

                var contextMessages = await _contextBuilder.BuildRoleplayContextAsync(
                    SelectedSession,
                    enabledCast,
                    ScenarioSetting,
                    DynamicMood,
                    Messages.Take(Messages.Count - 1).ToList(),
                    promptInstruction);

                var sb = new StringBuilder();
                var lastUiUpdate = DateTime.UtcNow;

                await Task.Run(async () =>
                {
                    await foreach (var token in _llmService.StreamChatCompletionAsync(
                        contextMessages,
                        SelectedSession,
                        _rpCts.Token))
                    {
                        sb.Append(token);

                        if ((DateTime.UtcNow - lastUiUpdate).TotalMilliseconds > 40)
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
                StatusText = "Turn stopped";
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
        public async Task RegenerateLastTurnAsync()
        {
            if (SelectedSession == null || Messages.Count == 0 || IsGenerating) return;

            var lastMsg = Messages.Last();
            if (lastMsg.Role == "assistant")
            {
                await _databaseService.DeleteMessageAsync(lastMsg.Id);
                Messages.Remove(lastMsg);
                await AdvanceCharacterTurnAsync();
            }
        }

        [RelayCommand]
        public async Task ClearSceneMessagesAsync()
        {
            if (SelectedSession == null || Messages.Count == 0) return;

            var result = MessageBox.Show("Clear all dialogue and turns in this scene?", "Clear Scene", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                foreach (var msg in Messages.ToList())
                {
                    await _databaseService.DeleteMessageAsync(msg.Id);
                }
                Messages.Clear();
                StatusText = "Scene cleared";
            }
        }
    }
}
