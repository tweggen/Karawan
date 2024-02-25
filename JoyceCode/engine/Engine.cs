using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using BepuPhysics;
using DefaultEcs;
using engine.behave.components;
using engine.elevation;
using engine.news;
using engine.Resource;
using engine.world;
using static engine.Logger;
using Trace = System.Diagnostics.Trace;

namespace engine;


class EntitySetupAction
{
    public string EntityName;
    public Action<DefaultEcs.Entity> SetupAction;
}


public enum GamePlayStates
{
    Running,
    Paused
}


public class Engine
{
    private object _lo = new();

    public object ShortSleep = new();
    
    private int _nextId = 0;

    private DefaultEcs.World _ecsWorld;

    private engine.physics.API _aPhysics;
    private engine.joyce.TransformApi _aTransform;
    private engine.joyce.HierarchyApi _aHierarchy;

    private DefaultEcs.Command.EntityCommandRecorder _entityCommandRecorder;
    private List<IList<DefaultEcs.Entity>> _listDoomedEntityLists = new();


    private int _requireEntityIdArrays = 0;
    private DefaultEcs.Entity[] _arrayEntityIds = new DefaultEcs.Entity[0];

    public DefaultEcs.Entity[] Entities
    {
        get
        {
            lock (_lo)
            {
                return _arrayEntityIds;
            }
        }
    }

    private IPlatform _platform;

    private behave.systems.BehaviorSystem _systemBehave;
    private physics.systems.ApplyPosesSystem _systemApplyPoses;
    private physics.systems.MoveKineticsSystem _systemMoveKinetics;
    private audio.systems.MovingSoundsSystem _systemMovingSounds;

    private IReadOnlyCollection<IModule> _roListModules = null;
    private List<IModule> _listModules = new();

    private readonly Queue<EntitySetupAction> _queueEntitySetupActions = new();

    private Thread _logicalThread;
    private readonly Stopwatch _queueStopwatch = new();

    private readonly WorkerQueue _workerCleanupActions = new("engine.Engine.Cleanup");
    private readonly WorkerQueue _workerMainThreadActions = new("engine.Engine.MainThread");

    private bool _isFullscreen = false;

    private physics.Manager _managerPhysics;
    private gongzuo.LuaScriptManager _managerLuaScript;
    private builtin.map.MapIconManager _managerMapIcons;
    private behave.Manager _managerBehavior;
    private SceneSequencer _sceneSequencer;

    public event EventHandler<float> OnLogicalFrame;
    public event EventHandler<float> OnPhysicalFrame;

    public event EventHandler<float> OnImGuiRender;

    private Entity _cameraEntity;
    public event EventHandler<DefaultEcs.Entity> OnCameraEntityChanged;
    private Entity _playerEntity;
    public event EventHandler<DefaultEcs.Entity> OnPlayerEntityChanged;


    private builtin.tools.FPSMonitor _fpsPhysicalMonitor = new("physical");
    private builtin.tools.FPSMonitor _fpsLogicalMonitor = new("logical");

    public int NFrameDurations = 200;

    private Queue<float> _frameDurationQueue = new();

    
    public float[] FrameDurations
    {
        get
        {
            lock (_lo)
            {
                return _frameDurationQueue.ToArray();
            }
        }
    }

    
    private bool _platformIsAvailable = false;
    private bool _isRunning = true;

    public BepuPhysics.Simulation Simulation
    {
        get => _aPhysics.Simulation;
    }

    public enum EngineState
    {
        Initialized,
        Starting,
        Running,
        Stopping,
        Stopped
    };

    public EngineState State { get; private set; }
    public event EventHandler<EngineState> EngineStateChanged;


    private GamePlayStates _gamePlayState = GamePlayStates.Running;

    public GamePlayStates GamePlayState
    {
        get =>_gamePlayState;
        set
        {
            lock (_lo)
            {
                if (_gamePlayState == value)
                {
                    return;
                }

                _gamePlayState = value;
            }

            OnGamePlayStateChanged?.Invoke(this, value);
        }
    }

