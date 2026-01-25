using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Aihao.ViewModels;

public class SelectedTabConverter : IValueConverter
{
    public static readonly SelectedTabConverter Instance = new();
    
    private static readonly SolidColorBrush SelectedBrush = new(Color.Parse("#1E1E1E"));
    private static readonly SolidColorBrush UnselectedBrush = new(Color.Parse("#2D2D2D"));
    
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return UnselectedBrush;
        
        return value == parameter ? SelectedBrush : UnselectedBrush;
    }
    
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
