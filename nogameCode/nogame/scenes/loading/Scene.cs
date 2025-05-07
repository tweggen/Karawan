using System;
using System.Numerics;
using engine;
using engine.draw;
using engine.joyce;

namespace nogame.scenes.loading;

public class Scene : AModule, IScene
{
    private engine.joyce.TransformApi _aTransform;

    public float YOffset { get; set; } = -5.0f;

    private DefaultEcs.Entity _eCamera;
    private DefaultEcs.Entity _eSpinner;
    private DefaultEcs.Entity _eLight;
    private DefaultEcs.Entity _eAmbientLight;
    private DefaultEcs.Entity _eLoadingText;
    
    private uint LoadingCamMask = 0x00000010; 
    
    private static Lazy<engine.joyce.InstanceDesc> _jMeshSpinner = new(
        () => InstanceDesc.CreateFromMatMesh(
            new MatMesh(
                new Material() { EmissiveTexture = I.Get<TextureCatalogue>().FindColorTexture(0xff226666) },
                engine.joyce.mesh.Tools.CreateCubeMesh($"loading 1 mesh", 1f)
            ),
            400f
        )
    );

    private static Lazy<LoadingSpinBehavior> _loadingSpinBehavior = new(() => new LoadingSpinBehavior());
    
    
    public void SceneOnLogicalFrame(float dt)
    {
    }


    public void SceneKickoff()
    {
    }
    

    protected override void OnModuleDeactivate()
    {
        _engine.AddDoomedEntity(_eCamera);
        _engine.AddDoomedEntity(_eLight);
        _engine.AddDoomedEntity(_eSpinner);
        _engine.AddDoomedEntity(_eLoadingText);
        _aTransform = null;
        
        /*
         * Null out everything we don't need when the scene is unloaded.
         */
        I.Get<SceneSequencer>().RemoveScene(this);
    }


    protected override void OnModuleActivate()
    {
        base.OnModuleActivate();
        
        _aTransform = I.Get<engine.joyce.TransformApi>();
        
        /*
         * Cube Spinner while loading.
         */
        _eSpinner = _engine.CreateEntity($"LoadingScene.MeshSpinner");
        _eSpinner.Set(new engine.joyce.components.Instance3(_jMeshSpinner.Value));
        I.Get<TransformApi>().SetTransforms(_eSpinner, true, LoadingCamMask, Quaternion.Identity, 
            Vector3.Zero,
            new Vector3(1, 1, 1));
        _eSpinner.Set(
            new engine.behave.components.Behavior(_loadingSpinBehavior.Value)
            {
                MaxDistance = 2000
            }
        );


        /*
         * Light while loading
         */
        {
            _eLight = _engine.CreateEntity("LoadingScene.PointLight");
            _eLight.Set(new engine.joyce.components.PointLight(
                new Vector4(1f, 1f, 1f, 1.0f), 15.0f));
            _aTransform.SetRotation(_eLight, 
                Quaternion.CreateFromAxisAngle(
                    new Vector3(0f, 1f, 0f), Single.Pi/2f));
            _aTransform.SetPosition(_eLight, new Vector3(0f, 0f, 10f));
        }

        //_eAmbientLight = _engine.CreateEntity("RootScene.AmbientLight");
        //_eAmbientLight.Set(new engine.joyce.components.AmbientLight(new Vector4(0.01f, 0.01f, 0.01f, 0.0f)));

        /*
         * Create a camera.
         */
        {
            _eCamera = _engine.CreateEntity("LoadingScene.Camera");
            var cCamera = new engine.joyce.components.Camera3();
            cCamera.Angle = 60.0f;
            cCamera.NearFrustum = 1f;

            /*
             * We need to be as far away as the skycube is. Plus a bonus.
             */
            cCamera.FarFrustum = (float)100f;
            cCamera.CameraMask = LoadingCamMask;
            cCamera.Renderbuffer = I.Get<ObjectRegistry<Renderbuffer>>().Get("rootscene_3d");
            _eCamera.Set(cCamera);
            _aTransform.SetVisible(_eCamera, true);
            _aTransform.SetCameraMask(_eCamera, LoadingCamMask);
            _aTransform.SetPosition(_eCamera, new Vector3(0f, 0f, 10f));
        }

        {
            _eLoadingText = _engine.CreateEntity("LoadingScene.LoadingText");
            _eLoadingText.Set(new engine.draw.components.OSDText(
                new Vector2(786f - 64f - 32f - 48f, 48 + 48f + 48f + 48f + YOffset),
                new Vector2(64f + 48f, 40f),
                $"loading...",
                12,
                0xff448822,
                0x00000000,
                HAlign.Right
            ));
        }

    }
}