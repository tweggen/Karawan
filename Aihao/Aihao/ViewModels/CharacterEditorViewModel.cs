using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aihao.ViewModels;

/// <summary>
/// Represents a single character entry.
/// </summary>
public partial class CharacterViewModel : ObservableObject
{
    private readonly CharacterEditorViewModel _owner;

    [ObservableProperty]
    private string _id = string.Empty;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _portrait = string.Empty;

    [ObservableProperty]
    private string _entityTemplate = string.Empty;

    [ObservableProperty]
    private string _modelDescription = string.Empty;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isModified;

    /// <summary>
    /// The unified property editor for this character's custom properties.
    /// </summary>
    public JsonPropertyEditorViewModel PropertiesEditor { get; }

    /// <summary>
    /// True if there are no custom properties.
    /// </summary>
    public bool HasNoProperties => PropertiesEditor.RootNodes.Count == 0;

    /// <summary>
    /// Summary text for display in the list.
    /// </summary>
    public string Summary => string.IsNullOrEmpty(Description)
        ? "(no description)"
        : Description.Length > 40
            ? Description[..37] + "..."
            : Description;

    public CharacterViewModel(CharacterEditorViewModel owner)
    {
        _owner = owner;

        // Initialize the unified property editor
        PropertiesEditor = new JsonPropertyEditorViewModel
        {
            Title = "Properties",
            AllowTypeChange = true,
            AllowAddRemove = true
        };
        PropertiesEditor.Modified += (_, _) => OnModified();
        PropertiesEditor.RootNodes.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasNoProperties));
    }

    /// <summary>
    /// Load properties from a JsonObject.
    /// </summary>
    public void LoadProperties(JsonObject? propsObj)
    {
        if (propsObj != null)
        {
            PropertiesEditor.LoadFromJson(propsObj);
        }
        else
        {
            PropertiesEditor.RootNodes.Clear();
        }
        PropertiesEditor.ClearModifiedFlags();
    }

    [RelayCommand]
    private void AddProperty()
    {
        PropertiesEditor.AddPropertyCommand.Execute(null);
    }

    partial void OnIdChanged(string value) => OnModified();
    partial void OnDisplayNameChanged(string value)
    {
        OnModified();
        OnPropertyChanged(nameof(Summary));
    }
    partial void OnDescriptionChanged(string value)
    {
        OnModified();
        OnPropertyChanged(nameof(Summary));
    }
    partial void OnPortraitChanged(string value) => OnModified();
    partial void OnEntityTemplateChanged(string value) => OnModified();
    partial void OnModelDescriptionChanged(string value) => OnModified();

    private void OnModified()
    {
        IsModified = true;
        _owner.MarkDirty();
    }

    /// <summary>
    /// Convert this view model back to a JSON object.
    /// </summary>
    public JsonObject ToJsonObject()
    {
        var obj = new JsonObject
        {
            ["displayName"] = DisplayName
        };

        if (!string.IsNullOrEmpty(Description))
            obj["description"] = Description;

        if (!string.IsNullOrEmpty(Portrait))
            obj["portrait"] = Portrait;
        else
            obj["portrait"] = null;

        if (!string.IsNullOrEmpty(EntityTemplate))
            obj["entityTemplate"] = EntityTemplate;

        if (!string.IsNullOrEmpty(ModelDescription))
            obj["modelDescription"] = ModelDescription;

        // Properties from the unified editor
        if (PropertiesEditor.RootNodes.Count > 0)
        {
            obj["properties"] = PropertiesEditor.ToJsonObject();
        }
        else
        {
            obj["properties"] = new JsonObject();
        }

        return obj;
    }
}

/// <summary>
/// View model for the Characters editor.
/// Edits the /characters section of the Mix configuration.
/// </summary>
public partial class CharacterEditorViewModel : ObservableObject
{
    private JsonObject? _originalJson;

    [ObservableProperty]
    private string _title = "Characters";

    [ObservableProperty]
    private bool _isDirty;

    [ObservableProperty]
    private CharacterViewModel? _selectedCharacter;

    /// <summary>
    /// True if a character is selected.
    /// </summary>
    public bool HasSelectedCharacter => SelectedCharacter != null;

    /// <summary>
    /// True if no character is selected.
    /// </summary>
    public bool HasNoSelectedCharacter => SelectedCharacter == null;

    partial void OnSelectedCharacterChanged(CharacterViewModel? value)
    {
        OnPropertyChanged(nameof(HasSelectedCharacter));
        OnPropertyChanged(nameof(HasNoSelectedCharacter));
    }

    [ObservableProperty]
    private string _searchText = string.Empty;

    public ObservableCollection<CharacterViewModel> Characters { get; } = new();

