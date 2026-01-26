using Avalonia.Controls;

namespace Aihao.Views;

/// <summary>
/// Editor view for the /implementations section.
/// Shows a list of interface-to-implementation bindings with
/// support for different creation types, property injection, and config.
/// </summary>
public partial class ImplementationsEditor : UserControl
{
    public ImplementationsEditor()
    {
        InitializeComponent();
    }
}
