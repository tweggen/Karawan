using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using DefaultEcs;
using engine;
using engine.joyce;
using engine.joyce.components;
using engine.news;
using static engine.Logger;

namespace grid;

/// <summary>
/// Data structure for a single frame of height data.
/// </summary>
public class GridFrame
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("heights")]
    public float[][] Heights { get; set; } = Array.Empty<float[]>();
}

/// <summary>
/// Data structure for the grid data JSON file.
/// </summary>
public class GridData
{
    [JsonPropertyName("heightScale")]
    public float HeightScale { get; set; } = 1.0f;

    [JsonPropertyName("frames")]
    public GridFrame[] Frames { get; set; } = Array.Empty<GridFrame>();
}

/// <summary>
/// A scene displaying a 7x12 grid of columns with heights loaded from a JSON file.
/// Use left/right arrow keys to navigate between frames.
/// </summary>
public class Scene : AModule, IScene, IInputPart
{
    private const int GridRows = 7;
    private const int GridCols = 12;
    private const float ColumnBaseSize = 1.0f; // 1 meter
    private const float InputZOrder = 20f;

    private Entity[,] _cubeGrid = new Entity[GridRows, GridCols];
    private Entity _eCamera;
    private Entity _eDirectionalLight;
    private Entity _eAmbientLight;

    private GridData _gridData;
    private int _currentFrameIndex = 0;
    private InstanceDesc _cubeInstanceDesc;

    /// <summary>
    /// Create the cube mesh with normals.
    /// </summary>
    private Mesh _createCubeMesh()
    {
        var mesh = engine.joyce.mesh.Tools.CreateCubeMesh("GridCube", ColumnBaseSize);

        // Add normals for each face (4 vertices per face * 6 faces = 24 normals)
        mesh.Normals = new List<Vector3>();

        int verticesPerFace = 4;
        for (int i = 0; i < verticesPerFace; i++) mesh.Normals.Add(new Vector3(0, 0, -1)); // Back
        for (int i = 0; i < verticesPerFace; i++) mesh.Normals.Add(new Vector3(0, 0, 1));  // Front
        for (int i = 0; i < verticesPerFace; i++) mesh.Normals.Add(new Vector3(0, 1, 0));  // Top
        for (int i = 0; i < verticesPerFace; i++) mesh.Normals.Add(new Vector3(0, -1, 0)); // Bottom
        for (int i = 0; i < verticesPerFace; i++) mesh.Normals.Add(new Vector3(1, 0, 0));  // Right
        for (int i = 0; i < verticesPerFace; i++) mesh.Normals.Add(new Vector3(-1, 0, 0)); // Left

        return mesh;
    }

    /// <summary>
    /// Create the instance description for the cube.
    /// </summary>
    private InstanceDesc _createCubeInstanceDesc()
    {
        var mesh = _createCubeMesh();

        var material = new Material
        {
            AlbedoColor = 0xff4080c0,  // Blue-gray (ARGB)
            Texture = new Texture(Texture.BLACK)
        };

        var meshes = new List<Mesh> { mesh };
        var meshMaterials = new List<int> { 0 };
        var materials = new List<Material> { material };
        var modelNodes = new List<ModelNode>();

        return new InstanceDesc(meshes, meshMaterials, materials, modelNodes, 100f);
    }

    /// <summary>
    /// Create a camera world matrix from position looking at target.
    /// </summary>
    private Matrix4x4 _createLookAtMatrix(Vector3 v3Position, Vector3 v3Target, Vector3 v3Up)
    {
        var v3MinusZ = Vector3.Normalize(v3Target - v3Position);
        var v3Right = Vector3.Normalize(Vector3.Cross(v3MinusZ, v3Up));
        var v3ActualUp = Vector3.Cross(v3Right, v3MinusZ);

        return new Matrix4x4(
            v3Right.X, v3Right.Y, v3Right.Z, 0,
            v3ActualUp.X, v3ActualUp.Y, v3ActualUp.Z, 0,
            -v3MinusZ.X, -v3MinusZ.Y, -v3MinusZ.Z, 0,
            v3Position.X, v3Position.Y, v3Position.Z, 1
        );
    }

