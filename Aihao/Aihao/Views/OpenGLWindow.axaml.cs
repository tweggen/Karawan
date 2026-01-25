using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Aihao.ViewModels;

namespace Aihao.Views;

/// <summary>
/// OpenGL rendering window - placeholder for Joyce/Splash integration
/// </summary>
public partial class OpenGLWindow : UserControl
{
    private Border? _renderContainer;
    private Point _lastPointerPos;
    private bool _isDragging;
    
    public OpenGLWindow()
    {
        InitializeComponent();
        
        // Set up when loaded
        Loaded += OnLoaded;
    }
    
    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _renderContainer = this.FindControl<Border>("RenderContainer");
        
        if (_renderContainer != null)
        {
            _renderContainer.PointerPressed += OnRenderPointerPressed;
            _renderContainer.PointerMoved += OnRenderPointerMoved;
            _renderContainer.PointerReleased += OnRenderPointerReleased;
            _renderContainer.PointerWheelChanged += OnRenderPointerWheel;
        }
        
        // Wire up to view model
        if (DataContext is OpenGLWindowViewModel vm)
        {
            vm.RenderRequested += OnRenderRequested;
            vm.ResetCameraRequested += OnResetCameraRequested;
            
            // Set initial state
            vm.SetRendererInfo("OpenGL renderer placeholder - connect Joyce/Splash here");
        }
    }
    
    private void OnRenderRequested(object? sender, EventArgs e)
    {
        // Trigger a redraw - in real implementation this would invalidate the GL context
        InvalidateVisual();
    }
    
    private void OnResetCameraRequested(object? sender, EventArgs e)
    {
        // Reset camera state
        InvalidateVisual();
    }
    
    private void OnRenderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_renderContainer == null) return;
        
        _isDragging = true;
        _lastPointerPos = e.GetPosition(_renderContainer);
        e.Pointer.Capture(_renderContainer);
    }
    
    private void OnRenderPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDragging || _renderContainer == null) return;
        if (DataContext is not OpenGLWindowViewModel vm) return;
        
        var pos = e.GetPosition(_renderContainer);
        var delta = pos - _lastPointerPos;
        _lastPointerPos = pos;
        
        var props = e.GetCurrentPoint(_renderContainer).Properties;
        
        if (props.IsLeftButtonPressed)
        {
            // Rotate camera
            vm.CameraYaw += (float)delta.X * 0.5f;
            vm.CameraPitch += (float)delta.Y * 0.5f;
            vm.CameraPitch = Math.Clamp(vm.CameraPitch, -89f, 89f);
        }
        else if (props.IsMiddleButtonPressed)
        {
            // Pan camera (to be implemented)
        }
    }
    
    private void OnRenderPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _isDragging = false;
        e.Pointer.Capture(null);
    }
    
    private void OnRenderPointerWheel(object? sender, PointerWheelEventArgs e)
    {
        if (DataContext is not OpenGLWindowViewModel vm) return;
        
        vm.CameraDistance -= (float)e.Delta.Y * 0.5f;
        vm.CameraDistance = Math.Max(0.1f, vm.CameraDistance);
    }
}
