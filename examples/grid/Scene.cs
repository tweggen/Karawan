using System;
using System.Collections.Generic;
using System.Numerics;
using DefaultEcs;
using engine;
using engine.joyce;
using engine.joyce.components;

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

    /// <summary>
    /// Create the cube mesh with normals.
    /// </summary>
    private Mesh _createCubeMesh()
    {
        // Use the engine's built-in cube mesh creator
        var mesh = engine.joyce.mesh.Tools.CreateCubeMesh("GridCube", 1.0f);
        
        // The cube mesh from Tools doesn't have normals, so we need to add them
        // For a cube, normals point outward from each face
        // Since the mesh has 24 vertices (4 per face * 6 faces), we add normals per vertex
        mesh.Normals = new List<Vector3>();
        
        int verticesPerFace = 4;
        // Back (-Z)
        for (int i = 0; i < verticesPerFace; i++) mesh.Normals.Add(new Vector3(0, 0, -1));
        // Front (+Z)
        for (int i = 0; i < verticesPerFace; i++) mesh.Normals.Add(new Vector3(0, 0, 1));
        // Top (+Y)
        for (int i = 0; i < verticesPerFace; i++) mesh.Normals.Add(new Vector3(0, 1, 0));
        // Bottom (-Y)
        for (int i = 0; i < verticesPerFace; i++) mesh.Normals.Add(new Vector3(0, -1, 0));
        // Right (+X)
        for (int i = 0; i < verticesPerFace; i++) mesh.Normals.Add(new Vector3(1, 0, 0));
        // Left (-X)
        for (int i = 0; i < verticesPerFace; i++) mesh.Normals.Add(new Vector3(-1, 0, 0));
        
        return mesh;
    }
    
    /// <summary>
    /// Create the instance description for the cube (mesh + material).
    /// </summary>
    private InstanceDesc _createCubeInstanceDesc()
    {
        var mesh = _createCubeMesh();
        
        // Create a simple material with an explicit texture to avoid atlas lookup issues.
        // Use the built-in black texture as a base, the AlbedoColor uniform will provide the actual color.
        var material = new Material
        {
            AlbedoColor = 0xff808080,  // Gray color (ARGB format: full alpha, gray RGB)
            Texture = new Texture(Texture.BLACK)  // Explicit texture to avoid color->texture atlas lookup
        };
        
        // Build the InstanceDesc
        var meshes = new List<Mesh> { mesh };
        var meshMaterials = new List<int> { 0 };
        var materials = new List<Material> { material };
        var modelNodes = new List<ModelNode>();
        
        return new InstanceDesc(meshes, meshMaterials, materials, modelNodes, 100f);
    }

    /// <summary>
    /// Create a look-at matrix for the camera.
    /// </summary>
    private Matrix4x4 _createLookAtMatrix(Vector3 position, Vector3 target, Vector3 up)
    {
        // Create a view-style matrix but we need the inverse for the transform
        // (camera transform is where the camera IS, not what it sees)
        var forward = Vector3.Normalize(target - position);
        var right = Vector3.Normalize(Vector3.Cross(up, forward));
        var actualUp = Vector3.Cross(forward, right);
        
        // Build the camera's world matrix (position + orientation)
        return new Matrix4x4(
            right.X, right.Y, right.Z, 0,
            actualUp.X, actualUp.Y, actualUp.Z, 0,
            forward.X, forward.Y, forward.Z, 0,
            position.X, position.Y, position.Z, 1
        );
    }

    /// <summary>
    /// Set up the scene entities. Must be called from the logical thread.
    /// </summary>
    private void _setupScene()
    {
        // 1. Create the cube entity at origin
        _eCube = _engine.CreateEntity("GridCube");
        var cubeInstanceDesc = _createCubeInstanceDesc();
        
        _eCube.Set(new Transform3ToWorld(
            0x00000001,  // CameraMask
            Transform3ToWorld.Visible,  // Flags
            Matrix4x4.Identity  // At origin, no rotation
        ));
        _eCube.Set(new Instance3(cubeInstanceDesc));

        // 2. Create the camera
        _eCamera = _engine.CreateEntity("GridCamera");
        
        Vector3 cameraPosition = new Vector3(3f, 3f, 5f);
        Vector3 cameraTarget = Vector3.Zero;
        Matrix4x4 cameraMatrix = _createLookAtMatrix(cameraPosition, cameraTarget, Vector3.UnitY);
        
        _eCamera.Set(new Transform3ToWorld(
            0x00000001,  // CameraMask
            Transform3ToWorld.Visible,  // Flags
            cameraMatrix
        ));
        
        _eCamera.Set(new Camera3
        {
            Angle = 60f,
            NearFrustum = 0.1f,
            FarFrustum = 100f,
            CameraMask = 0x00000001,
            CameraFlags = 0
        });
        
        // Register the camera with the engine
        _engine.Camera.Value = _eCamera;

        // 3. Create directional light (sun-like)
        _eDirectionalLight = _engine.CreateEntity("GridDirectionalLight");
        
        // Light pointing down and to the side
        Vector3 lightDirection = Vector3.Normalize(new Vector3(1f, -1f, 1f));
        
        _eDirectionalLight.Set(new Transform3ToWorld(
            0x00000001,
            Transform3ToWorld.Visible,
            Matrix4x4.Identity
        ));
        
        _eDirectionalLight.Set(new DirectionalLight(
            new Vector4(0.8f, 0.8f, 0.8f, 1f)  // White light
        ));

        // 4. Create ambient light
        _eAmbientLight = _engine.CreateEntity("GridAmbientLight");
        
        _eAmbientLight.Set(new Transform3ToWorld(
            0x00000001,
            Transform3ToWorld.Visible,
            Matrix4x4.Identity
        ));
        
        _eAmbientLight.Set(new AmbientLight(
            new Vector4(0.3f, 0.3f, 0.3f, 1f)  // Ambient light
        ));
    }

    /// <summary>
    /// Called every logical frame to update the scene.
    /// </summary>
    public void SceneOnLogicalFrame(float dt)
    {
        // Rotate the cube around the Y axis
        _rotationAngle += RotationSpeed * dt;
        
        if (_eCube.IsAlive && _eCube.Has<Transform3ToWorld>())
        {
            var transform = _eCube.Get<Transform3ToWorld>();
            transform.Matrix = Matrix4x4.CreateRotationY(_rotationAngle);
            _eCube.Set(transform);
        }
    }

    /// <summary>
    /// Called when the scene becomes active.
    /// </summary>
    public void SceneKickoff()
    {
        // Scene is now running
    }

    protected override void OnModuleActivate()
    {
        base.OnModuleActivate();
        
        // Queue the scene setup to run on the logical thread
        _engine.QueueMainThreadAction(() =>
        {
            _setupScene();
            
            // Register this scene with the scene sequencer
            I.Get<SceneSequencer>().AddScene(0f, this);
        });
    }

    protected override void OnModuleDeactivate()
    {
        // Queue cleanup to run on the logical thread
        _engine.QueueMainThreadAction(() =>
        {
            // Clean up entities
            if (_eCube.IsAlive) _eCube.Dispose();
            if (_eCamera.IsAlive) _eCamera.Dispose();
            if (_eDirectionalLight.IsAlive) _eDirectionalLight.Dispose();
            if (_eAmbientLight.IsAlive) _eAmbientLight.Dispose();
        });
        
        base.OnModuleDeactivate();
    }
}
