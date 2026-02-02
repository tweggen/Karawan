using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Aihao.ViewModels;

public partial class NarrationTokenPopupViewModel : ObservableObject
{
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string? _selectedItem;

    public ObservableCollection<string> AllItems { get; } = new();
    public ObservableCollection<string> FilteredItems { get; } = new();

    public Action<string>? OnItemSelected { get; set; }

    partial void OnSearchTextChanged(string value)
    {
        UpdateFilter();
    }

    partial void OnSelectedItemChanged(string? value)
    {
        if (value != null)
            OnItemSelected?.Invoke(value);
    }

    public void UpdateFilter()
    {
        FilteredItems.Clear();
        var query = SearchText?.Trim() ?? "";
        var count = 0;
        foreach (var item in AllItems)
        {
            if (count >= 10) break;
            if (string.IsNullOrEmpty(query) || item.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                FilteredItems.Add(item);
                count++;
            }
        }
    }

    public void SelectCurrent()
    {
        var item = SelectedItem;
        if (item != null)
            OnItemSelected?.Invoke(item);
    }
}
