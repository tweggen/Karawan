using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Aihao.Models;
using Aihao.Services;
using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

// Alias our KeyBinding to avoid conflict with Avalonia's
using KeyBindingModel = Aihao.Models.KeyBinding;
using KeyModifiersModel = Aihao.Models.KeyModifiers;

namespace Aihao.ViewModels;

/// <summary>
/// View model for a single action in the keybindings editor.
/// </summary>
public class KeyBindingItemVM : INotifyPropertyChanged
{
    private readonly ActionDefinition _action;
    private KeyBindingModel? _currentBinding;
    private KeyBindingModel? _originalBinding;
    private bool _isModified;
    private bool _isRecording;
    
    public event PropertyChangedEventHandler? PropertyChanged;
    
    public string ActionId => _action.Id;
    public string DisplayName => _action.DisplayName;
    public string Description => _action.Description;
    public string Category => _action.Category;
    public string? Icon => _action.Icon;
    public KeyBindingModel? DefaultBinding => _action.DefaultKeyBinding;
    public bool RequiresProject => _action.RequiresProject;
    
    public KeyBindingModel? CurrentBinding
    {
        get => _currentBinding;
        set
        {
            if (!Equals(_currentBinding, value))
            {
                _currentBinding = value;
                OnPropertyChanged(nameof(CurrentBinding));
                OnPropertyChanged(nameof(CurrentBindingDisplay));
                OnPropertyChanged(nameof(HasBinding));
                IsModified = !Equals(value, _originalBinding);
            }
        }
    }
    
    public string CurrentBindingDisplay => _currentBinding?.ToString() ?? "(none)";
    
    public bool HasBinding => _currentBinding != null;
    
    public bool IsModified
    {
        get => _isModified;
        private set
        {
            if (_isModified != value)
            {
                _isModified = value;
                OnPropertyChanged(nameof(IsModified));
            }
        }
    }
    
    public bool IsRecording
    {
        get => _isRecording;
        set
        {
            if (_isRecording != value)
            {
                _isRecording = value;
                OnPropertyChanged(nameof(IsRecording));
            }
        }
    }
    
    public KeyBindingItemVM(ActionDefinition action, KeyBindingModel? currentBinding)
    {
        _action = action;
        _currentBinding = currentBinding;
        _originalBinding = currentBinding;
    }
    
    public void SetBindingWithoutModified(KeyBindingModel? binding)
    {
        _currentBinding = binding;
        _originalBinding = binding;
        _isModified = false;
        OnPropertyChanged(nameof(CurrentBinding));
        OnPropertyChanged(nameof(CurrentBindingDisplay));
        OnPropertyChanged(nameof(HasBinding));
        OnPropertyChanged(nameof(IsModified));
    }
    
    public void ResetToDefault()
    {
        CurrentBinding = _action.DefaultKeyBinding;
    }
    
    public void ClearBinding()
    {
        CurrentBinding = null;
    }
    
    public void MarkAsSaved()
    {
        _originalBinding = _currentBinding;
        IsModified = false;
    }
    
    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// View model for the keybindings editor dialog.
/// </summary>
public partial class KeyBindingsEditorViewModel : ObservableObject
{
    private readonly ActionService _actionService;
    private readonly UserSettingsService _settingsService;
    private Dictionary<string, KeyBindingItemVM> _itemsByActionId = new();
    
    public ObservableCollection<KeyBindingItemVM> AllBindings { get; } = new();
    public ObservableCollection<KeyBindingItemVM> FilteredBindings { get; } = new();
    public ObservableCollection<string> Categories { get; } = new();
    
    [ObservableProperty]
    private string _searchText = string.Empty;
    
    [ObservableProperty]
    private string? _selectedCategory;
    
    [ObservableProperty]
    private KeyBindingItemVM? _selectedItem;
    
    [ObservableProperty]
    private KeyBindingItemVM? _recordingItem;
    
    [ObservableProperty]
    private bool _hasChanges;
    
    [ObservableProperty]
    private string? _conflictWarning;
    
    /// <summary>
    /// Event raised when dialog should close.
    /// </summary>
    public event EventHandler<bool>? RequestClose;
    
    public KeyBindingsEditorViewModel(ActionService actionService, UserSettingsService settingsService)
    {
        _actionService = actionService;
        _settingsService = settingsService;
        
        LoadBindings();
    }
    
