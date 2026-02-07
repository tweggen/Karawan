# Plan: L-System 3D Preview in Aihao Editor

## Context

Aihao is the Avalonia 11.3.8 IDE for Karawan. It has an L-System editor but no way to visualize results without running the full game. The Joyce engine uses singletons (`I` service locator), which is acceptable since the preview is also a singleton display.

**Goal**: Embed a real Splash.Silk 3D renderer into Aihao using Avalonia's `OpenGlControlBase`, parameterizing the Silk.NET backend to accept an externally-provided OpenGL context. The L-System preview is the first use case. No second renderer path — the same Splash code renders both the game and the preview.

## Architecture

```
Avalonia OpenGlControlBase
  → provides GlInterface.GetProcAddress
  → AvaloniaNativeContext adapter (implements Silk.NET INativeContext)
  → GL.GetApi(adapter) → Silk.NET GL instance
  → Platform.SetExternalGL(gl) → SilkThreeD.SetGL(gl)
  → SilkRenderer.RenderFrame(renderFrame)
  → draws into Avalonia's framebuffer
```

The L-System preview pipeline:
```
LSystemDefinitionViewModel.ToJson()
  → LSystemLoader.LoadDefinition() → CreateSystem()     [existing JoyceCode]
  → LGenerator.Generate(iterations)                       [existing JoyceCode]
  → AlphaInterpreter.Run() → MatMesh                     [existing JoyceCode]
  → InstanceDesc.CreateFromMatMesh()                      [existing JoyceCode]
  → InstanceManager.CreatePfInstance() → PfInstance        [existing Splash]
  → CameraOutput.AppendInstance()                          [existing Splash]
  → RenderFrame → SilkRenderer.RenderFrame()              [existing Splash.Silk]
```

## Implementation Steps

### Step 1: Null-guard Platform.SetupDone() for IView-less initialization

**Modify**: `Splash.Silk/Platform.cs`

`SetupDone()` (line 851) crashes when `_iView` is null. Add guards:

```csharp
public void SetupDone()
{
    _common = new();
    engine.GlobalSettings.Set("view.size", "320x200");

    string baseDirectory = System.AppContext.BaseDirectory;
    System.Console.WriteLine($"Running in directory {baseDirectory}");

    // Guard: only detect framework and bind events when IView exists
    if (_iView != null)
    {
        if (_iView.GetType().FullName.Contains("Glfw"))
            _underlyingFrameworks = UnderlyingFrameworks.Glfw;
        else if (_iView.GetType().FullName.Contains("Sdl"))
            _underlyingFrameworks = UnderlyingFrameworks.Sdl;

        _iView.Load += _windowOnLoad;
        _iView.Resize += _windowOnResize;
        _iView.Render += _windowOnRender;
        _iView.Update += _windowOnUpdate;
        _iView.Closing += _windowOnClose;
        _iView.FocusChanged += _windowOnFocusChanged;
    }

    I.Register<TextureGenerator>(() => new TextureGenerator());
    I.Register<IThreeD>(() => new SilkThreeD());
    _silkThreeD = I.Get<IThreeD>() as SilkThreeD;
    _silkThreeD.SetupDone();

    _engine.RunMainThread(() =>
    {
        _instanceManager = I.Get<InstanceManager>();
        _instanceManager.Manage(_engine.GetEcsWorld());
        _cameraManager = I.Get<CameraManager>();
        _cameraManager.Manage(_engine.GetEcsWorld());
        _logicalRenderer = I.Get<LogicalRenderer>();
    });

    _renderer = new SilkRenderer();
}
```

### Step 2: Add external GL entry points to Platform

**Modify**: `Splash.Silk/Platform.cs`

Add two new public methods:

```csharp
/// <summary>
/// Set GL context from an external source (e.g., Avalonia OpenGlControlBase).
/// Call this instead of relying on IView.Load to provide GL.
/// </summary>
public void SetExternalGL(GL gl)
{
    _gl = gl;
    _silkThreeD.SetGL(gl);
    gl.ClearDepth(1f);
    gl.ClearColor(0f, 0f, 0f, 0f);
}

/// <summary>
/// Render a single frame using the provided RenderFrame data and viewport size.
/// For use with externally-provided GL contexts (no IView).
/// Caller is responsible for GL context being current and calling SwapBuffers.
/// </summary>
public void RenderExternalFrame(RenderFrame renderFrame, int viewportWidth, int viewportHeight)
{
    _renderer.SetDimension(viewportWidth, viewportHeight);
    _renderer.RenderFrame(renderFrame);
    _silkThreeD.ExecuteGraphicsThreadActions(0.001f);
}
```