    public EventHandler<GamePlayStates> OnGamePlayStateChanged;


    public void SetEngineState(in EngineState newState)
    {
        bool isChanged = false;
        lock (_lo)
        {
            if (newState != State)
            {
                State = newState;
                isChanged = true;
            }
        }

        if (isChanged)
        {
            EngineStateChanged?.Invoke(this, newState);
        }
    }


    Vector2 _vViewUl = Vector2.Zero;
    Vector2 _vViewLr = Vector2.Zero;


    private int _isLoading = 0;

    public void SuggestBeginLoading()
    {
        lock (_lo)
        {
            ++_isLoading;
        }
    }

    public void SuggestEndLoading()
    {
        lock (_lo)
        {
            --_isLoading;
        }
    }


    public void Suspend()
    {
        I.Get<EventQueue>().Push(new Event("lifecycle.suspend", ""));
    }


    public void Resume()
    {
        I.Get<EventQueue>().Push(new Event("lifecycle.suspend", "user call"));
    }


    public Entity GetCameraEntity()
    {
        lock (_lo)
        {
            return _cameraEntity;
        }
    }


    public Entity GetPlayerEntity()
    {
        lock (_lo)
        {
            return _playerEntity;
        }
    }


    public void BeamTo(Vector3 vPos)
    {
        lock (_lo)
        {
            var pref = _playerEntity.Get<engine.physics.components.Body>().Reference;
            pref.Pose.Position = vPos;
            pref.Pose.Orientation = Quaternion.Identity;
        }
    }


    public void SetCameraEntity(in DefaultEcs.Entity entity)
    {
        bool entityChanged = false;
        lock (_lo)
        {
            if (_cameraEntity != entity)
            {
                entityChanged = true;
                _cameraEntity = entity;
            }
        }

        if (entityChanged)
        {
            OnCameraEntityChanged?.Invoke(this, entity);
        }
    }


    public void SetPlayerEntity(in DefaultEcs.Entity entity)
    {
        bool entityChanged = false;
        lock (_lo)
        {
            if (_playerEntity != entity)
            {
                entityChanged = true;
                _playerEntity = entity;
            }
        }

        if (entityChanged)
        {
            OnPlayerEntityChanged?.Invoke(this, entity);
        }
    }


    public int GetNextId()
    {
        lock (_lo)
        {
            return ++_nextId;
        }
    }


    public DefaultEcs.World GetEcsWorld()
    {
        return _ecsWorld;
    }


    public DefaultEcs.Command.WorldRecord GetEcsWorldRecord()
    {
        return _entityCommandRecorder.Record(_ecsWorld);
    }


    public void ApplyEcsRecorder(in DefaultEcs.Command.EntityCommandRecorder recorder)
    {
        recorder.Execute();
    }


    private void _commitWorldRecord()
    {
        _entityCommandRecorder.Execute();
    }


    private void _executeDoomedEntities()
    {
        List<IList<DefaultEcs.Entity>> listList;
        lock (_lo)
        {
            if (_listDoomedEntityLists.Count == 0)
            {
                return;
            }

            listList = _listDoomedEntityLists;
            _listDoomedEntityLists = new();
        }

        if (null == listList)
        {
            return;
        }

        foreach (var list in listList)
        {
            foreach (var entity in list)
            {
                entity.Dispose();
            }
        }
    }


    public void AddDoomedEntities(in IList<DefaultEcs.Entity> listDoomedEntities)
    {
        lock (_lo)
        {
            _listDoomedEntityLists.Add(listDoomedEntities);
        }
    }


    public void AddDoomedEntity(DefaultEcs.Entity entity)
    {
        lock (_lo)
        {
            List<Entity> listEntity = new List<Entity>();
            listEntity.Add(entity);
            _listDoomedEntityLists.Add(listEntity);
        }
    }


    public DefaultEcs.Entity CreateEntity(string name)
    {
        DefaultEcs.Entity entity = _ecsWorld.CreateEntity();
        entity.Set(new joyce.components.EntityName(name));
        return entity;
    }


