using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Aihao.ViewModels;

/// <summary>
/// Converts JsonValueKind to boolean for visibility.
/// </summary>
public class JsonValueKindToBoolConverter : IValueConverter
{
    public static readonly JsonValueKindToBoolConverter IsBool = new(true);
    public static readonly JsonValueKindToBoolConverter IsNotBool = new(false);
    
    private readonly bool _checkForBool;
    
    public JsonValueKindToBoolConverter(bool checkForBool)
    {
        _checkForBool = checkForBool;
    }
    
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is JsonValueKind kind)
        {
            bool isBool = kind == JsonValueKind.True || kind == JsonValueKind.False;
            return _checkForBool ? isBool : !isBool;
        }
        return !_checkForBool; // Default to showing non-bool editor
    }
    
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts string "true"/"false" to bool and back.
/// </summary>
public class StringToBoolConverter : IValueConverter
{
    public static readonly StringToBoolConverter Instance = new();
    
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s)
            return s.Equals("true", StringComparison.OrdinalIgnoreCase);
        return false;
    }
    
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
            return b ? "true" : "false";
        return "false";
    }
}

/// <summary>
/// Converts SpecialEditorType to boolean for visibility.
/// </summary>
public class EditorTypeConverter : IValueConverter
{
    public static readonly EditorTypeConverter IsResolution = new(SpecialEditorType.Resolution);
    public static readonly EditorTypeConverter IsDefault = new(SpecialEditorType.Default);
    
    private readonly SpecialEditorType _targetType;
    
    public EditorTypeConverter(SpecialEditorType targetType)
    {
        _targetType = targetType;
    }
    
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is SpecialEditorType editorType)
            return editorType == _targetType;
        return _targetType == SpecialEditorType.Default; // Default to showing default editor
    }
    
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts IsModified to background brush.
/// </summary>
public class ModifiedBackgroundConverter : IValueConverter
{
    public static readonly ModifiedBackgroundConverter Instance = new();
    
    private static readonly IBrush ModifiedBrush = new SolidColorBrush(Color.FromArgb(0x30, 0x00, 0x7A, 0xCC));
    private static readonly IBrush TransparentBrush = Brushes.Transparent;
    
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isModified && isModified)
            return ModifiedBrush;
        return TransparentBrush;
    }
    
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts validation error to border brush.
/// </summary>
public class ErrorBorderConverter : IValueConverter
{
    public static readonly ErrorBorderConverter Instance = new();
    
    private static readonly IBrush ErrorBrush = new SolidColorBrush(Color.FromRgb(0xF4, 0x47, 0x47));
    
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string error && !string.IsNullOrEmpty(error))
            return ErrorBrush;
        return null; // Use default border
    }
    
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