    /// <summary>
    /// Filtered view of characters based on search.
    /// </summary>
    public IEnumerable<CharacterViewModel> FilteredCharacters
    {
        get
        {
            var query = Characters.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var search = SearchText.ToLowerInvariant();
                query = query.Where(c =>
                    c.Id.ToLowerInvariant().Contains(search) ||
                    c.DisplayName.ToLowerInvariant().Contains(search) ||
                    c.Description.ToLowerInvariant().Contains(search));
            }

            return query.OrderBy(c => c.Id);
        }
    }

    public void LoadFromJson(JsonObject charactersNode)
    {
        _originalJson = charactersNode;
        Characters.Clear();

        foreach (var property in charactersNode)
        {
            // Skip internal keys
            if (property.Key.StartsWith("__"))
                continue;

            if (property.Value is not JsonObject charObj)
                continue;

            var character = new CharacterViewModel(this)
            {
                Id = property.Key
            };

            ParseCharacterValue(character, charObj);
            Characters.Add(character);
        }

        IsDirty = false;
        OnPropertyChanged(nameof(FilteredCharacters));
    }

    private void ParseCharacterValue(CharacterViewModel character, JsonObject obj)
    {
        if (obj.TryGetPropertyValue("displayName", out var displayNameNode) &&
            displayNameNode is JsonValue displayNameVal &&
            displayNameVal.TryGetValue<string>(out var displayName))
        {
            character.DisplayName = displayName;
        }

        if (obj.TryGetPropertyValue("description", out var descNode) &&
            descNode is JsonValue descVal &&
            descVal.TryGetValue<string>(out var desc))
        {
            character.Description = desc;
        }

        if (obj.TryGetPropertyValue("portrait", out var portraitNode) &&
            portraitNode is JsonValue portraitVal &&
            portraitVal.TryGetValue<string>(out var portrait))
        {
            character.Portrait = portrait;
        }

        if (obj.TryGetPropertyValue("entityTemplate", out var entityNode) &&
            entityNode is JsonValue entityVal &&
            entityVal.TryGetValue<string>(out var entityTemplate))
        {
            character.EntityTemplate = entityTemplate;
        }

        if (obj.TryGetPropertyValue("modelDescription", out var modelNode) &&
            modelNode is JsonValue modelVal &&
            modelVal.TryGetValue<string>(out var modelDesc))
        {
            character.ModelDescription = modelDesc;
        }

        // Properties
        if (obj.TryGetPropertyValue("properties", out var propsNode) &&
            propsNode is JsonObject propsObj)
        {
            character.LoadProperties(propsObj);
        }

        character.IsModified = false;
    }

    public void MarkDirty()
    {
        IsDirty = true;
    }

    partial void OnSearchTextChanged(string value)
    {
        OnPropertyChanged(nameof(FilteredCharacters));
    }

    [RelayCommand]
    private void AddCharacter()
    {
        var newChar = new CharacterViewModel(this)
        {
            Id = "newCharacter",
            DisplayName = "New Character",
            IsModified = true
        };

        Characters.Add(newChar);
        SelectedCharacter = newChar;
        IsDirty = true;
        OnPropertyChanged(nameof(FilteredCharacters));
    }

    [RelayCommand]
    private void RemoveCharacter(CharacterViewModel? character)
    {
        if (character == null) return;

        Characters.Remove(character);
        if (SelectedCharacter == character)
        {
            SelectedCharacter = null;
        }
        IsDirty = true;
        OnPropertyChanged(nameof(FilteredCharacters));
    }

    [RelayCommand]
    private void DuplicateCharacter(CharacterViewModel? character)
    {
        if (character == null) return;

        var duplicate = new CharacterViewModel(this)
        {
            Id = character.Id + "_copy",
            DisplayName = character.DisplayName + " (Copy)",
            Description = character.Description,
            Portrait = character.Portrait,
            EntityTemplate = character.EntityTemplate,
            ModelDescription = character.ModelDescription,
            IsModified = true
        };

        // Copy properties by serializing and deserializing
        if (character.PropertiesEditor.RootNodes.Count > 0)
        {
            var propsJson = character.PropertiesEditor.ToJsonObject();
            duplicate.LoadProperties(propsJson);
        }

        Characters.Add(duplicate);
        SelectedCharacter = duplicate;
        IsDirty = true;
        OnPropertyChanged(nameof(FilteredCharacters));
    }

    [RelayCommand]
    private void ClearFilter()
    {
        SearchText = string.Empty;
    }

    [RelayCommand]
    private void Save()
    {
        if (_originalJson == null) return;

        // Clear and rebuild the JSON
        _originalJson.Clear();

        foreach (var character in Characters.OrderBy(c => c.Id))
        {
            _originalJson[character.Id] = character.ToJsonObject();
            character.IsModified = false;
        }

        IsDirty = false;
    }

    /// <summary>
    /// Build a new JsonObject from current state (for external saving).
    /// </summary>
    public JsonObject ToJson()
    {
        var result = new JsonObject();

        foreach (var character in Characters.OrderBy(c => c.Id))
        {
            result[character.Id] = character.ToJsonObject();
        }

        return result;
    }

    /// <summary>
    /// Get all character IDs.
    /// </summary>
    public IEnumerable<string> GetCharacterIds()
    {
        return Characters.Select(c => c.Id).OrderBy(id => id);
    }

    /// <summary>
    /// Get character display name by ID.
    /// </summary>
    public string? GetDisplayName(string id)
    {
        return Characters.FirstOrDefault(c => c.Id == id)?.DisplayName;
    }
}
