using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Aihao.ViewModels;

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
/// Converts PropertyValueKind to boolean for visibility.
/// Used by PropertiesEditor - may be deprecated when migrated to unified editor.
/// </summary>
public class PropertyValueKindConverter : IValueConverter
{
    public static readonly PropertyValueKindConverter IsBoolean = new(PropertyValueKind.Boolean);
    public static readonly PropertyValueKindConverter IsNotBoolean = new(PropertyValueKind.Boolean, true);
    public static readonly PropertyValueKindConverter IsNumeric = new(PropertyValueKind.Integer, false, PropertyValueKind.Float);
    
    private readonly PropertyValueKind _targetKind;
    private readonly PropertyValueKind? _altKind;
    private readonly bool _invert;
    
    public PropertyValueKindConverter(PropertyValueKind targetKind, bool invert = false, PropertyValueKind? altKind = null)
    {
        _targetKind = targetKind;
        _invert = invert;
        _altKind = altKind;
    }
    
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is PropertyValueKind kind)
        {
            bool matches = kind == _targetKind || (_altKind.HasValue && kind == _altKind.Value);
            return _invert ? !matches : matches;
        }
        return _invert;
    }
    
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts PropertyEditorType to boolean for visibility.
/// Used by PropertiesEditor - may be deprecated when migrated to unified editor.
/// </summary>
public class PropertyEditorTypeConverter : IValueConverter
{
    public static readonly PropertyEditorTypeConverter IsDefault = new(PropertyEditorType.Default);
    public static readonly PropertyEditorTypeConverter IsResolution = new(PropertyEditorType.Resolution);
    public static readonly PropertyEditorTypeConverter IsVector2 = new(PropertyEditorType.Vector2);
    public static readonly PropertyEditorTypeConverter IsVector3 = new(PropertyEditorType.Vector3);
    public static readonly PropertyEditorTypeConverter IsColor = new(PropertyEditorType.Color);
    public static readonly PropertyEditorTypeConverter IsSlider = new(PropertyEditorType.Slider);
    
    private readonly PropertyEditorType _targetType;
    
    public PropertyEditorTypeConverter(PropertyEditorType targetType)
    {
        _targetType = targetType;
    }
    
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is PropertyEditorType editorType)
            return editorType == _targetType;
        return _targetType == PropertyEditorType.Default;
    }
    
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts hex color string to Avalonia SolidColorBrush for Background binding.
/// </summary>
public class HexToColorConverter : IValueConverter
{
    public static readonly HexToColorConverter Instance = new();
    
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string hex && hex.StartsWith("#"))
        {
            try
            {
                var color = Color.Parse(hex);
                return new SolidColorBrush(color);
            }
            catch
            {
                return Brushes.Transparent;
            }
        }
        return Brushes.Transparent;
    }
    
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is SolidColorBrush brush)
            return $"#{brush.Color.R:X2}{brush.Color.G:X2}{brush.Color.B:X2}";
        return "#000000";
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
        return null;
    }
    
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