    /// <summary>
    /// Load the grid data from the JSON file.
    /// </summary>
    private void _loadGridData()
    {
        try
        {
            using var stream = Assets.Open("griddata.json");
            _gridData = JsonSerializer.Deserialize<GridData>(stream, new JsonSerializerOptions
            {
                AllowTrailingCommas = true,
                PropertyNameCaseInsensitive = true
            });

            if (_gridData == null || _gridData.Frames.Length == 0)
            {
                Error("Failed to load grid data or no frames found.");
                _gridData = _createDefaultGridData();
            }
            else
            {
                Trace($"Loaded {_gridData.Frames.Length} frames from griddata.json, heightScale={_gridData.HeightScale}");
            }
        }
        catch (Exception ex)
        {
            Error($"Error loading grid data: {ex.Message}");
            _gridData = _createDefaultGridData();
        }
    }

    /// <summary>
    /// Create default grid data if loading fails.
    /// </summary>
    private GridData _createDefaultGridData()
    {
        var data = new GridData { HeightScale = 1.0f };
        var frame = new GridFrame
        {
            Name = "Default",
            Heights = new float[GridRows][]
        };

        for (int row = 0; row < GridRows; row++)
        {
            frame.Heights[row] = new float[GridCols];
            for (int col = 0; col < GridCols; col++)
            {
                frame.Heights[row][col] = 1.0f;
            }
        }

        data.Frames = new[] { frame };
        return data;
    }

    /// <summary>
    /// Create the transform matrix for a column at given row/col with specified height.
    /// The cube mesh is 1x1x1 centered at origin, so we scale Y and offset Y by half the height.
    /// </summary>
    private Matrix4x4 _createColumnTransform(int row, int col, float height)
    {
        // Center the grid: offset so grid center is at origin
        float offsetX = -(GridCols - 1) * ColumnBaseSize / 2f;
        float offsetZ = -(GridRows - 1) * ColumnBaseSize / 2f;

        float x = col * ColumnBaseSize + offsetX;
        float z = row * ColumnBaseSize + offsetZ;
        float y = height / 2f; // Bottom of cube at y=0

        // Scale Y by height, translate to position
        return Matrix4x4.CreateScale(1f, height, 1f) * Matrix4x4.CreateTranslation(x, y, z);
    }

    /// <summary>
    /// Update the grid to display the current frame.
    /// </summary>
    private void _updateGridDisplay()
    {
        if (_gridData == null || _gridData.Frames.Length == 0) return;

        var frame = _gridData.Frames[_currentFrameIndex];
        float scale = _gridData.HeightScale;

        Trace($"Displaying frame {_currentFrameIndex + 1}/{_gridData.Frames.Length}: {frame.Name}");

        for (int row = 0; row < GridRows; row++)
        {
            for (int col = 0; col < GridCols; col++)
            {
                if (!_cubeGrid[row, col].IsAlive) continue;

                float height = 0.1f; // Minimum height
                if (row < frame.Heights.Length && col < frame.Heights[row].Length)
                {
                    height = Math.Max(0.1f, frame.Heights[row][col] * scale);
                }

                var transform = _cubeGrid[row, col].Get<Transform3ToWorld>();
                transform.Matrix = _createColumnTransform(row, col, height);
                _cubeGrid[row, col].Set(transform);
            }
        }
    }

    /// <summary>
    /// Navigate to the next frame.
    /// </summary>
    private void _nextFrame()
    {
        if (_gridData == null || _gridData.Frames.Length == 0) return;
        _currentFrameIndex = (_currentFrameIndex + 1) % _gridData.Frames.Length;
        _engine.QueueMainThreadAction(_updateGridDisplay);
    }

    /// <summary>
    /// Navigate to the previous frame.
    /// </summary>
    private void _previousFrame()
    {
        if (_gridData == null || _gridData.Frames.Length == 0) return;
        _currentFrameIndex = (_currentFrameIndex - 1 + _gridData.Frames.Length) % _gridData.Frames.Length;
        _engine.QueueMainThreadAction(_updateGridDisplay);
    }

