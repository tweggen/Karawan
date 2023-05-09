using System.Collections.Generic;


namespace Splash;


public class LogicalRenderer
{
    private readonly object _lo = new();
    
    private readonly engine.Engine _engine;

    private readonly IThreeD _threeD;
    private readonly LightManager _lightManager;
    
    private readonly systems.CreatePfInstanceSystem _createPfInstanceSystem;
    private readonly systems.DrawInstancesSystem _drawInstancesSystem;
    private readonly systems.DrawSkyboxesSystem _drawSkyboxesSystem;

    private readonly Queue<RenderFrame> _renderQueue = new();


    /**
     * Collect the output of the cameras for later rendering.
     */
    private void _logicalRenderFrame(RenderFrame renderFrame)
    {
        var listCameras = _engine.GetEcsWorld().GetEntities()
            .With<engine.joyce.components.Camera3>()
            .With<engine.transform.components.Transform3ToWorld>()
            .AsEnumerable();

        foreach (var eCamera in listCameras)
        {
            RenderPart renderPart = new();
            renderPart.Camera3 = eCamera.Get<engine.joyce.components.Camera3>();
            renderPart.Transform3ToWorld = eCamera.Get<engine.transform.components.Transform3ToWorld>();
            CameraOutput cameraOutput = new(_threeD, renderPart.Camera3.CameraMask);
            renderPart.CameraOutput = cameraOutput;

            _drawInstancesSystem.Update(cameraOutput);

            var vCameraPosition = renderPart.Transform3ToWorld.Matrix.Translation;
            _drawSkyboxesSystem.CameraPosition = vCameraPosition;
            _drawSkyboxesSystem.Update(cameraOutput);

            renderFrame.RenderParts.Add(renderPart);
        }
    }

    
    /**
     * Called from the logical thread context every logical frame.
     * If behavior doesn't mess up.
     */
    public void CollectRenderData()
    {
        _createPfInstanceSystem.Update(_engine);

        RenderFrame renderFrame = null;
        /*
         * If we currently are not rendering, collect the data for the next 
         * rendering job. The entity system can only be read from this thread.
         */
        lock(_lo)
        {
            /*
             * Append a new render job if there is nothing to render.
             */
            if (_renderQueue.Count == 0)
            {
                renderFrame = new();
            }
        }

        if(null != renderFrame)
        {
            /*
             * Create/upload all ressources that haven't been uploaded.
             */
            lock (_lightManager)
            {
                _lightManager.CollectLights(renderFrame);
            }
            _logicalRenderFrame(renderFrame);
            lock(_lo)
            {
                _renderQueue.Enqueue(renderFrame);
            }
        }
    }


    public RenderFrame DequeueRenderFrame()
    {
        lock (_lo)
        {
            if (_renderQueue.Count > 0)
            {
                return _renderQueue.Dequeue();
            }
        }

        return null;
    }
    
    
    public LogicalRenderer(
        in engine.Engine engine,
        in IThreeD threeD,
        in LightManager lightManager
    )
    {
        _engine = engine;
        _threeD = threeD;
        _lightManager = lightManager;

        _createPfInstanceSystem = new(_engine);
        _drawInstancesSystem = new(_engine, _threeD);
        _drawSkyboxesSystem = new(_engine);
    }
}