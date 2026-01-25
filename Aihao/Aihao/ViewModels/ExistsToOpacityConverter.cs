using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Aihao.ViewModels;

public class ExistsToOpacityConverter : IValueConverter
{
    public static readonly ExistsToOpacityConverter Instance = new();
    
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool exists)
        {
            return exists ? 1.0 : 0.5;
        }
        return 1.0;
    }
    
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