    private void _executeEntitySetupActions(float matTime)
    {
        _queueStopwatch.Reset();
        _queueStopwatch.Start();
        while (_queueStopwatch.Elapsed.TotalMilliseconds < matTime * 1000f)
        {
            EntitySetupAction entitySetupAction;
            lock (_lo)
            {
                if (_queueEntitySetupActions.Count == 0)
                {
                    break;
                }

                entitySetupAction = _queueEntitySetupActions.Dequeue();
            }

            DefaultEcs.Entity entity = CreateEntity(entitySetupAction.EntityName);
            try
            {
                entitySetupAction.SetupAction(entity);
            }
            catch (Exception e)
            {
                Warning($"Error executing entity setup action: {e}.");
                entity.Dispose();
            }
        }

        _queueStopwatch.Stop();

        int queueLeft;
        lock (_lo)
        {
            queueLeft = _queueEntitySetupActions.Count;
        }

        if (0 < queueLeft)
        {
            Trace($"Left {queueLeft} items in setup actions queue.");
        }

    }


    public void QueueEntitySetupAction(
        string entityName, Action<DefaultEcs.Entity> setupAction)
    {
        lock (_lo)
        {
            _queueEntitySetupActions.Enqueue(
                new EntitySetupAction
                {
                    EntityName = entityName,
                    SetupAction = setupAction
                }
            );
        }
    }


    public void QueueCleanupAction(Action action)
    {
        _workerCleanupActions.Enqueue(action);
    }


    public void QueueMainThreadAction(Action action)
    {
        _workerMainThreadActions.Enqueue(action);
    }


    /**
     * Called by the platform on a new physical frame.
     */
    public void CallOnPhysicalFrame(float dt)
    {
        OnPhysicalFrame?.Invoke(this, dt);

        lock (_lo)
        {
            /*
             * Compute a running average of fps.
             */
            _fpsPhysicalMonitor.OnFrame(dt);
            while (_frameDurationQueue.Count >= NFrameDurations)
            {
                _frameDurationQueue.Dequeue();
            }

            _frameDurationQueue.Enqueue(dt);
        }
    }


    /**
     * Called by the platform as soon we believe the
     * platform APIs are available.
     */
    public void CallOnPlatformAvailable()
    {
        lock (_lo)
        {
            _platformIsAvailable = true;
        }
    }


    /**
     * Track, if this is the first call to the onLogicalFrame function.
     */
    private bool _firstTime = true;