    private void LoadBindings()
    {
        AllBindings.Clear();
        _itemsByActionId.Clear();
        Categories.Clear();
        Categories.Add("All");
        
        var categories = new HashSet<string>();
        
        foreach (var action in _actionService.Actions.Values.OrderBy(a => a.Category).ThenBy(a => a.DisplayName))
        {
            var binding = _actionService.GetKeyBinding(action.Id);
            var item = new KeyBindingItemVM(action, binding);
            item.PropertyChanged += OnItemPropertyChanged;
            
            AllBindings.Add(item);
            _itemsByActionId[action.Id] = item;
            categories.Add(action.Category);
        }
        
        foreach (var category in categories.OrderBy(c => c))
        {
            Categories.Add(category);
        }
        
        SelectedCategory = "All";
        ApplyFilter();
    }
    
    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(KeyBindingItemVM.IsModified))
        {
            HasChanges = AllBindings.Any(b => b.IsModified);
        }
        else if (e.PropertyName == nameof(KeyBindingItemVM.CurrentBinding))
        {
            CheckForConflicts();
        }
    }
    
    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }
    
    partial void OnSelectedCategoryChanged(string? value)
    {
        ApplyFilter();
    }
    
    private void ApplyFilter()
    {
        FilteredBindings.Clear();
        
        var query = SearchText?.ToLowerInvariant() ?? string.Empty;
        
        foreach (var item in AllBindings)
        {
            // Filter by category
            if (!string.IsNullOrEmpty(SelectedCategory) && 
                SelectedCategory != "All" && 
                item.Category != SelectedCategory)
            {
                continue;
            }
            
            // Filter by search text
            if (!string.IsNullOrEmpty(query))
            {
                if (!item.DisplayName.ToLowerInvariant().Contains(query) &&
                    !item.ActionId.ToLowerInvariant().Contains(query) &&
                    !(item.CurrentBindingDisplay?.ToLowerInvariant().Contains(query) ?? false))
                {
                    continue;
                }
            }
            
            FilteredBindings.Add(item);
        }
    }
    
    private void CheckForConflicts()
    {
        ConflictWarning = null;
        
        // Check for duplicate bindings
        var bindingToActions = new Dictionary<string, List<string>>();
        
        foreach (var item in AllBindings)
        {
            if (item.CurrentBinding == null) continue;
            
            var key = item.CurrentBinding.ToGestureString().ToLowerInvariant();
            if (!bindingToActions.ContainsKey(key))
            {
                bindingToActions[key] = new List<string>();
            }
            bindingToActions[key].Add(item.DisplayName);
        }
        
        var conflicts = bindingToActions.Where(kvp => kvp.Value.Count > 1).ToList();
        if (conflicts.Any())
        {
            var first = conflicts.First();
            ConflictWarning = $"⚠️ Conflict: {first.Key} is bound to: {string.Join(", ", first.Value)}";
        }
    }
    
    /// <summary>
    /// Start recording a new keybinding for the selected item.
    /// </summary>
    [RelayCommand]
    private void StartRecording(KeyBindingItemVM? item)
    {
        if (item == null) return;
        
        // Stop any current recording
        if (RecordingItem != null)
        {
            RecordingItem.IsRecording = false;
        }
        
        RecordingItem = item;
        item.IsRecording = true;
    }
    
    /// <summary>
    /// Stop recording without changing the binding.
    /// </summary>
    [RelayCommand]
    private void CancelRecording()
    {
        if (RecordingItem != null)
        {
            RecordingItem.IsRecording = false;
            RecordingItem = null;
        }
    }
    
    /// <summary>
    /// Handle a key press during recording.
    /// </summary>
    public void RecordKeyPress(Key key, Avalonia.Input.KeyModifiers modifiers)
    {
        if (RecordingItem == null) return;
        
        // Ignore modifier-only presses
        if (key == Key.LeftCtrl || key == Key.RightCtrl ||
            key == Key.LeftShift || key == Key.RightShift ||
            key == Key.LeftAlt || key == Key.RightAlt ||
            key == Key.LWin || key == Key.RWin)
        {
            return;
        }
        
        // Escape cancels recording
        if (key == Key.Escape && modifiers == Avalonia.Input.KeyModifiers.None)
        {
            CancelRecording();
            return;
        }
        
        // Convert Avalonia modifiers to our modifiers
        var ourModifiers = KeyModifiersModel.None;
        if (modifiers.HasFlag(Avalonia.Input.KeyModifiers.Control))
            ourModifiers |= KeyModifiersModel.Control;
        if (modifiers.HasFlag(Avalonia.Input.KeyModifiers.Shift))
            ourModifiers |= KeyModifiersModel.Shift;
        if (modifiers.HasFlag(Avalonia.Input.KeyModifiers.Alt))
            ourModifiers |= KeyModifiersModel.Alt;
        if (modifiers.HasFlag(Avalonia.Input.KeyModifiers.Meta))
            ourModifiers |= KeyModifiersModel.Meta;
        
        RecordingItem.CurrentBinding = new KeyBindingModel
        {
            Key = key.ToString(),
            Modifiers = ourModifiers
        };
        
        RecordingItem.IsRecording = false;
        RecordingItem = null;
    }
    
    /// <summary>
    /// Clear the keybinding for an item.
    /// </summary>
    [RelayCommand]
    private void ClearBinding(KeyBindingItemVM? item)
    {
        item?.ClearBinding();
    }
    
    /// <summary>
    /// Reset an item to its default binding.
    /// </summary>
    [RelayCommand]
    private void ResetBinding(KeyBindingItemVM? item)
    {
        item?.ResetToDefault();
    }
    
    /// <summary>
    /// Reset all bindings to defaults.
    /// </summary>
    [RelayCommand]
    private void ResetAllBindings()
    {
        foreach (var item in AllBindings)
        {
            item.ResetToDefault();
        }
    }
    
    /// <summary>
    /// Save changes and close.
    /// </summary>
    [RelayCommand]
    private async Task Save()
    {
        // Build overrides list
        var overrides = new List<KeyBindingOverride>();
        
        foreach (var item in AllBindings)
        {
            // Check if different from default
            var defaultBinding = item.DefaultBinding;
            var currentBinding = item.CurrentBinding;
            
            if (currentBinding == null && defaultBinding != null)
            {
                // Disabled
                overrides.Add(new KeyBindingOverride
                {
                    ActionId = item.ActionId,
                    Disabled = true
                });
            }
            else if (currentBinding != null && !currentBinding.Equals(defaultBinding))
            {
                // Custom binding
                overrides.Add(new KeyBindingOverride
                {
                    ActionId = item.ActionId,
                    KeyBinding = currentBinding.ToGestureString()
                });
            }
            
            item.MarkAsSaved();
        }
        
        // Apply to action service
        _actionService.ApplyOverrides(overrides);
        
        // Save to settings
        _settingsService.Settings.KeyBindingOverrides = overrides;
        await _settingsService.SaveAsync();
        
        HasChanges = false;
        RequestClose?.Invoke(this, true);
    }
    
    /// <summary>
    /// Cancel and close.
    /// </summary>
    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke(this, false);
    }
}
