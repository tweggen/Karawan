using System;
using System.Collections.Generic;
using System.Numerics;
using engine;
using engine.joyce;
using engine.joyce.components;

namespace grid;

/// <summary>
/// Minimal scene that creates:
/// - A 1x1m cube at the origin
/// - A camera 5m away looking at the origin
/// - A directional light
/// - An ambient light
/// </summary>
public class Scene : AModule, IScene
{
    private DefaultEcs.Entity _eCube;
    private DefaultEcs.Entity _eCamera;
    private DefaultEcs.Entity _eDirectionalLight;
    private DefaultEcs.Entity _eAmbientLight;

    private TransformApi _aTransform;

    public override IEnumerable<IModuleDependency> ModuleDepends() => new List<IModuleDependency>();

    /// <summary>
    /// Create a simple cube mesh with colored faces.
    /// </summary>
    private InstanceDesc _createCubeInstanceDesc()
    {
        // Create a 1x1m cube mesh
        var mesh = engine.joyce.mesh.Tools.CreateCubeMesh("grid.cube", 1.0f);
        mesh.GenerateNormals();

        // Create a simple material (gray color)
        var material = new Material()
        {
            AlbedoColor = 0xff808080, // Gray
            EmissiveColor = 0xff000000,
        };

        // Combine mesh and material
        var matmesh = new MatMesh(material, mesh);

        // Create instance description
        var instanceDesc = new InstanceDesc()
        {
            Meshes = new List<MatMesh>() { matmesh }
        };

        return instanceDesc;
    }

    /// <summary>
    /// Create the 3D entities for this scene.
    /// </summary>
    private void _createEntities()
    {
        _aTransform = I.Get<TransformApi>();

        // 1. Create the cube at the origin
        {
            _eCube = _engine.CreateEntity("Grid.Cube");
            
            var instanceDesc = _createCubeInstanceDesc();
            
            // Add the Instance3 component to make it visible
            _eCube.Set(new Instance3(instanceDesc));
            
            // Position at origin (default)
            _aTransform.SetTransforms(_eCube, 
                true, 0x00000001,  // visible, camera mask
                Quaternion.Identity,
                Vector3.Zero);
        }

        // 2. Create the directional light
        {
            _eDirectionalLight = _engine.CreateEntity("Grid.DirectionalLight");
            
            // White-ish directional light
            _eDirectionalLight.Set(new DirectionalLight(new Vector4(0.8f, 0.8f, 0.8f, 1.0f)));
            
            // Rotate the light to come from above and to the side
            // This creates a nice angle that shows the cube's 3D shape
            var lightRotation = Quaternion.CreateFromYawPitchRoll(
                45f * MathF.PI / 180f,  // Yaw (around Y)
                -45f * MathF.PI / 180f, // Pitch (around X) - negative to come from above
                0f);                     // Roll (around Z)
            _aTransform.SetRotation(_eDirectionalLight, lightRotation);
        }

        // 3. Create ambient light
        {
            _eAmbientLight = _engine.CreateEntity("Grid.AmbientLight");
            
            // Dim ambient light so shadows are visible
            _eAmbientLight.Set(new AmbientLight(new Vector4(0.2f, 0.2f, 0.2f, 1.0f)));
        }

        // 4. Create the camera
        {
            _eCamera = _engine.CreateEntity("Grid.Camera");
            
            var camera = new Camera3()
            {
                Angle = 60.0f,
                NearFrustum = 0.1f,
                FarFrustum = 100f,
                CameraFlags = Camera3.Flags.None,
                CameraMask = 0x00000001,
                Renderbuffer = I.Get<ObjectRegistry<Renderbuffer>>().Get("main_3d")
            };
            _eCamera.Set(camera);
            
            // Position camera 5m away, looking at origin
            // Place it at an angle so we can see multiple faces of the cube
            Vector3 cameraPosition = new Vector3(3f, 3f, 5f);
            _aTransform.SetPosition(_eCamera, cameraPosition);
            
            // Calculate rotation to look at origin
            Vector3 lookDirection = Vector3.Normalize(Vector3.Zero - cameraPosition);
            Vector3 up = Vector3.UnitY;
            
            // Create look-at rotation
            // The camera looks along -Z in its local space, so we need to rotate accordingly
            var lookRotation = _createLookAtRotation(cameraPosition, Vector3.Zero, up);
            _aTransform.SetRotation(_eCamera, lookRotation);
            
            _aTransform.SetCameraMask(_eCamera, 0x00000001);
            _aTransform.SetVisible(_eCamera, true);
        }

        // Set this camera as the engine's main camera
        _engine.Camera.Value = _eCamera;
    }

    /// <summary>
    /// Create a rotation quaternion that makes an object at 'from' look at 'to'.
    /// </summary>
    private Quaternion _createLookAtRotation(Vector3 from, Vector3 to, Vector3 up)
    {
        Vector3 forward = Vector3.Normalize(to - from);
        
        // Handle the case where forward is parallel to up
        if (MathF.Abs(Vector3.Dot(forward, up)) > 0.999f)
        {
            up = Vector3.UnitX;
        }

        Vector3 right = Vector3.Normalize(Vector3.Cross(up, forward));
        Vector3 correctedUp = Vector3.Cross(forward, right);

        // Build rotation matrix
        Matrix4x4 rotationMatrix = new Matrix4x4(
            right.X, right.Y, right.Z, 0,
            correctedUp.X, correctedUp.Y, correctedUp.Z, 0,
            forward.X, forward.Y, forward.Z, 0,
            0, 0, 0, 1
        );

        return Quaternion.CreateFromRotationMatrix(rotationMatrix);
    }

    public void SceneOnLogicalFrame(float dt)
    {
        // Optional: Rotate the cube slowly for visual interest
        if (_eCube.IsAlive)
        {
            var currentRotation = _aTransform.GetRotation(_eCube);
            var deltaRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, dt * 0.5f);
            _aTransform.SetRotation(_eCube, currentRotation * deltaRotation);
        }
    }

    public void SceneKickoff()
    {
        // Nothing special needed for kickoff
    }

    protected override void OnModuleActivate()
    {
        _createEntities();
        
        // Register this scene with the scene sequencer
        I.Get<SceneSequencer>().AddScene(0, this);
    }

    protected override void OnModuleDeactivate()
    {
        I.Get<SceneSequencer>().RemoveScene(this);
        
        // Clean up entities
        if (_eCube.IsAlive) _eCube.Dispose();
        if (_eCamera.IsAlive) _eCamera.Dispose();
        if (_eDirectionalLight.IsAlive) _eDirectionalLight.Dispose();
        if (_eAmbientLight.IsAlive) _eAmbientLight.Dispose();
    }
}