Also expose the SilkRenderer and InstanceManager for the preview service:
```csharp
public InstanceManager InstanceManager => _instanceManager;
```

### Step 3: Create AvaloniaNativeContext adapter

**New file**: `Aihao/Aihao/Services/Rendering/AvaloniaNativeContext.cs`

Bridges Avalonia's `GlInterface` to Silk.NET's `INativeContext`:

```csharp
using Avalonia.OpenGL;
using Silk.NET.Core.Contexts;

namespace Aihao.Services.Rendering;

/// <summary>
/// Adapts Avalonia's GlInterface to Silk.NET's INativeContext,
/// allowing creation of Silk.NET GL from Avalonia's OpenGL context.
/// </summary>
public class AvaloniaNativeContext : INativeContext
{
    private readonly GlInterface _gl;

    public AvaloniaNativeContext(GlInterface gl) => _gl = gl;

    public nint GetProcAddress(string proc, int? slot = null)
        => _gl.GetProcAddress(proc);

    public bool TryGetProcAddress(string proc, out nint addr, int? slot = null)
    {
        addr = _gl.GetProcAddress(proc);
        return addr != 0;
    }

    public void Dispose() { }
}
```

### Step 4: Create EnginePreviewService

**New file**: `Aihao/Aihao/Services/EnginePreviewService.cs`

Manages the singleton engine instance inside Aihao:

```csharp
namespace Aihao.Services;

/// <summary>
/// Manages a singleton Joyce engine instance for preview rendering.
/// Initializes the engine without a Silk.NET window — GL is provided
/// later by Avalonia's OpenGlControlBase.
/// </summary>
public class EnginePreviewService
{
    private static EnginePreviewService? _instance;
    private engine.Engine? _engine;
    private Splash.Silk.Platform? _platform;
    private bool _glInitialized;

    public static EnginePreviewService Instance => _instance ??= new();

    public Splash.Silk.Platform? Platform => _platform;
    public engine.Engine? Engine => _engine;
    public bool IsInitialized => _engine != null;
    public bool IsGLReady => _glInitialized;

    /// <summary>
    /// Initialize the engine (no GL yet). Call once at app startup or on first preview request.
    /// </summary>
    public void Initialize()
    {
        if (_engine != null) return;

        // Set required GlobalSettings before engine init
        engine.GlobalSettings.Set("platform.threeD.API", "OpenGL");
        engine.GlobalSettings.Set("platform.threeD.API.version", "410"); // macOS safe

        // Register services needed by AlphaInterpreter
        engine.I.Register<engine.joyce.TextureCatalogue>(
            () => CreatePreviewTextureCatalogue());
        engine.I.Register<engine.ObjectRegistry<engine.joyce.Material>>(
            () => new engine.ObjectRegistry<engine.joyce.Material>());

        // Create engine without IView (Platform.SetupDone will skip window bindings)
        _engine = Splash.Silk.Platform.EasyCreatePlatform(
            Array.Empty<string>(), out _platform);
    }

    /// <summary>
    /// Provide GL context from Avalonia. Call from OpenGlControlBase.OnOpenGlInit.
    /// </summary>
    public void SetGL(Silk.NET.OpenGL.GL gl)
    {
        if (_platform == null) return;
        _platform.SetExternalGL(gl);
        _glInitialized = true;
    }

    /// <summary>
    /// Create a TextureCatalogue with a synthetic rgba entry
    /// so AlphaInterpreter's material creation works.
    /// </summary>
    private static engine.joyce.TextureCatalogue CreatePreviewTextureCatalogue()
    {
        var catalogue = new engine.joyce.TextureCatalogue();
        // Register a synthetic "rgba" atlas entry.
        // This is the color lookup texture that FindColorTexture() references.
        // In the game it's compiled by Chushi; here we create a minimal entry.
        catalogue.AddAtlasEntry(
            "rgba",                              // textureTag
            "preview://rgba",                    // atlasTag
            System.Numerics.Vector2.Zero,        // uvOffset
            System.Numerics.Vector2.One,         // uvScale
            64, 64,                              // width, height
            false);                              // hasMipmap
        return catalogue;
    }

    /// <summary>
    /// Render a frame into the currently-bound framebuffer.
    /// </summary>
    public void RenderFrame(Splash.RenderFrame renderFrame, int width, int height)
    {
        _platform?.RenderExternalFrame(renderFrame, width, height);
    }
}
```

