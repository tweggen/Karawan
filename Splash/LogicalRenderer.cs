using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using engine;
using Splash.components;
using static engine.Logger;

namespace Splash;


public class LogicalRenderer
{
    private readonly object _lo = new();
    
    private readonly engine.Engine _engine;

    private readonly IThreeD _threeD;
    
    private systems.CreatePfInstanceSystem _createPfInstanceSystem;
    private systems.CreatePfRenderbufferSystem _createPfRenderbufferSystem;
    private systems.DrawInstancesSystem _drawInstancesSystem;
    private systems.DrawSkyboxesSystem _drawSkyboxesSystem;

    private uint _logicalFrameNumber;
    
    private readonly Queue<RenderFrame> _renderQueue = new();
    private bool _shallQuit;
    public bool ShallQuit
    {
        get
        {
            lock (_lo)
            {
                return _shallQuit;
            }
        }
        set
        {
            lock (_lo)
            {
                _shallQuit = value;
                System.Threading.Monitor.Pulse(_lo);
            }
            lock (_engine.ShortSleep)
            {   
                System.Threading.Monitor.Pulse(_engine.ShortSleep);
            }
        }
    }


    /**
     * Collect the output of the cameras for later rendering.
     */
    private void _logicalRenderFrame(engine.IScene scene, RenderFrame renderFrame)
    {
        var listCameras = _engine.GetEcsWorld().GetEntities()
            .With<engine.joyce.components.Camera3>()
            .With<engine.joyce.components.Transform3ToWorld>()
            .AsEnumerable().OrderBy(e => e.Get<engine.joyce.components.Camera3>().CameraMask);

        bool haveSkyboxPosition = false;

        List<RenderPart> listDirectRenders = new();
        
        foreach (var eCamera in listCameras)
        {
            RenderPart renderPart = new();
            var renderPartCamera3 = eCamera.Get<engine.joyce.components.Camera3>();
            if (eCamera.Has<PfRenderbuffer>())
            {
                renderPart.PfRenderbuffer = eCamera.Get<PfRenderbuffer>();
                if (renderPart.PfRenderbuffer.Renderbuffer == null ||
                    renderPart.PfRenderbuffer.RenderbufferEntry == null)
                {
                    Trace("No renderbuffer allocated yet.");
                    continue;
                }
            }
            else
            {
                /*
                 * It remains null.
                 */
            }

            var renderPartTransform3ToWorld = eCamera.Get<engine.joyce.components.Transform3ToWorld>();
            if (renderPartTransform3ToWorld.CameraMask == 0 || !renderPartTransform3ToWorld.IsVisible)
            {
                continue;
            }
            CameraOutput cameraOutput = new(
                scene, _threeD, 
                renderPartTransform3ToWorld.Matrix,
                renderPartCamera3, renderFrame.FrameStats);
            renderPart.CameraOutput = cameraOutput;
            
            /*
             * Now look, what kind of content comes from this camera.
             * Do we need to render meshes with this camera?
             */

            if (0 == (renderPartCamera3.CameraFlags & engine.joyce.components.Camera3.Flags.DontRenderInstances))
            {
                _drawInstancesSystem.Update(cameraOutput);
            }

            /*
             * Are we supposed to render skyboxes? 
             */
            if (0 != (renderPartCamera3.CameraFlags & engine.joyce.components.Camera3.Flags.RenderSkyboxes))
            {
                if (!haveSkyboxPosition)
                {
                    var vCameraPosition = renderPartTransform3ToWorld.Matrix.Translation;
                    _drawSkyboxesSystem.CameraPosition = vCameraPosition;
                    _drawSkyboxesSystem.Update(cameraOutput);
                    haveSkyboxPosition = true;
                }
            }

            /*
             * If this one renders into a buffer, add it to the list of renderparts now.
             * If it renders directly on screen, add it after the parts that render into
             * a buffer.
             */
            if (renderPartCamera3.Renderbuffer == null)
            {
                listDirectRenders.Add(renderPart);
            }
            else
            {
                renderFrame.RenderParts.Add(renderPart);
            }
        }

        /*
         * This way we first render to the buffers, then directly to the screen.
         */
        foreach (var renderPart in listDirectRenders)
        {
            renderFrame.RenderParts.Add(renderPart);
        }
    }

    
    /**
     * Called from the logical thread context every logical frame.
     * If behavior doesn't mess up.
     */
    public void CollectRenderData(engine.IScene scene)
    {
        /*
         * First the renderbuffers to give the GPU more latency.
         */
        _createPfRenderbufferSystem.Update(_engine);
        /*
         * Then prepare mesh uploads.
         */
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
            renderFrame.FrameNumber = ++_logicalFrameNumber;
            renderFrame.StartCollectTime = DateTime.Now;
            
            /*
            * Create/upload all resources that haven't been uploaded.
            */
            renderFrame.LightCollector.CollectLights();

            renderFrame.EndCollectTime = DateTime.Now;
            
            _logicalRenderFrame(scene, renderFrame);
            lock(_lo)
            {
                _renderQueue.Enqueue(renderFrame);
                System.Threading.Monitor.Pulse(_lo);
            }
        }
    }


    public RenderFrame WaitNextRenderFrame()
    {
        lock (_lo)
        {
            while (!_shallQuit)
            {
                if (_renderQueue.Count > 0)
                {
                    return _renderQueue.Dequeue();
                }
                System.Threading.Monitor.Wait(_lo);
            }
        }
        return null;
    }
    
    
    public LogicalRenderer()
    {
        _engine = I.Get<Engine>();
        _threeD = I.Get<IThreeD>();

        _engine.RunMainThread(() =>
        {
            _createPfInstanceSystem = new();
            _createPfRenderbufferSystem = new();
            _drawInstancesSystem = new();
            _drawSkyboxesSystem = new();
        });
    }
}