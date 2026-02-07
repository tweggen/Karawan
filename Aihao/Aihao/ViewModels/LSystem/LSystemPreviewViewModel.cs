using System;
using System.Threading;
using System.Threading.Tasks;
using Aihao.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aihao.ViewModels.LSystem;

/// <summary>
/// ViewModel for the L-System 3D preview pane.
/// </summary>
public partial class LSystemPreviewViewModel : ObservableObject
{
    private readonly LSystemPreviewService _previewService = new();
    private LSystemDefinitionViewModel? _definition;
    private CancellationTokenSource? _cts;

    [ObservableProperty] private int _iterations = 3;
    [ObservableProperty] private float _cameraDistance = 30f;
    [ObservableProperty] private float _cameraYaw = 45f;
    [ObservableProperty] private float _cameraPitch = 25f;
    [ObservableProperty] private bool _isGenerating;
    [ObservableProperty] private string _statusText = "No definition selected";
    [ObservableProperty] private bool _hasContent;

    /// <summary>
    /// Raised when a new render frame is ready and the GL control should repaint.
    /// </summary>
    public event Action? RenderFrameChanged;

    partial void OnIterationsChanged(int value)
    {
        _ = RefreshPreviewAsync();
    }

    public void SetDefinition(LSystemDefinitionViewModel? definition)
    {
        _definition = definition;
        if (definition == null)
        {
            StatusText = "No definition selected";
            HasContent = false;
            return;
        }
        _ = RefreshPreviewAsync();
    }

    [RelayCommand]
    private async Task RefreshPreviewAsync()
    {
        if (_definition == null) return;

        // Cancel any in-flight generation
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        IsGenerating = true;
        StatusText = "Generating...";

        try
        {
            var json = _definition.ToJson().ToJsonString();

            var success = await _previewService.GenerateAsync(json, Iterations, ct);

            if (ct.IsCancellationRequested) return;

            if (!success)
            {
                StatusText = "Empty result";
                HasContent = false;
                return;
            }

            HasContent = true;
            StatusText = $"Generated ({Iterations} iterations)";

            RenderFrameChanged?.Invoke();
        }
        catch (OperationCanceledException)
        {
            // Generation was cancelled, ignore
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            HasContent = false;
        }
        finally
        {
            IsGenerating = false;
        }
    }

    [RelayCommand]
    private void ResetCamera()
    {
        CameraDistance = 30f;
        CameraYaw = 45f;
        CameraPitch = 25f;
        RenderFrameChanged?.Invoke();
    }

    public void UpdateCamera(float yawDelta, float pitchDelta)
    {
        CameraYaw += yawDelta;
        CameraPitch = Math.Clamp(CameraPitch + pitchDelta, -89f, 89f);
        RenderFrameChanged?.Invoke();
    }

    public void UpdateZoom(float delta)
    {
        CameraDistance = Math.Max(1f, CameraDistance - delta * 2f);
        RenderFrameChanged?.Invoke();
    }
}