**Note on TextureCatalogue**: `AlphaInterpreter` calls `TextureCatalogue.FindColorTexture(0xff448822)` which looks up "rgba". We register a synthetic entry so it doesn't throw. The actual texture upload to GPU will use a programmatic 64x64 color lookup texture (generated in the GL init phase). The color lookup algorithm in `FindColorTexture()` maps ARGB → (x,y) pixel coordinates in this 64x64 grid. We need a matching texture uploaded to GPU — or alternatively, we can set `AlbedoColor` on the materials so the shader uses the diffuse color uniform even without a correct texture. For the first prototype, the AlbedoColor approach is sufficient since the shader multiplies `texture color * col4Diffuse`.

### Step 5: Create LSystemPreviewService

**New file**: `Aihao/Aihao/Services/Rendering/LSystemPreviewService.cs`

Generates L-System geometry and builds a RenderFrame:

```csharp
namespace Aihao.Services.Rendering;

/// <summary>
/// Generates L-System geometry and builds a Splash RenderFrame
/// for the preview, using the real engine rendering pipeline.
/// </summary>
public class LSystemPreviewService
{
    /// <summary>
    /// Generate a RenderFrame for previewing an L-System definition.
    /// </summary>
    public Splash.RenderFrame? BuildPreviewFrame(
        Aihao.ViewModels.LSystem.LSystemDefinitionViewModel definition,
        int iterations,
        int viewportWidth, int viewportHeight,
        float cameraDistance, float cameraYaw, float cameraPitch)
    {
        var engineService = EnginePreviewService.Instance;
        if (!engineService.IsInitialized || !engineService.IsGLReady)
            return null;

        // 1. Generate L-System → MatMesh (using existing pipeline)
        var json = definition.ToJson();
        var jsonStr = json.ToJsonString();
        var loader = new builtin.tools.Lindenmayer.LSystemLoader();
        var lsDef = loader.LoadDefinition(jsonStr);
        var system = loader.CreateSystem(lsDef);
        var generator = new builtin.tools.Lindenmayer.LGenerator(system, "preview");
        var instance = generator.Generate(iterations);

        var matMesh = new engine.joyce.MatMesh();
        var alpha = new builtin.tools.Lindenmayer.AlphaInterpreter(instance);
        alpha.Run(null, Vector3.Zero, matMesh, null);

        if (matMesh.IsEmpty()) return null;

        // 2. Convert MatMesh → InstanceDesc → PfInstance
        var instanceDesc = engine.joyce.InstanceDesc.CreateFromMatMesh(matMesh, 500f);
        var instanceManager = engineService.Platform.InstanceManager;
        var pfInstance = instanceManager.CreatePfInstance(instanceDesc);

        // 3. Build camera
        var camera3 = new engine.joyce.components.Camera3()
        {
            Angle = MathF.PI / 4f,
            NearFrustum = 0.1f,
            FarFrustum = 1000f,
            CameraFlags = engine.joyce.components.Camera3.Flags.EnableFog
        };

        // Camera position from spherical coordinates
        var yawRad = cameraYaw * MathF.PI / 180f;
        var pitchRad = cameraPitch * MathF.PI / 180f;

        // Compute geometry center from AABB
        var aabb = instanceDesc.AABB; // or compute from mesh vertices
        var target = aabb.Center;

        var eye = target + new Vector3(
            cameraDistance * MathF.Cos(pitchRad) * MathF.Sin(yawRad),
            cameraDistance * MathF.Sin(pitchRad),
            cameraDistance * MathF.Cos(pitchRad) * MathF.Cos(yawRad));

        var cameraToWorld = Matrix4x4.CreateLookAt(eye, target, Vector3.UnitY);
        // CreateLookAt gives view matrix (world→camera), invert for camera→world
        Matrix4x4.Invert(cameraToWorld, out var transformToWorld);

        // 4. Build CameraOutput manually
        var threeD = engine.I.Get<Splash.IThreeD>();
        var renderFrame = new Splash.RenderFrame();
        renderFrame.FrameNumber = 1;
        renderFrame.StartCollectTime = DateTime.Now;

        // Add basic lighting
        renderFrame.LightCollector.AddAmbientLight(
            new engine.joyce.components.AmbientLight { Color = new Vector4(0.3f) });
        renderFrame.LightCollector.AddDirectionalLight(
            new engine.joyce.components.DirectionalLight
            {
                Direction = Vector3.Normalize(new Vector3(0.5f, -1f, 0.3f)),
                Color = new Vector4(0.8f)
            });

        var cameraOutput = new Splash.CameraOutput(
            null, threeD, transformToWorld, camera3, renderFrame.FrameStats);

        var gpuAnimState = new engine.joyce.components.GPUAnimationState();
        cameraOutput.AppendInstance(pfInstance, Matrix4x4.Identity, gpuAnimState);
        cameraOutput.ComputeAfterAppend();

        var renderPart = new Splash.RenderPart();
        renderPart.CameraOutput = cameraOutput;
        renderFrame.RenderParts.Add(renderPart);

        renderFrame.EndCollectTime = DateTime.Now;
        return renderFrame;
    }
}
```