    private void _onLogicalFrame(float dt)
    {
        EngineState engineState;
        GamePlayStates gamePlayState;
        _fpsLogicalMonitor.OnFrame(dt);

        lock (_lo)
        {
            engineState = State;
            gamePlayState = _gamePlayState;
        }

        /*
         * If the engine is stopped, do not do any logical frame stuff.
         */
        if (engineState == EngineState.Stopped)
        {
            return;
        }

        /*
         * Before rendering the first time and calling user handlers the first time,
         * we need to read physics to transforms, update hierarchy and transforms.
         * That way user handlers have the transform2world available.
         */
        if (_firstTime)
        {
            _firstTime = false;

            /*
             * Apply poses needs input from simulation
             */
            _systemApplyPoses.Update(dt);

            /*
             * hierarchy needs
             * - input from user handlers
             */
            _aHierarchy.Update();

            /*
             * transform system needs
             * - updated hierarchy system
             * - input from user handlers
             * - input from physics
             */
            _aTransform.Update();

            /*
             * Move kinetics re quires
             * - input from user, already processed by Transform System
             */
            _systemMoveKinetics.Update(dt);
        }

        /*
         * Goal: shortest latency from user input to screen.
         */

        /*
         * First collect and execute all events, so that they can
         * affect behaviours.
         */
        {
            var eq = I.Get<EventQueue>();
            var sm = I.Get<SubscriptionManager>();
            while (!eq.IsEmpty())
            {
                Event ev = eq.Pop();
                sm.Handle(ev);
            }
        }

        /*
         * Call the various ways of user behavior and/or controllers.
         * They will read world position and modify physics and or positions
         *
         * Require: Previously computed world transforms.
         */
        if (gamePlayState == GamePlayStates.Running) _systemBehave.Update(dt);

        OnLogicalFrame?.Invoke(this, dt);

        /*
         * After everything has behaved, read the camera(s) to get
         * the camera positions for further processing.
         */
        var vCameraPosition = new Vector3(0f, 0f, 0f);
        var vCameraRight = new Vector3(1f, 1f, 0f);
        var listCameras = GetEcsWorld().GetEntities()
            .With<engine.joyce.components.Camera3>()
            .With<engine.joyce.components.Transform3ToWorld>()
            .AsEnumerable();
        foreach (var eCamera in listCameras)
        {
            var cCamera3 = eCamera.Get<engine.joyce.components.Camera3>();
            var cTransform3ToWorld = eCamera.Get<engine.joyce.components.Transform3ToWorld>();
            var mToWorld = cTransform3ToWorld.Matrix;

            vCameraPosition = cTransform3ToWorld.Matrix.Translation;
            vCameraRight = new Vector3(mToWorld.M11, mToWorld.M12, mToWorld.M13);

            _systemMovingSounds.SetListenerPosRight(vCameraPosition, vCameraRight);

            break;
        }

        /*
         * We can update moving sounds only after the behaviour has defined
         * the velocities.
         */
        _systemMovingSounds.Update(dt);


        if (gamePlayState == GamePlayStates.Running) {
            /*
             * Advance physics, based on new user input and/or gravitation.
             */
            _aPhysics.Update(dt);
        }

        /*
         * Apply poses needs input from simulation
         */
        _systemApplyPoses.Update(dt);

        /*
         * hierarchy needs
         * - input from user handlers
         */
        _aHierarchy.Update();

        /*
         * transform system needs
         * - updated hierarchy system
         * - input from user handlers
         * - input from physics
         */
        _aTransform.Update();

        /*
         * Move kinetics requires
         * - input from user, already processed by Transform System
         */
        _systemMoveKinetics.Update(dt);

        /*
         * Write back all entity modifications to the objects.
         */
        _commitWorldRecord();

        /*
         * If no new frame has been created, read all geom entities for rendering
         * into data structures.
         */
        if (null != _sceneSequencer.MainScene)
        {
            _platform.CollectRenderData(_sceneSequencer.MainScene);
        }
        else
        {
            // ErrorThrow("Null scene", (m) => new InvalidOperationException(m));
        }

        // TXWTODO: Measure the time of all actions.
        /*
         * Async delete any entities that shall be deleted
         */
        _executeDoomedEntities();

        /*
         * Async create / setup new entities.
         */
        _executeEntitySetupActions(
            (_isLoading > 0)
                ? 0.024f
                : 0.001f
        );
        _workerMainThreadActions.RunPart(
            (_isLoading > 0)
                ? 0.004f
                : 0.001f
        );
        _workerCleanupActions.RunPart(0.001f);
    }


    public IReadOnlyCollection<IModule> GetModules()
    {
        lock (_lo)
        {
            if (null == _roListModules)
            {
                _roListModules = _listModules.ToImmutableList();
            }
            return new List<IModule>(_listModules);
        }
    }
    

    public bool HasModule(in IModule module)
    {
        lock (_lo)
        {
            return _listModules.Contains(module);
        }
    }
    

    public void AddModule(in IModule module)
    {
        lock (_lo)
        {
            _roListModules = null;
            _listModules.Add(module);
        }
    }


    public void RemoveModule(in IModule module)
    {
        lock (_lo)
        {
            _roListModules = null;
            _listModules.Remove(module);
        }
    }


    public void CallOnImGuiRender(float dt)
    {
        OnImGuiRender?.Invoke(this, dt);
    }

    
    public void Execute()
    {
        _platform.Execute();
    }


    public bool IsFullscreen()
    {
        lock (_lo)
        {
            return _isFullscreen;
        }
    }


    public void SetFullscreen(bool isFullscreen)
    {
        IPlatform platform = null;
        lock (_lo)
        {
            _isFullscreen = isFullscreen;
            platform = _platform;
        }

        if (null != platform)
        {
            platform.SetFullscreen(isFullscreen);
        }
    }

