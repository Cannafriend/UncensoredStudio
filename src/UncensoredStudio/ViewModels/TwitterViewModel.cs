using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UncensoredStudio.Models;
using UncensoredStudio.Services;

namespace UncensoredStudio.ViewModels
{
    public partial class TwitterViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;
        private readonly LLMService _llmService;
        private readonly ContextBuilderService _contextBuilder;
        private readonly TwitterService _twitterService;

        private CancellationTokenSource? _generationCts;

        [ObservableProperty]
        private ObservableCollection<TwitterDraft> _drafts = new();

        [ObservableProperty]
        private TwitterDraft? _selectedDraft;

        [ObservableProperty]
        private string _selectedMode = "Micro-Smut"; // Micro-Smut, Teaser, Caption, Thread

        public ObservableCollection<string> AvailableModes { get; } = new()
        {
            "Micro-Smut",
            "Teaser",
            "Caption",
            "Thread"
        };

        [ObservableProperty]
        private string _selectedTone = "Sultry / Teasing";

        public ObservableCollection<string> AvailableTones { get; } = new()
        {
            "Sultry / Teasing",
            "Dominant / Seductive",
            "Slow-Burn Romance",
            "Dark / Possessive",
            "Playful & Flirty",
            "High Tension Cliffhanger"
        };

        [ObservableProperty]
        private string _inputPrompt = "";

        [ObservableProperty]
        private ObservableCollection<TweetCard> _tweetCards = new();

        [ObservableProperty]
        private bool _isGenerating = false;

        [ObservableProperty]
        private string _statusText = "Ready to generate spicy posts";

        public TwitterViewModel(
            DatabaseService databaseService,
            LLMService llmService,
            ContextBuilderService contextBuilder,
            TwitterService twitterService)
        {
            _databaseService = databaseService;
            _llmService = llmService;
            _contextBuilder = contextBuilder;
            _twitterService = twitterService;
        }

        public async Task InitializeAsync()
        {
            var drafts = await _databaseService.GetTwitterDraftsAsync();
            Drafts = new ObservableCollection<TwitterDraft>(drafts);
            if (Drafts.Count > 0)
            {
                SelectedDraft = Drafts[0];
            }
        }

        partial void OnSelectedDraftChanged(TwitterDraft? value)
        {
            if (value != null)
            {
                InputPrompt = value.RawContent ?? "";
                SelectedMode = value.Category ?? "Micro-Smut";
                if (!string.IsNullOrEmpty(value.TweetsJson))
                {
                    try
                    {
                        var list = JsonSerializer.Deserialize<System.Collections.Generic.List<TweetCard>>(value.TweetsJson);
                        if (list != null)
                        {
                            TweetCards = new ObservableCollection<TweetCard>(list);
                        }
                    }
                    catch
                    {
                        TweetCards = new ObservableCollection<TweetCard>(_twitterService.SplitIntoThread(value.RawContent));
                    }
                }
            }
        }

        [RelayCommand]
        public async Task GeneratePostsAsync()
        {
            if (string.IsNullOrWhiteSpace(InputPrompt) || IsGenerating) return;

            IsGenerating = true;
            StatusText = $"Generating {SelectedMode} ({SelectedTone})...";
            TweetCards.Clear();
            _generationCts = new CancellationTokenSource();

            try
            {
                string prompt = await _contextBuilder.BuildTwitterSmutPromptAsync(
                    SelectedMode,
                    InputPrompt,
                    SelectedTone);

                var sb = new StringBuilder();
                await foreach (var token in _llmService.StreamTextCompletionAsync(
                    prompt,
                    temperature: 1.12,
                    minP: 0.06,
                    topP: 0.95,
                    repetitionPenalty: 1.12,
                    dryMultiplier: 0.85,
                    maxTokens: 512,
                    _generationCts.Token))
                {
                    sb.Append(token);
                }

                string rawResult = sb.ToString().Trim();
                var parsedCards = _twitterService.SplitIntoThread(rawResult);
                TweetCards = new ObservableCollection<TweetCard>(parsedCards);

                StatusText = $"Generated {TweetCards.Count} tweet(s)";
            }
            catch (OperationCanceledException)
            {
                StatusText = "Cancelled";
            }
            catch (Exception ex)
            {
                StatusText = $"Error: {ex.Message}";
            }
            finally
            {
                IsGenerating = false;
            }
        }

        [RelayCommand]
        public void StopGeneration()
        {
            _generationCts?.Cancel();
            IsGenerating = false;
            StatusText = "Stopped";
        }

        [RelayCommand]
        public void CopyTweet(TweetCard? card)
        {
            if (card != null && !string.IsNullOrEmpty(card.Text))
            {
                Clipboard.SetText(card.Text);
                StatusText = $"Copied Tweet #{card.Index} to clipboard!";
            }
        }

        [RelayCommand]
        public void CopyFullThread()
        {
            if (TweetCards.Count == 0) return;

            string fullText = _twitterService.FormatCompleteThreadForCopy(TweetCards.ToList());
            Clipboard.SetText(fullText);
            StatusText = "Copied complete thread to clipboard!";
        }

        [RelayCommand]
        public async Task SaveDraftAsync()
        {
            if (TweetCards.Count == 0 && string.IsNullOrWhiteSpace(InputPrompt)) return;

            string title = InputPrompt.Length > 30 ? InputPrompt.Substring(0, 30) + "..." : InputPrompt;
            if (string.IsNullOrWhiteSpace(title)) title = $"{SelectedMode} Post";

            var draft = new TwitterDraft
            {
                Title = title,
                Category = SelectedMode,
                RawContent = InputPrompt,
                TweetsJson = JsonSerializer.Serialize(TweetCards.ToList())
            };

            await _databaseService.SaveTwitterDraftAsync(draft);
            Drafts.Insert(0, draft);
            SelectedDraft = draft;
            StatusText = "Saved draft to library";
        }

        [RelayCommand]
        public async Task DeleteDraftAsync()
        {
            if (SelectedDraft == null) return;

            await _databaseService.DeleteTwitterDraftAsync(SelectedDraft.Id);
            Drafts.Remove(SelectedDraft);
            SelectedDraft = Drafts.FirstOrDefault();
            StatusText = "Draft deleted";
        }
    }
}