**Important**: This code calls `AlphaInterpreter`, `InstanceManager.CreatePfInstance()`, and builds a `CameraOutput` — all existing engine code. No new renderer path.

### Step 6: Create LSystemPreviewViewModel

**New file**: `Aihao/Aihao/ViewModels/LSystem/LSystemPreviewViewModel.cs`

```csharp
public partial class LSystemPreviewViewModel : ObservableObject
{
    [ObservableProperty] private int _iterations = 1;
    [ObservableProperty] private float _cameraDistance = 5f;
    [ObservableProperty] private float _cameraYaw = 30f;
    [ObservableProperty] private float _cameraPitch = 20f;
    [ObservableProperty] private bool _isRendering;
    [ObservableProperty] private string _statusText = "No definition selected";

    private LSystemDefinitionViewModel? _currentDefinition;
    private readonly LSystemPreviewService _previewService = new();

    // The latest RenderFrame to be consumed by the GL control
    private Splash.RenderFrame? _currentRenderFrame;

    public event Action? RenderFrameChanged;
    public event Action<LSystemPreviewViewModel>? PopOutRequested;

    public Splash.RenderFrame? CurrentRenderFrame => _currentRenderFrame;

    public void SetDefinition(LSystemDefinitionViewModel? definition) { ... }

    [RelayCommand]
    private void RefreshPreview()
    {
        if (_currentDefinition == null) return;
        // Build render frame (can be done on background thread for generation,
        // but mesh upload must happen on GL thread)
        _currentRenderFrame = _previewService.BuildPreviewFrame(
            _currentDefinition, Iterations,
            512, 384, CameraDistance, CameraYaw, CameraPitch);
        RenderFrameChanged?.Invoke();
    }

    [RelayCommand]
    private void PopOut() => PopOutRequested?.Invoke(this);
}
```

### Step 7: Create AvaloniaGlPreviewControl

**New file**: `Aihao/Aihao/Views/LSystem/AvaloniaGlPreviewControl.cs`

Subclass of Avalonia's `OpenGlControlBase` that bridges to Splash:

```csharp
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Silk.NET.OpenGL;
using Aihao.Services;
using Aihao.Services.Rendering;

namespace Aihao.Views.LSystem;

public class AvaloniaGlPreviewControl : OpenGlControlBase
{
    private GL? _silkGl;
    private Splash.RenderFrame? _pendingFrame;

    public void SetRenderFrame(Splash.RenderFrame? frame)
    {
        _pendingFrame = frame;
        RequestNextFrameRendering(); // Avalonia method to trigger OnOpenGlRender
    }

    protected override void OnOpenGlInit(GlInterface gl)
    {
        base.OnOpenGlInit(gl);

        // Bridge Avalonia's GL to Silk.NET
        var nativeContext = new AvaloniaNativeContext(gl);
        _silkGl = GL.GetApi(nativeContext);

        // Initialize engine if not already done
        var service = EnginePreviewService.Instance;
        if (!service.IsInitialized)
            service.Initialize();

        // Provide GL to the engine
        service.SetGL(_silkGl);
    }

    protected override void OnOpenGlRender(GlInterface gl, int fb)
    {
        var service = EnginePreviewService.Instance;
        if (!service.IsGLReady || _pendingFrame == null) return;

        var size = GetPixelSize();
        service.RenderFrame(_pendingFrame, (int)size.Width, (int)size.Height);
    }

    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        _silkGl?.Dispose();
        _silkGl = null;
        base.OnOpenGlDeinit(gl);
    }

    private Avalonia.Size GetPixelSize()
    {
        var scaling = VisualRoot?.RenderScaling ?? 1.0;
        return new Avalonia.Size(Bounds.Width * scaling, Bounds.Height * scaling);
    }
}
```

