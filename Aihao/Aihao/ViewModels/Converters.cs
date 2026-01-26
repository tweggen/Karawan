using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Aihao.Models;

namespace Aihao.ViewModels;

/// <summary>
/// Converters for PropertyValueType in the property tree editor.
/// </summary>
public static class PropertyValueTypeConverters
{
    public static readonly IValueConverter IsString = new PropertyValueTypeMatchConverter(PropertyValueType.String);
    public static readonly IValueConverter IsNumber = new PropertyValueTypeMatchConverter(PropertyValueType.Number);
    public static readonly IValueConverter IsBoolean = new PropertyValueTypeMatchConverter(PropertyValueType.Boolean);
    public static readonly IValueConverter IsNull = new PropertyValueTypeMatchConverter(PropertyValueType.Null);
    public static readonly IValueConverter IsObject = new PropertyValueTypeMatchConverter(PropertyValueType.Object);
    public static readonly IValueConverter IsArray = new PropertyValueTypeMatchConverter(PropertyValueType.Array);
    public static readonly IValueConverter IsStringOrNumber = new PropertyValueTypeMultiMatchConverter(PropertyValueType.String, PropertyValueType.Number);
    
    private class PropertyValueTypeMatchConverter : IValueConverter
    {
        private readonly PropertyValueType _targetType;
        
        public PropertyValueTypeMatchConverter(PropertyValueType targetType)
        {
            _targetType = targetType;
        }
        
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is PropertyValueType type)
            {
                return type == _targetType;
            }
            return false;
        }
        
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    
    private class PropertyValueTypeMultiMatchConverter : IValueConverter
    {
        private readonly PropertyValueType[] _targetTypes;
        
        public PropertyValueTypeMultiMatchConverter(params PropertyValueType[] targetTypes)
        {
            _targetTypes = targetTypes;
        }
        
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is PropertyValueType type)
            {
                return Array.IndexOf(_targetTypes, type) >= 0;
            }
            return false;
        }
        
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

/// <summary>
/// Converters for settings item types.
/// </summary>
public static class SettingsTypeConverter
{
    public static readonly IValueConverter IsString = new SettingsTypeMatchConverter(SettingsItemType.String);
    public static readonly IValueConverter IsBool = new SettingsTypeMatchConverter(SettingsItemType.Bool);
    public static readonly IValueConverter IsInt = new SettingsTypeMatchConverter(SettingsItemType.Int);
    public static readonly IValueConverter IsChoice = new SettingsTypeMatchConverter(SettingsItemType.Choice);
    public static readonly IValueConverter IsPath = new SettingsTypeMatchConverter(SettingsItemType.Path);
    
    private class SettingsTypeMatchConverter : IValueConverter
    {
        private readonly SettingsItemType _targetType;
        
        public SettingsTypeMatchConverter(SettingsItemType targetType)
        {
            _targetType = targetType;
        }
        
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is SettingsItemType type)
            {
                return type == _targetType;
            }
            return false;
        }
        
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

/// <summary>
/// Converts object? to decimal? for NumericUpDown binding.
/// </summary>
public class ObjectToDecimalConverter : IValueConverter
{
    public static readonly ObjectToDecimalConverter Instance = new();
    
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null) return null;
        
        if (value is int i) return (decimal)i;
        if (value is long l) return (decimal)l;
        if (value is double d) return (decimal)d;
        if (value is float f) return (decimal)f;
        if (value is decimal dec) return dec;
        
        if (int.TryParse(value.ToString(), out var parsed))
            return (decimal)parsed;
            
        return null;
    }
    
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is decimal dec)
            return (int)dec;
        return value;
    }
}

/// <summary>
/// Converts int? to decimal? for NumericUpDown Min/Max binding.
/// </summary>
public class IntToDecimalConverter : IValueConverter
{
    public static readonly IntToDecimalConverter Instance = new();
    
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null) return null;
        if (value is int i) return (decimal)i;
        return null;
    }
    
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is decimal dec) return (int)dec;
        return value;
    }
}

/// <summary>
/// Converts object? to bool for ToggleSwitch binding.
/// </summary>
public class ObjectToBoolConverter : IValueConverter
{
    public static readonly ObjectToBoolConverter Instance = new();
    
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b) return b;
        if (value is string s) return s.Equals("true", StringComparison.OrdinalIgnoreCase);
        return false;
    }
    
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool b && b;
    }
}

/// <summary>
/// Converts a depth value to a left margin for tree indentation.
/// </summary>
public class DepthToMarginConverter : IValueConverter
{
    public static readonly DepthToMarginConverter Instance = new();
    
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int depth)
        {
            return new Thickness(depth * 16, 4, 4, 4);
        }
        return new Thickness(4);
    }
    
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts a boolean to FontWeight (true = Bold, false = Normal).
/// </summary>
public class BoolToFontWeightConverter : IValueConverter
{
    public static readonly BoolToFontWeightConverter Instance = new();
    
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b && b)
        {
            return FontWeight.Bold;
        }
        return FontWeight.Normal;
    }
    
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts recording state to background color.
/// </summary>
public class RecordingBackgroundConverter : IValueConverter
{
    public static readonly RecordingBackgroundConverter Instance = new();
    
    private static readonly SolidColorBrush RecordingBrush = new(Color.Parse("#1A007ACC"));
    private static readonly SolidColorBrush NormalBrush = new(Colors.Transparent);
    
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isRecording && isRecording)
        {
            return RecordingBrush;
        }
        return NormalBrush;
    }
    
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts bool to opacity (true = 1.0, false = 0.5).
/// </summary>
public class BoolToOpacityConverter : IValueConverter
{
    public static readonly BoolToOpacityConverter Instance = new();
    
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b && b)
        {
            return 1.0;
        }
        return 0.5;
    }
    
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
