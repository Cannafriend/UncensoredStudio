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
    public partial class NovelViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;
        private readonly LLMService _llmService;
        private readonly ContextBuilderService _contextBuilder;
        private readonly ExportService _exportService;

        private CancellationTokenSource? _generationCts;

        [ObservableProperty]
        private ObservableCollection<Project> _projects = new();

        [ObservableProperty]
        private Project? _selectedProject;

        [ObservableProperty]
        private ObservableCollection<Chapter> _chapters = new();

        [ObservableProperty]
        private Chapter? _selectedChapter;

        [ObservableProperty]
        private string _activeChapterContent = "";

        [ObservableProperty]
        private string _selectedText = "";

        [ObservableProperty]
        private string _aiSuggestionText = "";

        [ObservableProperty]
        private string _customInstructionText = "";

        [ObservableProperty]
        private bool _isGenerating = false;

        [ObservableProperty]
        private string _generationStatus = "Ready";

        // Character Management inside Story Studio
        [ObservableProperty]
        private ObservableCollection<CharacterProfile> _characters = new();

        [ObservableProperty]
        private CharacterProfile? _selectedCharacter;

        [ObservableProperty]
        private ObservableCollection<CharacterProfile> _triggeredCharacters = new();

        [ObservableProperty]
        private ObservableCollection<LoreEntry> _triggeredLoreEntries = new();

        [ObservableProperty]
        private bool _isCharacterDrawerOpen = false;

        // Dynamic Perspective & POV
        [ObservableProperty]
        private string _selectedPerspective = "Third Person (He/She)";

        public ObservableCollection<string> AvailablePerspectives { get; } = new()
        {
            "Third Person (He/She)",
            "First Person (I/Me)",
            "Second Person (You)"
        };

        [ObservableProperty]
        private string _povFocus = "Omniscient Narrator";

        public NovelViewModel(
            DatabaseService databaseService,
            LLMService llmService,
            ContextBuilderService contextBuilder,
            ExportService exportService)
        {
            _databaseService = databaseService;
            _llmService = llmService;
            _contextBuilder = contextBuilder;
            _exportService = exportService;
        }

        public async Task InitializeAsync()
        {
            var projects = await _databaseService.GetProjectsAsync();
            Projects = new ObservableCollection<Project>(projects);

            if (Projects.Count > 0)
            {
                SelectedProject = Projects[0];
            }
            else
            {
                await CreateNewProjectAsync();
            }

            await LoadCharactersAsync();
        }

        public async Task LoadCharactersAsync()
        {
            var chars = await _databaseService.GetCharacterProfilesAsync(SelectedProject?.Id);
            Characters = new ObservableCollection<CharacterProfile>(chars);
            if (Characters.Count > 0)
            {
                SelectedCharacter = Characters[0];
            }
        }

        partial void OnSelectedProjectChanged(Project? value)
        {
            if (value != null)
            {
                _ = LoadChaptersForProjectAsync(value.Id);
                _ = LoadCharactersAsync();
            }
            else
            {
                Chapters.Clear();
                SelectedChapter = null;
            }
        }

        partial void OnSelectedChapterChanged(Chapter? value)
        {
            if (value != null)
            {
                ActiveChapterContent = value.Content ?? "";
                UpdateTriggeredContext();
            }
            else
            {
                ActiveChapterContent = "";
                TriggeredCharacters.Clear();
                TriggeredLoreEntries.Clear();
            }
        }

        partial void OnActiveChapterContentChanged(string value)
        {
            if (SelectedChapter != null)
            {
                SelectedChapter.Content = value;
                UpdateTriggeredContext();
            }
        }

        private async Task LoadChaptersForProjectAsync(string projectId)
        {
            var chaps = await _databaseService.GetChaptersForProjectAsync(projectId);
            Chapters = new ObservableCollection<Chapter>(chaps);
            if (Chapters.Count > 0)
            {
                SelectedChapter = Chapters[0];
            }
            else
            {
                await AddNewChapterAsync();
            }
        }

        [RelayCommand]
        public void ToggleCharacterDrawer()
        {
            IsCharacterDrawerOpen = !IsCharacterDrawerOpen;
        }

        [RelayCommand]
        public async Task CreateNewCharacterAsync()
        {
            var newChar = new CharacterProfile
            {
                ProjectId = SelectedProject?.Id,
                Name = "New Character",
                Aliases = "Name, Alias",
                AgeRole = "28, Protagonist",
                Appearance = "Physical description, build, hair, eyes, scent, posture...",
                Personality = "Mannerisms, speech cadence, quirks, temperament...",
                KinksAndDesires = "Desires, intimate turn-ons, boundaries, sexual dynamic...",
                DialogueStyle = "Dialogue tone and manner of speaking..."
            };

            await _databaseService.SaveCharacterProfileAsync(newChar);
            Characters.Insert(0, newChar);
            SelectedCharacter = newChar;
            IsCharacterDrawerOpen = true;
        }

        [RelayCommand]
        public async Task SaveSelectedCharacterAsync()
        {
            if (SelectedCharacter == null) return;
            await _databaseService.SaveCharacterProfileAsync(SelectedCharacter);
            UpdateTriggeredContext();
            MessageBox.Show($"Character '{SelectedCharacter.Name}' saved successfully!", "Character Profile Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        [RelayCommand]
        public async Task DeleteSelectedCharacterAsync()
        {
            if (SelectedCharacter == null) return;

            var res = MessageBox.Show($"Delete character profile '{SelectedCharacter.Name}'?", "Delete Character", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res == MessageBoxResult.Yes)
            {
                await _databaseService.DeleteCharacterProfileAsync(SelectedCharacter.Id);
                Characters.Remove(SelectedCharacter);
                SelectedCharacter = Characters.FirstOrDefault();
                UpdateTriggeredContext();
            }
        }

        [RelayCommand]
        public async Task CreateNewProjectAsync()
        {
            var newProj = new Project
            {
                Title = "New Story Project",
                Genre = "Dark Romance / Erotica",
                Synopsis = "Write your novel's core plot, stakes, and main character dynamics here..."
            };
            await _databaseService.SaveProjectAsync(newProj);
            Projects.Insert(0, newProj);
            SelectedProject = newProj;

            await AddNewChapterAsync();
        }

        [RelayCommand]
        public async Task DeleteProjectAsync()
        {
            if (SelectedProject == null) return;

            var result = MessageBox.Show($"Are you sure you want to delete '{SelectedProject.Title}' and all its chapters?",
                "Delete Project", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                string id = SelectedProject.Id;
                await _databaseService.DeleteProjectAsync(id);
                Projects.Remove(SelectedProject);
                SelectedProject = Projects.FirstOrDefault();
            }
        }

        [RelayCommand]
        public async Task AddNewChapterAsync()
        {
            if (SelectedProject == null) return;

            int nextIndex = Chapters.Count;
            var newChap = new Chapter
            {
                ProjectId = SelectedProject.Id,
                Title = $"Chapter {nextIndex + 1}",
                OrderIndex = nextIndex,
                Content = "Start writing your chapter here, or use the AI tools on the right to draft the opening..."
            };

            await _databaseService.SaveChapterAsync(newChap);
            Chapters.Add(newChap);
            SelectedChapter = newChap;
        }

        [RelayCommand]
        public async Task DeleteChapterAsync()
        {
            if (SelectedChapter == null) return;

            var result = MessageBox.Show($"Delete '{SelectedChapter.Title}'?", "Delete Chapter", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                string id = SelectedChapter.Id;
                await _databaseService.DeleteChapterAsync(id);
                Chapters.Remove(SelectedChapter);
                SelectedChapter = Chapters.FirstOrDefault();
            }
        }

        [RelayCommand]
        public async Task SaveCurrentChapterAsync()
        {
            if (SelectedChapter == null || SelectedProject == null) return;

            SelectedChapter.Content = ActiveChapterContent;
            await _databaseService.SaveChapterAsync(SelectedChapter);
            await _databaseService.SaveProjectAsync(SelectedProject);
            GenerationStatus = "Saved at " + DateTime.Now.ToString("HH:mm:ss");
        }

        [RelayCommand]
        public async Task ExecuteAiActionAsync(string actionType)
        {
            if (SelectedChapter == null || SelectedProject == null || IsGenerating) return;

            IsGenerating = true;
            GenerationStatus = $"🧠 AI Author thinking ({actionType} in {SelectedPerspective})...";
            AiSuggestionText = "";
            _generationCts = new CancellationTokenSource();

            try
            {
                var messages = await _contextBuilder.BuildNovelInstructionMessagesAsync(
                    SelectedChapter,
                    SelectedProject,
                    actionType,
                    SelectedPerspective,
                    PovFocus,
                    SelectedText,
                    CustomInstructionText);

                var stringBuilder = new StringBuilder();
                var lastUiUpdate = DateTime.UtcNow;

                await Task.Run(async () =>
                {
                    await foreach (var token in _llmService.StreamChatCompletionAsync(
                        messages,
                        temperature: 1.02,
                        minP: 0.07,
                        topP: 0.95,
                        repetitionPenalty: 1.08,
                        dryMultiplier: 0.8,
                        maxTokens: 1024,
                        cancellationToken: _generationCts.Token))
                    {
                        stringBuilder.Append(token);

                        if ((DateTime.UtcNow - lastUiUpdate).TotalMilliseconds > 50)
                        {
                            lastUiUpdate = DateTime.UtcNow;
                            string currentDraft = stringBuilder.ToString();
                            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                AiSuggestionText = currentDraft;
                                GenerationStatus = "✍️ Writing prose...";
                            }), System.Windows.Threading.DispatcherPriority.Background);
                        }
                    }
                });

                // Sanitize output to strip any dataset hallucination artifacts
                AiSuggestionText = CleanNovelOutput(stringBuilder.ToString());
                GenerationStatus = "Generation Complete";
            }
            catch (OperationCanceledException)
            {
                GenerationStatus = "Cancelled";
            }
            catch (Exception ex)
            {
                GenerationStatus = $"Error: {ex.Message}";
            }
            finally
            {
                IsGenerating = false;
            }
        }

        private static string CleanNovelOutput(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";

            // Cut off at any meta delimiters
            string[] cutOffs = new[]
            {
                "[/Generated Text]",
                "End Output.",
                "Sincerely,\nClaude",
                "Sincerely,",
                "I appreciate your thoughtful and thorough response",
                "Thank you for choosing to work with me",
                "[Response]:",
                "Dear [Your Name]"
            };

            foreach (var cut in cutOffs)
            {
                int idx = text.IndexOf(cut, StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    text = text.Substring(0, idx);
                }
            }

            return text.Trim();
        }

        [RelayCommand]
        public void StopGeneration()
        {
            _generationCts?.Cancel();
            IsGenerating = false;
            GenerationStatus = "Stopped by user";
        }

        [RelayCommand]
        public void InsertSuggestionAtEnd()
        {
            if (string.IsNullOrWhiteSpace(AiSuggestionText)) return;

            if (string.IsNullOrWhiteSpace(ActiveChapterContent))
            {
                ActiveChapterContent = AiSuggestionText.Trim();
            }
            else
            {
                ActiveChapterContent = ActiveChapterContent.TrimEnd() + "\n\n" + AiSuggestionText.Trim();
            }

            AiSuggestionText = "";
            _ = SaveCurrentChapterAsync();
        }

        [RelayCommand]
        public void ReplaceSelectionWithSuggestion()
        {
            if (string.IsNullOrWhiteSpace(AiSuggestionText)) return;

            if (!string.IsNullOrEmpty(SelectedText) && ActiveChapterContent.Contains(SelectedText))
            {
                ActiveChapterContent = ActiveChapterContent.Replace(SelectedText, AiSuggestionText.Trim());
            }
            else
            {
                InsertSuggestionAtEnd();
            }

            AiSuggestionText = "";
            _ = SaveCurrentChapterAsync();
        }

        [RelayCommand]
        public async Task ExportChapterMarkdownAsync()
        {
            if (SelectedProject == null || SelectedChapter == null) return;
            string path = await _exportService.ExportChapterToMarkdownAsync(SelectedProject, SelectedChapter);
            MessageBox.Show($"Chapter exported to:\n{path}", "Export Successful", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        [RelayCommand]
        public async Task ExportEntireNovelMarkdownAsync()
        {
            if (SelectedProject == null || Chapters.Count == 0) return;
            string path = await _exportService.ExportEntireNovelToMarkdownAsync(SelectedProject, Chapters.ToList());
            MessageBox.Show($"Entire Novel exported to:\n{path}", "Novel Exported", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void UpdateTriggeredContext()
        {
            try
            {
                string text = ActiveChapterContent ?? "";
                string? projId = SelectedProject?.Id;

                var (chars, lore) = await Task.Run(async () =>
                {
                    var allChars = await _databaseService.GetCharacterProfilesAsync(projId);
                    var trigChars = _contextBuilder.GetTriggeredCharacters(allChars.Where(c => c.IsEnabled).ToList(), text);
                    var allLore = await _databaseService.GetLoreEntriesAsync();
                    var trigLore = _contextBuilder.GetTriggeredLoreEntries(allLore.Where(l => l.IsEnabled).ToList(), text);
                    return (trigChars, trigLore);
                });

                TriggeredCharacters = new ObservableCollection<CharacterProfile>(chars);
                TriggeredLoreEntries = new ObservableCollection<LoreEntry>(lore);
            }
            catch
            {
            }
        }
    }
}