    /*
     * The entity array, if requested, is updated every _entityUpdateRate logical frames.
     * In the given configuration, it is updated every 20ms.
     */
    private int _entityUpdateRate = 12;
    private int _entityUpdateCount = 0;

    private void _checkUpdateEntityArray()
    {
        lock (_lo)
        {
            if (_requireEntityIdArrays > 0)
            {
                if (_entityUpdateCount <= 0)
                {
                    _entityUpdateCount = _entityUpdateRate;
                    var entities = GetEcsWorld().GetEntities().AsEnumerable();
                    _arrayEntityIds = new DefaultEcs.Entity[entities.Count()];
                    int idx = 0;
                    foreach (var entity in GetEcsWorld().GetEntities().AsEnumerable())
                    {
                        _arrayEntityIds[idx] = entity;
                        idx++;
                    }
                }
                else
                {
                    _entityUpdateCount--;
                }
            }
        }
    }


    private void _logicalThreadFunction()
    {
        float invFps = 1f / 60f;
        float accumulator = 0f;

        float toWait = 0f;
        int nSleeps = 0;

        Stopwatch stopWatch = new Stopwatch();
        stopWatch.Start();
        while (_platform.IsRunning())
        {
            EngineState engineState;

            if (toWait > 0f && stopWatch.Elapsed.TotalSeconds < toWait)
            {
                //Thread.Yield();
                // Thread.Sleep(1);
                lock (ShortSleep)
                {
                    System.Threading.Monitor.Wait(ShortSleep,1);
                    nSleeps++;
                }
                continue;
            }

            stopWatch.Stop();
            accumulator += (float) stopWatch.Elapsed.TotalSeconds;
            stopWatch.Reset();

            stopWatch.Start();
            int nLogical = 0;
            
            /*
             * Run as many logical frames as have been elapsed.
             */
            while (accumulator > invFps)
            {
                lock (_lo)
                {
                    engineState = State;
                    if (engineState > EngineState.Running)
                    {
                        break;
                    }
                }

                if (_platformIsAvailable)
                {
                    _onLogicalFrame(invFps);
                    ++nLogical;
                }
                
                // Trace($"accu {accumulator}");

                /*
                 * And subtract the logical advance.
                 */
                accumulator -= invFps;
            }


            _fpsPhysicalMonitor.Update();
            _fpsLogicalMonitor.Update();
 
            stopWatch.Stop();
            float processedTime = (float)stopWatch.Elapsed.TotalSeconds;
            stopWatch.Reset();

            accumulator += processedTime;
            toWait = invFps - accumulator;
            nSleeps = 0;

            _checkUpdateEntityArray();
            stopWatch.Start();
        }

    }


    public void Exit()
    {
        lock (_lo)
        {
            _isRunning = false;
        }
    }


    /**
     * Call after all dependencies are set.
     */
    public void SetupDone()
    {
        _aPhysics = I.Get<engine.physics.API>();
        _aTransform = I.Get<engine.joyce.TransformApi>();
        _aHierarchy = I.Get<engine.joyce.HierarchyApi>();

        _systemBehave = new(this);
        _systemApplyPoses = new(this);
        _systemMoveKinetics = new(this);
        _systemMovingSounds = new(this);
        _managerPhysics = new physics.Manager();
        _managerPhysics.Manage(this);
        _managerBehavior = new behave.Manager();
        _managerBehavior.Manage(this);
        _managerLuaScript = new();
        _managerLuaScript.Manage(_ecsWorld);
        _managerMapIcons = new();
        _managerMapIcons.Manage(_ecsWorld);

        _logicalThread = new Thread(_logicalThreadFunction);
        _logicalThread.Priority = ThreadPriority.AboveNormal;
        I.Get<InputEventPipeline>().ModuleActivate(this);
    }


    public bool IsRunning()
    {
        bool platformIsRunning = _platform.IsRunning();
        lock (_lo)
        {
            return _isRunning && platformIsRunning;
        }
    }


