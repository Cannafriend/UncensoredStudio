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
    public partial class LorebookViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;
        private readonly ContextBuilderService _contextBuilder;

        [ObservableProperty]
        private ObservableCollection<LoreEntry> _allEntries = new();

        [ObservableProperty]
        private ObservableCollection<LoreEntry> _filteredEntries = new();

        [ObservableProperty]
        private LoreEntry? _selectedEntry;

        [ObservableProperty]
        private string _selectedCategoryFilter = "All";

        public ObservableCollection<string> Categories { get; } = new()
        {
            "All",
            "Character",
            "Location",
            "Trope/Kink",
            "Lore",
            "Dynamic"
        };

        [ObservableProperty]
        private string _searchQuery = "";

        // Sandbox Tester
        [ObservableProperty]
        private string _testInputText = "";

        [ObservableProperty]
        private ObservableCollection<LoreEntry> _testTriggeredEntries = new();

        public LorebookViewModel(
            DatabaseService databaseService,
            ContextBuilderService contextBuilder)
        {
            _databaseService = databaseService;
            _contextBuilder = contextBuilder;
        }

        public async Task InitializeAsync()
        {
            var entries = await _databaseService.GetLoreEntriesAsync();
            AllEntries = new ObservableCollection<LoreEntry>(entries);
            ApplyFilter();
            if (FilteredEntries.Count > 0)
            {
                SelectedEntry = FilteredEntries[0];
            }
        }

        partial void OnSelectedCategoryFilterChanged(string value) => ApplyFilter();
        partial void OnSearchQueryChanged(string value) => ApplyFilter();

        partial void OnTestInputTextChanged(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                TestTriggeredEntries.Clear();
                return;
            }

            var enabled = AllEntries.Where(e => e.IsEnabled).ToList();
            var triggered = _contextBuilder.GetTriggeredLoreEntries(enabled, value);
            TestTriggeredEntries = new ObservableCollection<LoreEntry>(triggered);
        }

        private void ApplyFilter()
        {
            var filtered = AllEntries.AsEnumerable();

            if (SelectedCategoryFilter != "All")
            {
                filtered = filtered.Where(e => e.Category.Equals(SelectedCategoryFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                string q = SearchQuery.ToLowerInvariant();
                filtered = filtered.Where(e => e.Title.ToLowerInvariant().Contains(q) || e.Keys.ToLowerInvariant().Contains(q) || e.Content.ToLowerInvariant().Contains(q));
            }

            FilteredEntries = new ObservableCollection<LoreEntry>(filtered.ToList());
        }

        [RelayCommand]
        public async Task CreateNewEntryAsync()
        {
            var newEntry = new LoreEntry
            {
                Title = "New Lore Entity",
                Category = SelectedCategoryFilter == "All" ? "Character" : SelectedCategoryFilter,
                Keys = "name, alias, nickname",
                Content = "Detailed physical description, psychological traits, secrets, kinks, and behavioral rules...",
                IsEnabled = true,
                InsertionOrder = 100
            };

            await _databaseService.SaveLoreEntryAsync(newEntry);
            AllEntries.Insert(0, newEntry);
            ApplyFilter();
            SelectedEntry = newEntry;
        }

        [RelayCommand]
        public async Task SaveSelectedEntryAsync()
        {
            if (SelectedEntry == null) return;
            await _databaseService.SaveLoreEntryAsync(SelectedEntry);
            MessageBox.Show("Lore entry saved successfully!", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        [RelayCommand]
        public async Task DeleteSelectedEntryAsync()
        {
            if (SelectedEntry == null) return;

            var result = MessageBox.Show($"Delete lore entry '{SelectedEntry.Title}'?", "Delete Lore", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                await _databaseService.DeleteLoreEntryAsync(SelectedEntry.Id);
                AllEntries.Remove(SelectedEntry);
                ApplyFilter();
                SelectedEntry = FilteredEntries.FirstOrDefault();
            }
        }
    }
}