### Step 8: Create LSystemPreviewControl (AXAML wrapper)

**New file**: `Aihao/Aihao/Views/LSystem/LSystemPreviewControl.axaml`

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:Aihao.ViewModels.LSystem"
             xmlns:local="using:Aihao.Views.LSystem"
             x:Class="Aihao.Views.LSystem.LSystemPreviewControl"
             x:DataType="vm:LSystemPreviewViewModel">
    <DockPanel>
        <Border DockPanel.Dock="Bottom" Padding="4,2"
                Background="{DynamicResource SystemControlBackgroundChromeMediumLowBrush}">
            <StackPanel Orientation="Horizontal" Spacing="6">
                <TextBlock Text="Iter:" VerticalAlignment="Center" FontSize="11"/>
                <Slider Minimum="0" Maximum="5" Value="{Binding Iterations}"
                        Width="80" VerticalAlignment="Center"/>
                <TextBlock Text="{Binding Iterations}" Width="16"
                           VerticalAlignment="Center" FontSize="11"/>
                <Button Content="Refresh" Command="{Binding RefreshPreviewCommand}"
                        FontSize="11" Padding="6,2"/>
                <Button Content="Pop Out" Command="{Binding PopOutCommand}"
                        FontSize="11" Padding="6,2"/>
                <TextBlock Text="{Binding StatusText}" Opacity="0.6"
                           VerticalAlignment="Center" FontSize="10"/>
            </StackPanel>
        </Border>
        <local:AvaloniaGlPreviewControl x:Name="GlControl"/>
    </DockPanel>
</UserControl>
```

**New file**: `Aihao/Aihao/Views/LSystem/LSystemPreviewControl.axaml.cs`

Code-behind wires the ViewModel's `RenderFrameChanged` event to `GlControl.SetRenderFrame()`, and handles mouse orbit interaction (pointer drag → yaw/pitch, scroll → zoom). Follow the mouse interaction pattern from `Aihao/Aihao/Views/OpenGLWindow.axaml.cs`.

### Step 9: Embed preview in L-System editor layout

**Modify**: `Aihao/Aihao/ViewModels/LSystem/LSystemEditorViewModel.cs`

Add preview property and wire to selection:
```csharp
public LSystemPreviewViewModel Preview { get; } = new();

// In OnSelectedTreeItemChanged, after setting SelectedDefinition:
Preview.SetDefinition(def);
```

**Modify**: `Aihao/Aihao/Views/LSystemEditor.axaml`

Change the right column (Column 4, currently rule detail in a ScrollViewer at line 194) to a vertical split: rule detail on top, preview on bottom.

```xml
<Grid Grid.Column="4">
    <Grid.RowDefinitions>
        <RowDefinition Height="*"/>
        <RowDefinition Height="Auto"/>
        <RowDefinition Height="300"/>
    </Grid.RowDefinitions>

    <!-- Existing rule/macro detail content (move from lines 194-317) -->
    <ScrollViewer Grid.Row="0" Margin="4">
        <!-- existing StackPanel content -->
    </ScrollViewer>

    <GridSplitter Grid.Row="1" Height="4" ResizeBehavior="PreviousAndNext"/>

    <lsViews:LSystemPreviewControl Grid.Row="2" DataContext="{Binding Preview}"/>
</Grid>
```

### Step 10: Pop-out document tab support

**Modify**: `Aihao/Aihao/ViewModels/Dock/DockViewModels.cs`

Add `LSystemPreviewDocumentViewModel`:
```csharp
public partial class LSystemPreviewDocumentViewModel : DocumentViewModel
{
    public LSystemPreviewViewModel Preview { get; }
    public LSystemPreviewDocumentViewModel(LSystemPreviewViewModel preview)
    {
        Preview = preview; Content = preview;
        Id = "LSystemPreview"; Title = "L-System Preview"; CanClose = true;
    }
}
```

**New file**: `Aihao/Aihao/Views/Dock/LSystemPreviewDocumentView.axaml` (+ `.axaml.cs`)

Hosts `LSystemPreviewControl` bound to `{Binding Preview}`.

**Modify**: `Aihao/Aihao/Views/MainWindow.axaml`

Add DataTemplate:
```xml
<DataTemplate DataType="dock:LSystemPreviewDocumentViewModel">
    <dockViews:LSystemPreviewDocumentView/>
