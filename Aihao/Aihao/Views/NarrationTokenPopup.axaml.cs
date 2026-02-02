using Avalonia.Controls;
using Avalonia.Input;
using Aihao.ViewModels;

namespace Aihao.Views;

public partial class NarrationTokenPopup : UserControl
{
    public NarrationTokenPopup()
    {
        InitializeComponent();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            if (DataContext is NarrationTokenPopupViewModel vm)
                vm.SelectCurrent();
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            IsVisible = false;
        }
        base.OnKeyDown(e);
    }
}
