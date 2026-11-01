using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UncensoredStudio.Models;
using UncensoredStudio.Services;

namespace UncensoredStudio.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;
        private readonly BackendProcessService _backendService;

        public event Action<string>? FontFamilyChanged;
        public event Action? MemoryCleared;

        [ObservableProperty]
        private AppSettings _settings = new();

        [ObservableProperty]
        private ObservableCollection<FontOption> _availableFonts = new();

        [ObservableProperty]
        private FontOption? _selectedFont;

        [ObservableProperty]
        private ObservableCollection<Project> _availableProjects = new();

        [ObservableProperty]
        private Project? _selectedProjectToClear;

        [ObservableProperty]
        private string _statusMessage = "Studio Settings Ready";

        public SettingsViewModel(
            DatabaseService databaseService,
            BackendProcessService backendService)
        {
            _databaseService = databaseService;
            _backendService = backendService;

            InitializeFonts();
        }

        private void InitializeFonts()
        {
            AvailableFonts = new ObservableCollection<FontOption>
            {
                new FontOption
                {
                    DisplayName = "Segoe UI (Default Clean)",
                    FontFamilyValue = "Segoe UI, sans-serif",
                    Category = "System Sans"
                },
                new FontOption
                {
                    DisplayName = "Archivo Black (Bold Impact)",
                    FontFamilyValue = "pack://application:,,,/Assets/Fonts/#Archivo Black, Archivo Black, sans-serif",
                    Category = "Google Fonts"
                },
                new FontOption
                {
                    DisplayName = "Averia Sans Libre (Organic Soft)",
                    FontFamilyValue = "pack://application:,,,/Assets/Fonts/#Averia Sans Libre, Averia Sans Libre, sans-serif",
                    Category = "Google Fonts"
                },
                new FontOption
                {
                    DisplayName = "Boogaloo (Retro Playful)",
                    FontFamilyValue = "pack://application:,,,/Assets/Fonts/#Boogaloo, Boogaloo, cursive",
                    Category = "Google Fonts"
                },
                new FontOption
                {
                    DisplayName = "Carter One (Pulp Fiction)",
                    FontFamilyValue = "pack://application:,,,/Assets/Fonts/#Carter One, Carter One, sans-serif",
                    Category = "Google Fonts"
                },
                new FontOption
                {
                    DisplayName = "DynaPuff (Bubbly Casual)",
                    FontFamilyValue = "pack://application:,,,/Assets/Fonts/#DynaPuff, DynaPuff, cursive",
                    Category = "Google Fonts"
                },
                new FontOption
                {
                    DisplayName = "Iceland (Cyberpunk Techno)",
                    FontFamilyValue = "pack://application:,,,/Assets/Fonts/#Iceland, Iceland, monospace",
                    Category = "Google Fonts"
                },
                new FontOption
                {
                    DisplayName = "Kaushan Script (Sensual Handwritten)",
                    FontFamilyValue = "pack://application:,,,/Assets/Fonts/#Kaushan Script, Kaushan Script, cursive",
                    Category = "Google Fonts"
                },
                new FontOption
                {
                    DisplayName = "Georgia (Literary Serif)",
                    FontFamilyValue = "Georgia, serif",
                    Category = "System Serif"
                },
                new FontOption
                {
                    DisplayName = "Consolas (Terminal Monospace)",
                    FontFamilyValue = "Consolas, monospace",
                    Category = "System Mono"
                }
            };
        }

        public async Task InitializeAsync()
        {
            Settings = await _databaseService.GetSettingsAsync();

            var match = AvailableFonts.FirstOrDefault(f => f.FontFamilyValue == Settings.SelectedFontFamily)
                        ?? AvailableFonts[0];
            SelectedFont = match;

            var projs = await _databaseService.GetProjectsAsync();
            AvailableProjects = new ObservableCollection<Project>(projs);
            if (AvailableProjects.Count > 0)
            {
                SelectedProjectToClear = AvailableProjects[0];
            }
        }

        partial void OnSelectedFontChanged(FontOption? value)
        {
            if (value == null) return;
            Settings.SelectedFontFamily = value.FontFamilyValue;
            _ = _databaseService.SaveSettingsAsync(Settings);
            FontFamilyChanged?.Invoke(value.FontFamilyValue);
            StatusMessage = $"Font changed to {value.DisplayName}";
        }

        [RelayCommand]
        public async Task ClearAllMemoryAsync()
        {
            var res = MessageBox.Show(
                "⚠️ DANGER: Are you sure you want to clear ALL studio memory?\n\nThis will permanently delete:\n- All chat sessions & conversations\n- All character dossiers & lorebooks\n- All novel chapters & projects\n- All X/Twitter drafts\n\nThis cannot be undone!",
                "Clear Entire Studio Memory",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (res == MessageBoxResult.Yes)
            {
                await _databaseService.ClearAllStudioMemoryAsync();
                StatusMessage = "All memory permanently wiped.";
                MemoryCleared?.Invoke();
                MessageBox.Show("All memory has been completely wiped clean.", "Memory Cleared", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        [RelayCommand]
        public async Task ClearStoryMemoryAsync()
        {
            if (SelectedProjectToClear == null)
            {
                MessageBox.Show("Please select a story project to clear.", "No Story Selected", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var res = MessageBox.Show(
                $"Are you sure you want to clear all memory and chapters for the story '{SelectedProjectToClear.Title}'?\n\nThis will delete all its chapters and attached character profiles.",
                "Clear Story Memory",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (res == MessageBoxResult.Yes)
            {
                await _databaseService.ClearStoryMemoryAsync(SelectedProjectToClear.Id);
                StatusMessage = $"Memory cleared for '{SelectedProjectToClear.Title}'";
                MemoryCleared?.Invoke();
                MessageBox.Show($"Memory cleared for '{SelectedProjectToClear.Title}'.", "Story Cleared", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        [RelayCommand]
        public async Task ClearAllChatHistoryAsync()
        {
            var res = MessageBox.Show(
                "Clear all chat message history across all sessions?\n(Your character profiles, stories, and lorebooks will be preserved)",
                "Clear Chat History",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (res == MessageBoxResult.Yes)
            {
                await _databaseService.ClearAllChatHistoryAsync();
                StatusMessage = "All chat history cleared.";
                MemoryCleared?.Invoke();
                MessageBox.Show("All chat message history has been wiped.", "Chat Cleared", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        [RelayCommand]
        public async Task ClearAllLoreAndCharactersAsync()
        {
            var res = MessageBox.Show(
                "Delete all Lorebook entries and Character Profiles?",
                "Clear World & Characters",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (res == MessageBoxResult.Yes)
            {
                await _databaseService.ClearAllLoreAndCharactersAsync();
                StatusMessage = "Lore and character dossiers cleared.";
                MemoryCleared?.Invoke();
                MessageBox.Show("All lore and character dossiers wiped.", "Memory Cleared", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        [RelayCommand]
        public async Task SaveHardwareSettingsAsync()
        {
            await _databaseService.SaveSettingsAsync(Settings);
            StatusMessage = "Hardware settings saved.";
            MessageBox.Show("Hardware configuration saved.", "Settings Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
