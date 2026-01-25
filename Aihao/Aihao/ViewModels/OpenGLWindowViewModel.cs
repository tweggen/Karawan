using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aihao.ViewModels;

/// <summary>
/// ViewModel for OpenGL rendering window - used for tool output visualization
/// </summary>
public partial class OpenGLWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = "Render Output";
    
    [ObservableProperty]
    private int _width = 800;
    
    [ObservableProperty]
    private int _height = 600;
    
    [ObservableProperty]
    private bool _isRendering;
    
    [ObservableProperty]
    private double _fps;
    
    [ObservableProperty]
    private string _rendererInfo = "Not initialized";
    
    [ObservableProperty]
    private RenderMode _currentRenderMode = RenderMode.Solid;
    
    [ObservableProperty]
    private bool _showGrid = true;
    
    [ObservableProperty]
    private bool _showAxes = true;
    
    [ObservableProperty]
    private float _cameraDistance = 10f;
    
    [ObservableProperty]
    private float _cameraYaw;
    
    [ObservableProperty]
    private float _cameraPitch = -30f;
    
    // Events for the view to subscribe to
    public event EventHandler? RenderRequested;
    public event EventHandler? ResetCameraRequested;
    
    public void RequestRender()
    {
        RenderRequested?.Invoke(this, EventArgs.Empty);
    }
    
    [RelayCommand]
    private void SetRenderMode(RenderMode mode)
    {
        CurrentRenderMode = mode;
        RequestRender();
    }
    
    [RelayCommand]
    private void ToggleGrid()
    {
        ShowGrid = !ShowGrid;
        RequestRender();
    }
    
    [RelayCommand]
    private void ToggleAxes()
    {
        ShowAxes = !ShowAxes;
        RequestRender();
    }
    
    [RelayCommand]
    private void ResetCamera()
    {
        CameraDistance = 10f;
        CameraYaw = 0f;
        CameraPitch = -30f;
        ResetCameraRequested?.Invoke(this, EventArgs.Empty);
    }
    
    [RelayCommand]
    private void TakeScreenshot()
    {
        // TODO: Implement screenshot capture
    }
    
    public void UpdateFps(double fps)
    {
        Fps = fps;
    }
    
    public void SetRendererInfo(string info)
    {
        RendererInfo = info;
    }
}

public enum RenderMode
{
    Solid,
    Wireframe,
    Points,
    SolidWireframe
}