    /// <summary>
    /// Set up the scene entities.
    /// </summary>
    private void _setupScene()
    {
        Trace("Setting up grid scene...");

        // Load grid data
        _loadGridData();

        // Create the shared instance descriptor for all cubes
        _cubeInstanceDesc = _createCubeInstanceDesc();

        // Create the 7x12 grid of cubes
        for (int row = 0; row < GridRows; row++)
        {
            for (int col = 0; col < GridCols; col++)
            {
                string entityName = $"GridCube_{row}_{col}";
                _cubeGrid[row, col] = _engine.CreateEntity(entityName);
                _cubeGrid[row, col].Set(new Transform3ToWorld(0x00000001, Transform3ToWorld.Visible, Matrix4x4.Identity));
                _cubeGrid[row, col].Set(new Instance3(_cubeInstanceDesc));
            }
        }

        Trace($"Created {GridRows}x{GridCols} = {GridRows * GridCols} cubes");

        // Create camera - position it to see the entire grid
        _eCamera = _engine.CreateEntity("GridCamera");
        float gridWidth = GridCols * ColumnBaseSize;
        float gridDepth = GridRows * ColumnBaseSize;
        float cameraDistance = Math.Max(gridWidth, gridDepth) * 1.2f;
        Vector3 cameraPos = new Vector3(cameraDistance * 0.5f, cameraDistance * 0.6f, cameraDistance * 0.8f);
        Matrix4x4 cameraMatrix = _createLookAtMatrix(cameraPos, Vector3.Zero, Vector3.UnitY);

        _eCamera.Set(new Transform3ToWorld(0x00000001, Transform3ToWorld.Visible, cameraMatrix));
        _eCamera.Set(new Camera3
        {
            Angle = 60f,
            NearFrustum = 0.01f,
            FarFrustum = 100f,
            CameraMask = 0x00000001,
            CameraFlags = 0
        });
        _engine.Camera.Value = _eCamera;
        Trace($"Camera: position={cameraPos}");

        // Create directional light
        _eDirectionalLight = _engine.CreateEntity("GridDirectionalLight");
        _eDirectionalLight.Set(new Transform3ToWorld(0x00000001, Transform3ToWorld.Visible, Matrix4x4.Identity));
        _eDirectionalLight.Set(new DirectionalLight(new Vector4(0.8f, 0.8f, 0.8f, 1f)));

        // Create ambient light
        _eAmbientLight = _engine.CreateEntity("GridAmbientLight");
        _eAmbientLight.Set(new Transform3ToWorld(0x00000001, Transform3ToWorld.Visible, Matrix4x4.Identity));
        _eAmbientLight.Set(new AmbientLight(new Vector4(0.3f, 0.3f, 0.3f, 1f)));

        // Register for frame updates
        I.Get<SceneSequencer>().AddScene(0f, this);

        // Display the first frame
        _updateGridDisplay();

        Trace("Grid scene setup complete. Use Left/Right arrows to navigate frames.");
    }

    public void InputPartOnInputEvent(Event ev)
    {
        if (ev.Type == Event.INPUT_KEY_PRESSED)
        {
            switch (ev.Code)
            {
                case "(cursorright)":
                    _nextFrame();
                    ev.IsHandled = true;
                    break;
                case "(cursorleft)":
                    _previousFrame();
                    ev.IsHandled = true;
                    break;
            }
        }
    }

    public void SceneOnLogicalFrame(float dt)
    {
        // No rotation animation for the grid - it's static
    }

    public void SceneKickoff()
    {
        Trace("Grid scene kicked off!");
    }

    protected override void OnModuleActivate()
    {
        base.OnModuleActivate();
        Trace("Grid Scene activating...");

        // Register for input events
        I.Get<InputEventPipeline>().AddInputPart(InputZOrder, this);

        // Setup must run on the logical thread
        _engine.QueueMainThreadAction(_setupScene);
    }

    protected override void OnModuleDeactivate()
    {
        Trace("Grid Scene deactivating...");

        // Unregister from input events
        I.Get<InputEventPipeline>().RemoveInputPart(this);

        _engine.QueueMainThreadAction(() =>
        {
            I.Get<SceneSequencer>().RemoveScene(this);

            // Dispose all cube entities
            for (int row = 0; row < GridRows; row++)
            {
                for (int col = 0; col < GridCols; col++)
                {
                    if (_cubeGrid[row, col].IsAlive) _cubeGrid[row, col].Dispose();
                }
            }

            if (_eCamera.IsAlive) _eCamera.Dispose();
            if (_eDirectionalLight.IsAlive) _eDirectionalLight.Dispose();
            if (_eAmbientLight.IsAlive) _eAmbientLight.Dispose();
        });

        base.OnModuleDeactivate();
    }
}