    private int _enableMouseCounter = 0;

    public void EnableMouse()
    {
        bool doEnable = false;
        lock (_lo)
        {
            ++_enableMouseCounter;
            if (1 == _enableMouseCounter)
            {
                doEnable = true;
            }
        }

        if (doEnable)
        {
            _platform.MouseEnabled = true;
        }
    }


    public void DisableMouse()
    {
        bool doDisable = false;
        lock (_lo)
        {
            if (0 == _enableMouseCounter)
            {
                ErrorThrow("Mismatch disabling mouse.", (m) => new InvalidOperationException(m));
            }

            if (1 == _enableMouseCounter)
            {
                doDisable = true;
            }

            --_enableMouseCounter;
        }

        if (doDisable)
        {
            _platform.MouseEnabled = false;
        }
    }


    public void EnableEntityIds()
    {
        lock (_lo)
        {
            _requireEntityIdArrays++;
        }
    }


    public void DisableEntityIds()
    {
        lock (_lo)
        {
            _requireEntityIdArrays--;
        }
    }


    /**
     * Load default resources into the resource cache.
     * These might be required ot be availabel even if the
     * platform still is loading.
     */
    private void _loadDefaultResources()
    {
        /*
         * Load some default resources.
         */
        try
        {
            Trace("Loading default resources...");
            I.Get<Resources>().FindAdd("shaders/default.vert", (string _) => new ShaderSource("LIghtingVS.vert"));
            I.Get<Resources>().FindAdd("shaders/default.frag", (string _) => new ShaderSource("LIghtingFS.frag"));
            I.Get<Resources>().FindAdd("shaders/screen.frag", (string _) => new ShaderSource("ScreenFS.frag"));
            Trace("Loading default resources done.");
        }
        catch (Exception e)
        {
            Error($"Unable to load engine default resources: {e}");
        }
    }


    public void PlatformSetupDone()
    {
        _sceneSequencer = I.Get<SceneSequencer>();
        State = EngineState.Running;

        /*
         * Start the reality as soon the platform also is set up.
         */
        _logicalThread.Start();
    }


    public void SetViewRectangle(Vector2 ul, Vector2 lr)
    {
        _vViewUl = ul;
        _vViewLr = lr;
    }


    /**
     * Return the rectangle the entire view should be rendered in.
     * This size is defined and set to a smaller rectangle if the game
     * shall be rendered in a windowed context next to e.g. the debugging UI.
     */
    public void GetViewRectangle(out Vector2 ul, out Vector2 lr)
    {
        ul = _vViewUl;
        lr = _vViewLr;
    }


    public Engine(engine.IPlatform platform)
    {
        engine.Unit u = new();

        u.RunStartupTest();


        _nextId = 0;
        _platform = platform;
        _ecsWorld = new DefaultEcs.World();
        _entityCommandRecorder = new(4096, 1024 * 1024);

        I.Register<engine.Timeline>(() => new engine.Timeline());
        I.Register<engine.news.SubscriptionManager>(() => new SubscriptionManager());
        I.Register<engine.news.EventQueue>(() => new EventQueue());
        I.Register<engine.news.InputEventPipeline>(() => new InputEventPipeline());
        I.Register<engine.joyce.TransformApi>(() => new joyce.TransformApi(this));
        I.Register<engine.joyce.HierarchyApi>(() => new joyce.HierarchyApi(this));
        I.Register<engine.physics.API>(() => new physics.API(this));
        I.Register<engine.ObjectRegistry<joyce.Material>>(() => new ObjectRegistry<joyce.Material>());
        I.Register<engine.ObjectRegistry<joyce.Renderbuffer>>(() => new ObjectRegistry<joyce.Renderbuffer>());
        I.Register<engine.Resources>(() => new Resources());
        I.Register<engine.SceneSequencer>(() => new SceneSequencer(this));
        I.Register<engine.physics.ObjectCatalogue>(() => new engine.physics.ObjectCatalogue());
        
        State = EngineState.Starting;

        u.RunEngineTest(this);

        _loadDefaultResources();
    }
}