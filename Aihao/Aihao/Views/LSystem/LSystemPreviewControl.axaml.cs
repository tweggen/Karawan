using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Aihao.Controls;
using Aihao.ViewModels.LSystem;

namespace Aihao.Views.LSystem;

public partial class LSystemPreviewControl : UserControl
{
    private Point _lastPointerPos;
    private bool _isDragging;
    private AvaloniaGlPreviewControl? _glPreview;

    public LSystemPreviewControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    protected override void OnLoaded(Avalonia.Interactivity.RoutedEventArgs e)
    {
        base.OnLoaded(e);
        _glPreview = this.FindControl<AvaloniaGlPreviewControl>("GlPreview");
        if (_glPreview != null)
        {
            _glPreview.PointerPressed += OnPreviewPointerPressed;
            _glPreview.PointerMoved += OnPreviewPointerMoved;
            _glPreview.PointerReleased += OnPreviewPointerReleased;
            _glPreview.PointerWheelChanged += OnPreviewPointerWheel;
            _glPreview.GlReady += OnGlReady;
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is LSystemPreviewViewModel vm)
        {
            vm.RenderFrameChanged += OnRenderFrameChanged;
        }
    }

    private void OnGlReady()
    {
        // Trigger initial generation once GL is ready
        if (DataContext is LSystemPreviewViewModel vm)
        {
            vm.RefreshPreviewCommand.Execute(null);
        }
    }

    private void OnRenderFrameChanged()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_glPreview != null && DataContext is LSystemPreviewViewModel vm)
            {
                _glPreview.CameraDistance = vm.CameraDistance;
                _glPreview.CameraYaw = vm.CameraYaw;
                _glPreview.CameraPitch = vm.CameraPitch;
                _glPreview.HasContent = vm.HasContent;
                _glPreview.InvalidateVisual();
            }
        });
    }

    private void OnPreviewPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_glPreview == null) return;
        _isDragging = true;
        _lastPointerPos = e.GetPosition(_glPreview);
        e.Pointer.Capture(_glPreview);
    }

    private void OnPreviewPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDragging || _glPreview == null) return;
        if (DataContext is not LSystemPreviewViewModel vm) return;

        var pos = e.GetPosition(_glPreview);
        var delta = pos - _lastPointerPos;
        _lastPointerPos = pos;

        var props = e.GetCurrentPoint(_glPreview).Properties;
        if (props.IsLeftButtonPressed)
        {
            vm.UpdateCamera((float)delta.X * 0.5f, (float)delta.Y * 0.5f);
        }
    }

    private void OnPreviewPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _isDragging = false;
        e.Pointer.Capture(null);
    }

    private void OnPreviewPointerWheel(object? sender, PointerWheelEventArgs e)
    {
        if (DataContext is not LSystemPreviewViewModel vm) return;
        vm.UpdateZoom((float)e.Delta.Y);
    }
}
