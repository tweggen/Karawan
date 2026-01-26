using Avalonia.Controls;

namespace Aihao.Views;

/// <summary>
/// A reusable editor control for C# type references.
/// Shows a text box with validation feedback for fully-qualified type names,
/// static method references, or property names.
/// </summary>
public partial class CSharpTypeReferenceEditor : UserControl
{
    public CSharpTypeReferenceEditor()
    {
        InitializeComponent();
    }
}