</DataTemplate>
```

**Modify**: `Aihao/Aihao/ViewModels/MainWindowViewModel.cs`

In the "lsystems" section editor creation, subscribe to PopOutRequested:
```csharp
lsystemEditor.Preview.PopOutRequested += (preview) =>
    _dockFactory.AddDocument(new LSystemPreviewDocumentViewModel(preview));
```

## Key Files Reference

| Existing file | Role |
|---|---|
| `Splash.Silk/Platform.cs` | **Modify**: null guards, SetExternalGL(), RenderExternalFrame() |
| `Splash.Silk/SilkThreeD.cs` | Already has `SetGL(GL)` — no changes needed |
| `Splash.Silk/SilkRenderer.cs` | Already has `RenderFrame()` — no changes needed |
| `Splash/CameraOutput.cs` | Used to build render batches manually |
| `Splash/RenderFrame.cs` | The render data container |
| `Splash/InstanceManager.cs` | `CreatePfInstance()` converts InstanceDesc → PfInstance |
| `JoyceCode/engine/joyce/InstanceDesc.cs` | `CreateFromMatMesh()` converts MatMesh → InstanceDesc |
| `JoyceCode/builtin/tools/Lindenmayer/AlphaInterpreter.cs` | L-System → MatMesh (existing, reused as-is) |
| `JoyceCode/builtin/tools/Lindenmayer/LSystemLoader.cs` | JSON → runtime System |
| `JoyceCode/builtin/tools/Lindenmayer/LGenerator.cs` | Iterate and finalize L-System |
| `Aihao/Aihao/Views/OpenGLWindow.axaml.cs` | Mouse orbit pattern to follow |
| `Aihao/Aihao/ViewModels/LSystem/LSystemEditorViewModel.cs` | **Modify**: add Preview property |
| `Aihao/Aihao/Views/LSystemEditor.axaml` | **Modify**: add preview pane |
| `Aihao/Aihao/ViewModels/Dock/DockViewModels.cs` | **Modify**: add preview document VM |
| `Aihao/Aihao/Views/MainWindow.axaml` | **Modify**: add DataTemplate |
| `Aihao/Aihao/ViewModels/MainWindowViewModel.cs` | **Modify**: handle pop-out |

## Risks and Mitigations

**GL context compatibility**: Avalonia's GL context must be compatible with Silk.NET's expectations (version 4.1+ on macOS). Avalonia uses its own GL abstraction but exposes GetProcAddress. If the version is too low, the shader compilation in ShaderManager may fail. Mitigation: set GlobalSettings API version to match what Avalonia provides.

**GL state conflicts**: Avalonia uses GL internally for compositing. SilkThreeD.SetGL() sets GL state (cull face, depth test, etc.) which could interfere. Mitigation: save/restore GL state around render calls, or ensure Avalonia resets its own state.

**TextureCatalogue "rgba" entry**: AlphaInterpreter creates materials via FindColorTexture which needs an "rgba" atlas entry. We register a synthetic entry. The actual texture must be uploaded to GPU during GL init. If the color lookup texture isn't right, materials will render with wrong colors but AlbedoColor uniforms will still work as a fallback.

**CameraOutput constructor needs IScene**: It takes an `IScene` parameter. For preview, we may need to pass null or a minimal stub. Check if null is acceptable in the render path (it's only used for frustum culling context which we can skip for preview).

**Threading**: L-System generation (LGenerator, AlphaInterpreter) is CPU-bound and can run on a background thread. Mesh upload (CreatePfInstance → FillMeshEntry → UploadMeshEntry) must happen on the GL thread (inside OnOpenGlRender). The RenderFrame can be built on any thread, but rendering must happen on the GL thread.

## Verification

1. `dotnet build Karawan.sln` — must compile
2. Run the existing game (`dotnet run --project Karawan/Karawan.csproj`) — must still work unchanged
3. Run Aihao, load the game project, open L-Systems editor
4. Select tree1 → preview renders in bottom-right showing tree geometry using Splash
5. Adjust iterations slider → geometry complexity changes
6. Drag in preview → camera orbits
7. Scroll → zoom in/out
8. Click "Pop Out" → standalone document tab opens
9. Select tree2 → preview updates
