using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Aihao.Models;
using Aihao.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aihao.ViewModels;

/// <summary>
/// Represents an item in the command palette list.
/// </summary>
public partial class CommandPaletteItemVM : ObservableObject
{
    public string Id { get; }
    public string DisplayName { get; }
    public string? Description { get; }
    public string Category { get; }
    public string? Icon { get; }
    public string? KeyBindingDisplay { get; }
    public Func<Task>? Action { get; }
    
    [ObservableProperty]
    private bool _isSelected;
    
    public CommandPaletteItemVM(QuickOpenItem item, string? keyBindingDisplay = null)
    {
        Id = item.Id;
        DisplayName = item.DisplayName;
        Description = item.Description;
        Category = item.Category;
        Icon = item.Icon;
        KeyBindingDisplay = keyBindingDisplay;
        Action = item.Action;
    }
    
    public CommandPaletteItemVM(ActionDefinition action, string? keyBindingDisplay, Func<Task> handler)
    {
        Id = action.Id;
        DisplayName = action.DisplayName;
        Description = action.Description;
        Category = action.Category;
        Icon = action.Icon;
        KeyBindingDisplay = keyBindingDisplay;
        Action = handler;
    }
}

/// <summary>
/// View model for the unified command palette.
/// </summary>
public partial class CommandPaletteViewModel : ObservableObject
{
    private readonly ActionService _actionService;
    private readonly List<QuickOpenItem> _quickOpenItems;
    private readonly Func<string, bool>? _canExecuteAction;
    private readonly bool _isProjectLoaded;
    
    private List<CommandPaletteItemVM> _allItems = new();
    
    public ObservableCollection<CommandPaletteItemVM> FilteredItems { get; } = new();
    
    [ObservableProperty]
    private string _searchText = string.Empty;
    
    [ObservableProperty]
    private CommandPaletteItemVM? _selectedItem;
    
    [ObservableProperty]
    private int _selectedIndex;
    
    [ObservableProperty]
    private CommandPaletteMode _mode;
    
    [ObservableProperty]
    private string _placeholder = "Search...";
    
    /// <summary>
    /// Event raised when an item is executed and the palette should close.
    /// </summary>
    public event EventHandler? RequestClose;
    
    public CommandPaletteViewModel(
        CommandPaletteMode mode,
        ActionService actionService,
        List<QuickOpenItem> quickOpenItems,
        bool isProjectLoaded,
        Func<string, bool>? canExecuteAction = null)
    {
        _mode = mode;
        _actionService = actionService;
        _quickOpenItems = quickOpenItems;
        _isProjectLoaded = isProjectLoaded;
        _canExecuteAction = canExecuteAction;
        
        Placeholder = mode == CommandPaletteMode.QuickOpen 
            ? "Go to..." 
            : "Run command...";
        
        LoadItems();
        ApplyFilter();
    }
    
