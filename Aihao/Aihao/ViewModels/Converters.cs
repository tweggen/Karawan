using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Aihao.Models;

namespace Aihao.ViewModels;

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

/// <summary>
/// Static converters for Implementations editor and other views.
/// </summary>
public static class Converters
{
    /// <summary>
    /// Converts any enum to its integer index.
    /// </summary>
    public static readonly IValueConverter EnumToIndex = new EnumToIndexConverter();
    
    /// <summary>
    /// Returns true if ImplementationCreationType is ExplicitClass.
    /// </summary>
    public static readonly IValueConverter IsExplicitClass = new CreationTypeConverter(ImplementationCreationType.ExplicitClass);
    
    /// <summary>
    /// Returns true if ImplementationCreationType is FactoryMethod.
    /// </summary>
    public static readonly IValueConverter IsFactoryMethod = new CreationTypeConverter(ImplementationCreationType.FactoryMethod);
    
    /// <summary>
    /// Returns true if ImplementationPropertyType is Dictionary.
    /// </summary>
    public static readonly IValueConverter IsDictionary = new PropertyTypeConverter(ImplementationPropertyType.Dictionary, false);
    
    /// <summary>
    /// Returns true if ImplementationPropertyType is NOT Dictionary.
    /// </summary>
    public static readonly IValueConverter IsNotDictionary = new PropertyTypeConverter(ImplementationPropertyType.Dictionary, true);
    
    private class EnumToIndexConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null) return 0;
            return (int)value;
        }
        
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is int index && targetType.IsEnum)
            {
                return Enum.ToObject(targetType, index);
            }
            return value;
        }
    }
    
    private class CreationTypeConverter : IValueConverter
    {
        private readonly ImplementationCreationType _targetType;
        
        public CreationTypeConverter(ImplementationCreationType targetType)
        {
            _targetType = targetType;
        }
        
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is ImplementationCreationType type)
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
    
    private class PropertyTypeConverter : IValueConverter
    {
        private readonly ImplementationPropertyType _targetType;
        private readonly bool _negate;
        
        public PropertyTypeConverter(ImplementationPropertyType targetType, bool negate)
        {
            _targetType = targetType;
            _negate = negate;
        }
        
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is ImplementationPropertyType type)
            {
                var result = type == _targetType;
                return _negate ? !result : result;
            }
            return _negate;
        }
        
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
