using System;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Aihao.Models;

/// <summary>
/// The kind of C# reference (type, method, property, etc.)
/// </summary>
public enum CSharpReferenceKind
{
    /// <summary>
    /// A fully-qualified type name (e.g., "engine.news.InputEventPipeline")
    /// </summary>
    Type,
    
    /// <summary>
    /// A static factory method (e.g., "some.namespace.Factory.CreateInstance")
    /// </summary>
    StaticMethod,
    
    /// <summary>
    /// A property name (for property injection)
    /// </summary>
    Property
}

/// <summary>
/// Represents a reference to a C# type, method, or other code element.
/// This is a reusable model for any place in Aihao where we need to
/// specify a C# code reference (implementations, modules, factories, etc.)
/// </summary>
public partial class CSharpTypeReference : ObservableObject
{
    private static readonly Regex ValidIdentifierPattern = new(@"^[a-zA-Z_][a-zA-Z0-9_]*$", RegexOptions.Compiled);
    private static readonly Regex ValidFullNamePattern = new(@"^[a-zA-Z_][a-zA-Z0-9_]*(\.[a-zA-Z_][a-zA-Z0-9_]*)*$", RegexOptions.Compiled);
    
    [ObservableProperty]
    private string _fullName = string.Empty;
    
    [ObservableProperty]
    private CSharpReferenceKind _kind = CSharpReferenceKind.Type;
    
    [ObservableProperty]
    private string? _validationError;
    
    [ObservableProperty]
    private bool _isValid = true;
    
    /// <summary>
    /// True if there is a validation error.
    /// </summary>
    public bool HasError => !IsValid;
    
    /// <summary>
    /// The namespace portion (everything before the last dot for types,
    /// everything before the second-to-last dot for methods).
    /// </summary>
    public string Namespace => ExtractNamespace();
    
    /// <summary>
    /// The short name (last segment for types, class.method for methods).
    /// </summary>
    public string ShortName => ExtractShortName();
    
    /// <summary>
    /// For StaticMethod kind: the class name containing the method.
    /// </summary>
    public string? ClassName => Kind == CSharpReferenceKind.StaticMethod ? ExtractClassName() : null;
    
    /// <summary>
    /// For StaticMethod kind: the method name.
    /// </summary>
    public string? MethodName => Kind == CSharpReferenceKind.StaticMethod ? ExtractMethodName() : null;
    
    public CSharpTypeReference()
    {
    }
    
    public CSharpTypeReference(string fullName, CSharpReferenceKind kind = CSharpReferenceKind.Type)
    {
        _fullName = fullName;
        _kind = kind;
        Validate();
    }
    
    partial void OnFullNameChanged(string value)
    {
        Validate();
        OnPropertyChanged(nameof(Namespace));
        OnPropertyChanged(nameof(ShortName));
        OnPropertyChanged(nameof(ClassName));
        OnPropertyChanged(nameof(MethodName));
    }
    
    partial void OnKindChanged(CSharpReferenceKind value)
    {
        Validate();
        OnPropertyChanged(nameof(ClassName));
        OnPropertyChanged(nameof(MethodName));
    }
    
    partial void OnIsValidChanged(bool value)
    {
        OnPropertyChanged(nameof(HasError));
    }
    
    private void Validate()
    {
        ValidationError = null;
        IsValid = true;
        
        if (string.IsNullOrWhiteSpace(FullName))
        {
            // Empty is valid (represents null/default)
            return;
        }
        
        switch (Kind)
        {
            case CSharpReferenceKind.Type:
                if (!ValidFullNamePattern.IsMatch(FullName))
                {
                    ValidationError = "Invalid type name. Must be a valid fully-qualified C# type name.";
                    IsValid = false;
                }
                break;
                
            case CSharpReferenceKind.StaticMethod:
                // Must have at least Class.Method (two dots minimum for Namespace.Class.Method)
                var lastDot = FullName.LastIndexOf('.');
                if (lastDot <= 0)
                {
                    ValidationError = "Static method must be in format Namespace.Class.Method";
                    IsValid = false;
                }
                else if (!ValidFullNamePattern.IsMatch(FullName))
                {
                    ValidationError = "Invalid method reference. Must be a valid fully-qualified method name.";
                    IsValid = false;
                }
                break;
                
            case CSharpReferenceKind.Property:
                if (!ValidIdentifierPattern.IsMatch(FullName))
                {
                    ValidationError = "Invalid property name. Must be a valid C# identifier.";
                    IsValid = false;
                }
                break;
        }
    }
    
    private string ExtractNamespace()
    {
        if (string.IsNullOrEmpty(FullName))
            return string.Empty;
            
        var lastDot = FullName.LastIndexOf('.');
        if (lastDot <= 0)
            return string.Empty;
            
        if (Kind == CSharpReferenceKind.StaticMethod)
        {
            // For methods, namespace is everything before ClassName.MethodName
            var secondLastDot = FullName.LastIndexOf('.', lastDot - 1);
            return secondLastDot > 0 ? FullName.Substring(0, secondLastDot) : string.Empty;
        }
        
        return FullName.Substring(0, lastDot);
    }
    
    private string ExtractShortName()
    {
        if (string.IsNullOrEmpty(FullName))
            return string.Empty;
            
        if (Kind == CSharpReferenceKind.StaticMethod)
        {
            // For methods, short name is Class.Method
            var lastDot = FullName.LastIndexOf('.');
            if (lastDot <= 0)
                return FullName;
                
            var secondLastDot = FullName.LastIndexOf('.', lastDot - 1);
            return secondLastDot >= 0 ? FullName.Substring(secondLastDot + 1) : FullName;
        }
        
        var dot = FullName.LastIndexOf('.');
        return dot >= 0 ? FullName.Substring(dot + 1) : FullName;
    }
    
    private string? ExtractClassName()
    {
        if (string.IsNullOrEmpty(FullName))
            return null;
            
        var lastDot = FullName.LastIndexOf('.');
        if (lastDot <= 0)
            return null;
            
        var secondLastDot = FullName.LastIndexOf('.', lastDot - 1);
        if (secondLastDot < 0)
            return FullName.Substring(0, lastDot);
            
        return FullName.Substring(secondLastDot + 1, lastDot - secondLastDot - 1);
    }
    
    private string? ExtractMethodName()
    {
        if (string.IsNullOrEmpty(FullName))
            return null;
            
        var lastDot = FullName.LastIndexOf('.');
        return lastDot >= 0 ? FullName.Substring(lastDot + 1) : FullName;
    }
    
    public override string ToString() => FullName;
    
    /// <summary>
    /// Creates a type reference from a string.
    /// </summary>
    public static CSharpTypeReference FromType(string fullName)
        => new(fullName, CSharpReferenceKind.Type);
    
    /// <summary>
    /// Creates a static method reference from a string.
    /// </summary>
    public static CSharpTypeReference FromStaticMethod(string fullName)
        => new(fullName, CSharpReferenceKind.StaticMethod);
}