    private void LoadItems()
    {
        _allItems.Clear();
        
        if (Mode == CommandPaletteMode.QuickOpen)
        {
            // Load quick open items
            foreach (var item in _quickOpenItems)
            {
                if (item.RequiresProject && !_isProjectLoaded)
                    continue;
                    
                _allItems.Add(new CommandPaletteItemVM(item));
            }
        }
        else
        {
            // Load actions from action service
            foreach (var action in _actionService.Actions.Values
                .Where(a => a.ShowInCommandPalette)
                .OrderBy(a => a.Category)
                .ThenBy(a => a.DisplayName))
            {
                if (action.RequiresProject && !_isProjectLoaded)
                    continue;
                
                // Check if action can execute
                if (_canExecuteAction != null && !_canExecuteAction(action.Id))
                    continue;
                
                var keyBinding = _actionService.GetKeyBinding(action.Id);
                var keyBindingDisplay = keyBinding?.ToString();
                
                Func<Task> handler = () => _actionService.ExecuteAsync(action.Id);
                
                _allItems.Add(new CommandPaletteItemVM(action, keyBindingDisplay, handler));
            }
        }
    }
    
    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }
    
    private void ApplyFilter()
    {
        FilteredItems.Clear();
        
        var query = SearchText?.ToLowerInvariant()?.Trim() ?? string.Empty;
        
        IEnumerable<CommandPaletteItemVM> results;
        
        if (string.IsNullOrEmpty(query))
        {
            // Show all items when no search
            results = _allItems;
        }
        else
        {
            // Score and sort by relevance
            results = _allItems
                .Select(item => new { Item = item, Score = CalculateMatchScore(item, query) })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Item.DisplayName)
                .Select(x => x.Item);
        }
        
        foreach (var item in results)
        {
            FilteredItems.Add(item);
        }
        
        // Select first item
        if (FilteredItems.Count > 0)
        {
            SelectItem(0);
        }
        else
        {
            SelectedItem = null;
            SelectedIndex = -1;
        }
    }
    
    private int CalculateMatchScore(CommandPaletteItemVM item, string query)
    {
        var score = 0;
        var lowerName = item.DisplayName.ToLowerInvariant();
        var lowerId = item.Id.ToLowerInvariant();
        var lowerCategory = item.Category.ToLowerInvariant();
        var lowerDesc = item.Description?.ToLowerInvariant() ?? string.Empty;
        
        // Exact match in name - highest score
        if (lowerName == query)
            return 1000;
        
        // Name starts with query
        if (lowerName.StartsWith(query))
            score += 100;
        
        // Name contains query
        if (lowerName.Contains(query))
            score += 50;
        
        // ID contains query
        if (lowerId.Contains(query))
            score += 30;
        
        // Category contains query
        if (lowerCategory.Contains(query))
            score += 20;
        
        // Description contains query
        if (lowerDesc.Contains(query))
            score += 10;
        
        // Fuzzy matching - all characters appear in order
        if (score == 0 && FuzzyMatch(lowerName, query))
            score += 5;
        
        return score;
    }
    
    private bool FuzzyMatch(string text, string pattern)
    {
        var patternIndex = 0;
        foreach (var c in text)
        {
            if (patternIndex < pattern.Length && c == pattern[patternIndex])
            {
                patternIndex++;
            }
        }
        return patternIndex == pattern.Length;
    }
    
    private void SelectItem(int index)
    {
        // Deselect previous
        if (SelectedItem != null)
        {
            SelectedItem.IsSelected = false;
        }
        
        if (index >= 0 && index < FilteredItems.Count)
        {
            SelectedIndex = index;
            SelectedItem = FilteredItems[index];
            SelectedItem.IsSelected = true;
        }
        else
        {
            SelectedIndex = -1;
            SelectedItem = null;
        }
    }
    
    /// <summary>
    /// Move selection up.
    /// </summary>
    [RelayCommand]
    public void SelectPrevious()
    {
        if (FilteredItems.Count == 0) return;
        
        var newIndex = SelectedIndex - 1;
        if (newIndex < 0)
            newIndex = FilteredItems.Count - 1;
        
        SelectItem(newIndex);
    }
    
    /// <summary>
    /// Move selection down.
    /// </summary>
    [RelayCommand]
    public void SelectNext()
    {
        if (FilteredItems.Count == 0) return;
        
        var newIndex = SelectedIndex + 1;
        if (newIndex >= FilteredItems.Count)
            newIndex = 0;
        
        SelectItem(newIndex);
    }
    
    /// <summary>
    /// Execute the selected item.
    /// </summary>
    [RelayCommand]
    public async Task ExecuteSelected()
    {
        if (SelectedItem?.Action != null)
        {
            RequestClose?.Invoke(this, EventArgs.Empty);
            await SelectedItem.Action();
        }
    }
    
    /// <summary>
    /// Execute a specific item.
    /// </summary>
    [RelayCommand]
    public async Task ExecuteItem(CommandPaletteItemVM? item)
    {
        if (item?.Action != null)
        {
            RequestClose?.Invoke(this, EventArgs.Empty);
            await item.Action();
        }
    }
    
    /// <summary>
    /// Cancel and close.
    /// </summary>
    [RelayCommand]
    public void Cancel()
    {
        RequestClose?.Invoke(this, EventArgs.Empty);
    }
}
