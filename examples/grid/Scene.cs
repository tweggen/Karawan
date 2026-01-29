using System;
using System.Collections.Generic;
using System.Numerics;
using DefaultEcs;
using engine;
using engine.joyce;
using engine.joyce.components;
using static engine.Logger;

namespace grid;

/// <summary>
/// A minimal scene with a rotating cube, camera, and lighting.
/// </summary>
public class Scene : AModule, IScene
{
    private Entity _eCube;
    private Entity _eCamera;
    private Entity _eDirectionalLight;
    private Entity _eAmbientLight;
    
    private float _rotationAngle = 0f;
    private const float RotationSpeed = 0.5f; // radians per second
    
    private int _frameCount = 0;

    /// <summary>
    /// Create the cube mesh with normals.
    /// </summary>
    private Mesh _createCubeMesh()
    {
        var mesh = engine.joyce.mesh.Tools.CreateCubeMesh("GridCube", 1.0f);
        
        // Add normals for each face (4 vertices per face * 6 faces = 24 normals)
        mesh.Normals = new List<Vector3>();
        
        int verticesPerFace = 4;
        for (int i = 0; i < verticesPerFace; i++) mesh.Normals.Add(new Vector3(0, 0, -1)); // Back
        for (int i = 0; i < verticesPerFace; i++) mesh.Normals.Add(new Vector3(0, 0, 1));  // Front
        for (int i = 0; i < verticesPerFace; i++) mesh.Normals.Add(new Vector3(0, 1, 0));  // Top
        for (int i = 0; i < verticesPerFace; i++) mesh.Normals.Add(new Vector3(0, -1, 0)); // Bottom
        for (int i = 0; i < verticesPerFace; i++) mesh.Normals.Add(new Vector3(1, 0, 0));  // Right
        for (int i = 0; i < verticesPerFace; i++) mesh.Normals.Add(new Vector3(-1, 0, 0)); // Left
        
        Trace($"Created cube mesh: {mesh.Vertices.Count} vertices, {mesh.Normals.Count} normals, {mesh.Indices.Count} indices");
        
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
            AlbedoColor = 0xff808080,  // Gray (ARGB)
            Texture = new Texture(Texture.BLACK)
        };
        
        Trace($"Created material: AlbedoColor=0x{material.AlbedoColor:X8}");
        
        var meshes = new List<Mesh> { mesh };
        var meshMaterials = new List<int> { 0 };
        var materials = new List<Material> { material };
        var modelNodes = new List<ModelNode>();
        
        return new InstanceDesc(meshes, meshMaterials, materials, modelNodes, 100f);
    }

    /// <summary>
    /// Create a camera world matrix from position looking at target.
    /// </summary>
    private Matrix4x4 _createLookAtMatrix(Vector3 position, Vector3 target, Vector3 up)
    {
        var forward = Vector3.Normalize(target - position);
        var right = Vector3.Normalize(Vector3.Cross(up, forward));
        var actualUp = Vector3.Cross(forward, right);
        
        return new Matrix4x4(
            right.X, right.Y, right.Z, 0,
            actualUp.X, actualUp.Y, actualUp.Z, 0,
            forward.X, forward.Y, forward.Z, 0,
            position.X, position.Y, position.Z, 1
        );
    }

    /// <summary>
    /// Set up the scene entities.
    /// </summary>
    private void _setupScene()
    {
        Trace("Setting up grid scene...");
        
        // 1. Create cube at origin
        _eCube = _engine.CreateEntity("GridCube");
        _eCube.Set(new Transform3ToWorld(0x00000001, Transform3ToWorld.Visible, Matrix4x4.Identity));
        _eCube.Set(new Instance3(_createCubeInstanceDesc()));
        Trace($"Cube: alive={_eCube.IsAlive}, Transform3ToWorld={_eCube.Has<Transform3ToWorld>()}, Instance3={_eCube.Has<Instance3>()}");

        // 2. Create camera
        _eCamera = _engine.CreateEntity("GridCamera");
        Vector3 cameraPos = new Vector3(3f, 3f, 5f);
        Matrix4x4 cameraMatrix = _createLookAtMatrix(cameraPos, Vector3.Zero, Vector3.UnitY);
        
        _eCamera.Set(new Transform3ToWorld(0x00000001, Transform3ToWorld.Visible, cameraMatrix));
        _eCamera.Set(new Camera3
        {
            Angle = 60f,
            NearFrustum = 0.1f,
            FarFrustum = 100f,
            CameraMask = 0x00000001,
            CameraFlags = 0
        });
        _engine.Camera.Value = _eCamera;
        Trace($"Camera: position={cameraPos}, registered with engine");

        // 3. Create directional light
        _eDirectionalLight = _engine.CreateEntity("GridDirectionalLight");
        _eDirectionalLight.Set(new Transform3ToWorld(0x00000001, Transform3ToWorld.Visible, Matrix4x4.Identity));
        _eDirectionalLight.Set(new DirectionalLight(new Vector4(0.8f, 0.8f, 0.8f, 1f)));

        // 4. Create ambient light
        _eAmbientLight = _engine.CreateEntity("GridAmbientLight");
        _eAmbientLight.Set(new Transform3ToWorld(0x00000001, Transform3ToWorld.Visible, Matrix4x4.Identity));
        _eAmbientLight.Set(new AmbientLight(new Vector4(0.3f, 0.3f, 0.3f, 1f)));
        
        // Register for frame updates
        I.Get<SceneSequencer>().AddScene(0f, this);
        
        Trace("Grid scene setup complete.");
    }

    public void SceneOnLogicalFrame(float dt)
    {
        _frameCount++;
        _rotationAngle += RotationSpeed * dt;
        
        if (_eCube.IsAlive && _eCube.Has<Transform3ToWorld>())
        {
            var transform = _eCube.Get<Transform3ToWorld>();
            transform.Matrix = Matrix4x4.CreateRotationY(_rotationAngle);
            _eCube.Set(transform);
        }
        
        // Diagnostic logging every ~1 second
        if (_frameCount % 60 == 1)
        {
            //bool hasPfInstance = _eCube.IsAlive && _eCube.Has<Splash.components.PfInstance>();
            //Trace($"Frame {_frameCount}: hasPfInstance={hasPfInstance}, rotation={_rotationAngle:F2}");
            int a = 1;
        }
    }

    public void SceneKickoff()
    {
        Trace("Grid scene kicked off!");
    }

    protected override void OnModuleActivate()
    {
        base.OnModuleActivate();
        Trace("Grid Scene activating...");
        
        // Setup must run on the logical thread
        _engine.QueueMainThreadAction(_setupScene);
    }

    protected override void OnModuleDeactivate()
    {
        Trace("Grid Scene deactivating...");
        
        _engine.QueueMainThreadAction(() =>
        {
            I.Get<SceneSequencer>().RemoveScene(this);
            if (_eCube.IsAlive) _eCube.Dispose();
            if (_eCamera.IsAlive) _eCamera.Dispose();
            if (_eDirectionalLight.IsAlive) _eDirectionalLight.Dispose();
            if (_eAmbientLight.IsAlive) _eAmbientLight.Dispose();
        });
        
        base.OnModuleDeactivate();
    }
}
