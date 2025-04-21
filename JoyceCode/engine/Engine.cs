using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using DefaultEcs;
using engine.joyce;
using engine.news;
using engine.Resource;
using engine.rom;
using static engine.Logger;

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

    public physics.actions.Log PLog;
    
    private int _nextId = 0;

    private DefaultEcs.World _ecsWorld;

    private engine.physics.API _aPhysics;
    private engine.joyce.TransformApi _aTransform;
    private engine.joyce.HierarchyApi _aHierarchy;

    private DefaultEcs.Command.EntityCommandRecorder _entityCommandRecorder;
    #if !DEBUG
    private List<IList<DefaultEcs.Entity>> _listDoomedEntityLists = new();
    #else
    private HashSet<DefaultEcs.Entity> _setDoomedEntities = new();
    #endif

    private bool _isSingleThreaded = true;

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
    private behave.systems.ParticleEmitterSystem _systemParticleEmitter;
    private behave.systems.ParticleSystem _systemParticle;
    private physics.systems.ApplyPosesSystem _systemApplyPoses;
    private physics.systems.MoveKineticsSystem _systemMoveKinetics;
    private audio.systems.MovingSoundsSystem _systemMovingSounds;
    private behave.systems.AnimationSystem _systemAnimation;

    private IReadOnlyCollection<IModule> _roListModules = null;
    private List<IModule> _listModules = new();

    private readonly Queue<EntitySetupAction> _queueEntitySetupActions = new();

    private Thread _logicalThread = null;
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
    public event EventHandler<float> OnAfterPhysics;

    public event EventHandler<float> OnImGuiRender;

    private CameraInfo? _cameraInfo;

    public CameraInfo CameraInfo
    {
        get
        {
            lock (_lo)
            {
                return _cameraInfo;
            }
        }
    }

    public readonly EntityObserver Camera = new();
    public readonly EntityObserver Player = new();


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

    private uint _frameNumber = 0;

    public uint FrameNumber
    {
        get => _frameNumber;
        set { _frameNumber = value; }
    }

    
    public physics.API APhysics
    {
        get => _aPhysics; 
    }
    
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


#if false
    private bool _tryGetMemberEntity(out DefaultEcs.Entity eReturn, ref DefaultEcs.Entity myMember)
    {
        lock (_lo)
        {
            eReturn = myMember;
            bool haveMember = eReturn != default && eReturn.IsAlive && eReturn.IsEnabled();
            if (!haveMember)
            {
                eReturn = default;
            }

            return haveMember;
        }
    }


    private bool _tryGetMemberEntity<T>(out DefaultEcs.Entity eReturn, ref DefaultEcs.Entity myMember)
    {
        bool haveMember = _tryGetMemberEntity(out eReturn, ref myMember) && eReturn.Has<T>();
        if (!haveMember)
        {
            eReturn = default;
        }

        return haveMember;
    }
#endif


    public void BeamTo(Vector3 vPos, Quaternion qStart)
    {
        QueueMainThreadAction(() =>
        {
            var pref = Player.Value.Get<engine.physics.components.Body>().Reference;
            pref.Pose.Position = vPos;
            pref.Pose.Orientation = qStart;
        });
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
        Debug.Assert(_isSingleThreaded || Thread.CurrentThread == _logicalThread, "not in logical threead.");
        return _ecsWorld;
    }


    public DefaultEcs.World GetEcsWorldDangerous()
    {
        return _ecsWorld;
    }


    /**
     * Read the ecs world from a non-logical thread. This is valid, if the world is not about to be used.
     */
    public DefaultEcs.World GetEcsWorldNoAssert()
    {
        return _ecsWorld;
    }


#if false
    public DefaultEcs.Command.WorldRecord GetEcsWorldRecord()
    {
        Debug.Assert(Thread.CurrentThread == _logicalThread, "not in logical threead.");
        return _entityCommandRecorder.Record(_ecsWorld);
    }


    public void ApplyEcsRecorder(in DefaultEcs.Command.EntityCommandRecorder recorder)
    {
        Debug.Assert(Thread.CurrentThread == _logicalThread, "not in logical threead.");
        recorder.Execute();
    }


    private void _commitWorldRecord()
    {
        _entityCommandRecorder.Execute();
    }
#endif

    
    private void _executeDoomedEntities()
    {
#if !DEBUG
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
#else
        List<Entity> list = new();
        lock (_lo)
        {
            list = new(_setDoomedEntities);
            _setDoomedEntities.Clear();
        }

        foreach (var e in list)
        {
            e.Dispose();
        }
#endif
    }


    public void AddDoomedEntities(in IList<DefaultEcs.Entity> listDoomedEntities)
    {
        lock (_lo)
        {
#if !DEBUG
            _listDoomedEntityLists.Add(listDoomedEntities);
#else
            foreach (var e in listDoomedEntities)
            {
                if (!e.IsAlive)
                {
                    ErrorThrow<ArgumentException>($"Tried to kill an entity {e} that has not been alive anymore.");
                }
                if (_setDoomedEntities.Contains(e))
                {
                    ErrorThrow<ArgumentException>($"Entity {e} already was doomed before.");
                }

                _setDoomedEntities.Add(e);
            }
#endif
        }
    }


    public void AddDoomedEntity(DefaultEcs.Entity entity)
    {
        lock (_lo)
        {
#if !DEBUG
            List<Entity> listEntity = new List<Entity>();
            listEntity.Add(entity);
            _listDoomedEntityLists.Add(listEntity);
#else
            if (!entity.IsAlive)
            {
                ErrorThrow<ArgumentException>($"Tried to kill an entity {entity} that has not been alive anymore.");
            }
            if (_setDoomedEntities.Contains(entity))
            {
                ErrorThrow<ArgumentException>($"Entity {entity} already was doomed before.");
            }

            _setDoomedEntities.Add(entity);
#endif
        }
    }


    #if DEBUG
    public DefaultEcs.Entity CreateEntity(string name)
    {
        Debug.Assert(_isSingleThreaded || System.Threading.Thread.CurrentThread == _logicalThread, "Not called from logical thread.");
        DefaultEcs.Entity entity = _ecsWorld.CreateEntity();
        entity.Set(new joyce.components.EntityName(name));
        return entity;
    }
    #else
    public Entity CreateEntity(string _) => _ecsWorld.CreateEntity();
    #endif


    private void _executeEntitySetupActions(float matTime)
    {
        _queueStopwatch.Reset();
        _queueStopwatch.Start();
        float totalMillies = (float) _queueStopwatch.Elapsed.TotalMilliseconds;
        while (totalMillies < matTime * 1000f)
        {
            EntitySetupAction entitySetupAction;
            lock (_lo)
            {
                int count = _queueEntitySetupActions.Count;
                if (count == 0)
                {
                    break;
                }
                else
                {
                    if (count > 1000)
                    {
                        if (matTime < 2000f)
                        {
                            /*
                             * Emergency: If there is too much in the queue, increase the processing time
                             * to 2s.
                             */
                            matTime = 2000f;
                            Warning($"Action queue high threshold, blocking.");
                        }
                    }
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

            float lastMillies = totalMillies;
            totalMillies = (float)_queueStopwatch.Elapsed.TotalMilliseconds;
            float actionMillies = totalMillies - lastMillies;
            if (actionMillies > 1000f * matTime)
            {
                Trace($"Warning, action took {actionMillies}ms.");
            }
        }

        _queueStopwatch.Stop();

        int queueLeft;
        lock (_lo)
        {
            queueLeft = _queueEntitySetupActions.Count;
        }

        if (30 < queueLeft)
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
    
    
    public Task TaskMainThread(Action action)
    {
        if (Thread.CurrentThread == _logicalThread)
        {
            action();
            return Task.CompletedTask;
        }
        else
        {
            TaskCompletionSource<int> tcsAction = new();
            QueueMainThreadAction(() =>
            {
                action();
                tcsAction.SetResult(0);
            });
            return tcsAction.Task;
        }
    }
    

    /**
     * Run the given method either synchronously in the main thread
     * or queue it async.
     */
    public void RunMainThread(Action action)
    {
        if (Thread.CurrentThread == _logicalThread)
        {
            action();
        }
        else
        {
            QueueMainThreadAction(action);
        }
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

    /**
     * Perform a logical frame.
     * @param dt
     *     The logical timespan this frame si supposed to cover
     * @param timeLeft
     *     The computing time in total for this frame.
     */
    private void _onLogicalFrame(float dt, float timeLeft)
    {
        EngineState engineState;
        GamePlayStates gamePlayState;
        _fpsLogicalMonitor.OnFrame(dt);
        var ectx = I.Get<EmissionContext>(); 

        lock (_lo)
        {
            engineState = State;
            gamePlayState = _gamePlayState;
            _frameNumber++;
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
            
            /*
             * We don't call animation before the first frame, only after it.
             */
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
                sm.Handle(ev, ectx);
            }
        }

        /*
         * Call the various ways of user behavior and/or controllers.
         * They will read world position and modify physics and or positions
         *
         * Require: Previously computed world transforms.
         */
        if (gamePlayState == GamePlayStates.Running)
        {
            _systemBehave.Update(dt);
            _systemParticleEmitter.Update(dt);
            _systemParticle.Update(dt);
        }

        OnLogicalFrame?.Invoke(this, dt);

        /*
         * Create the new camera info.
         */
        lock (_lo)
        {
            _cameraInfo = new CameraInfo(Camera.Value);
        }
        
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

        OnAfterPhysics?.Invoke(this, dt);

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

        #if false
        /*
         * Write back all entity modifications to the objects.
         */
        _commitWorldRecord();
        #endif

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

        /*
         * A bit of homework. In 7 of 8 cases, execute queues. In number eight, check the current fragment.
         */
        if ((_frameNumber & 7) != 0)
        {
            /*
             * Now work on the main thread queues.
             */

            bool executeCleanups = timeLeft > 0.005;
            bool executeActions = timeLeft > 0.002;

            /*
             * Async delete any entities that shall be deleted
             */
            if (executeCleanups)
            {
                _executeDoomedEntities();
            }

            /*
             * Async create / setup new entities.
             */
            if (executeActions)
            {
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
            }

            if (executeCleanups)
            {
                _workerCleanupActions.RunPart(0.001f);
            }

        }
        else // if ((_frameNumber & 7) != 0)
        {
            /*
             * Finally make sure the world is loaded for all viewers. We
             * are a little bit lazy on that.
             */
            I.Get<world.MetaGen>().Loader?.WorldLoaderProvideFragments();
        }

        /*
         * After all other things, take care to load the next animation frame.
         */
        _systemAnimation.Update(dt);
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
    

    public bool HasModuleType(in System.Type moduleType)
    {
        lock (_lo)
        {
            foreach (var mod in _listModules)
            {
                if (mod.GetType() == moduleType)
                {
                    return true;
                }
            }
        }
        return false;
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
        /*
         * Now, that the config is loaded, start the logical thread
         * and the actual execution.
         */
        _logicalThread = new Thread(_logicalThreadFunction);
        _logicalThread.Priority = ThreadPriority.AboveNormal;

        /*
         * Start the reality as soon the platform also is set up.
         */
        _logicalThread.Start();

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


    private void _startLogicalThreadServices()
    {
        _systemParticle = new();
        _systemParticleEmitter = new();
        _systemBehave = new();
        _systemApplyPoses = new();
        _systemMoveKinetics = new();
        _systemMovingSounds = new();
        _systemAnimation = new();
        _managerPhysics = new physics.Manager();
        _managerPhysics.Manage(this);
        _managerBehavior = new behave.Manager();
        _managerBehavior.Manage(this);
        _managerLuaScript = new();
        _managerLuaScript.Manage(_ecsWorld);
        _managerMapIcons = new();
        _managerMapIcons.Manage(_ecsWorld);

        _cameraInfo = new CameraInfo(Camera.Value);
    }


    private void _logicalThreadFunction()
    {
        DefaultEcs.Entity.OkThread = System.Threading.Thread.CurrentThread;

        _startLogicalThreadServices();
        
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
                // Trace($"toWait {toWait}");
                lock (ShortSleep)
                {
                    bool reaquiredInTime = System.Threading.Monitor.Wait(ShortSleep,1);
                    nSleeps++;
                }
                continue;
            }

            stopWatch.Stop();
            accumulator += (float) stopWatch.Elapsed.TotalSeconds;
            stopWatch.Reset();
            
            /*
             * Throw away anything larger than 200ms from the accumulator
             */
            if (accumulator > 0.2f)
            {
                accumulator = 0.2f;
            }

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

                /*
                 * Also tell the logical frame how much time there is left.
                 * So it can decide to do or postpone setup action jobs.
                 */
                float timeLeft = Single.Max(invFps * 2f - accumulator, 0f); 
                // Trace($"accu {accumulator} timeLeft {timeLeft}");
                
                if (_platformIsAvailable)
                {
                    _onLogicalFrame(invFps, timeLeft);
                    ++nLogical;
                }
                

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

        //I.Get<ModuleFactory>().FindModule<InputEventPipeline>();
    }


    public bool IsRunning()
    {
        bool platformIsRunning = _platform.IsRunning();
        lock (_lo)
        {
            return _isRunning && platformIsRunning;
        }
    }


    private void _mouseEnableFunc(bool f)
    {
        _platform.MouseEnabled = f;
    }
    private builtin.CountedEnabler _mouseEnabler;
    public void EnableMouse() => _mouseEnabler.Add();
    public void DisableMouse() => _mouseEnabler.Remove();
    

    private void _keyboardEnableFunc(bool f)
    {
        _platform.KeyboardEnabled = f;
    }
    private builtin.CountedEnabler _keyboardEnabler;
    public void EnableKeyboard() => _keyboardEnabler.Add();
    public void DisableKeyboard() => _keyboardEnabler.Remove();


    private void _pauseEnablerFunc(bool f)
    {
        GamePlayState = f?GamePlayStates.Paused:GamePlayStates.Running;
    }
    private builtin.CountedEnabler _pauseEnabler;
    public void EnablePause() => _pauseEnabler.Add();
    public void DisablePause() => _pauseEnabler.Remove();
    

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


    private TaskScheduler _taskScheduler;
    public TaskScheduler TaskScheduler { get => _taskScheduler; }
    
    private TaskFactory _taskFactory;
    public TaskFactory TF { get => _taskFactory; }
    
    public Task Run(Action action) => _taskFactory.StartNew(action, CancellationToken.None, TaskCreationOptions.DenyChildAttach, _taskScheduler );
    public Task<TResult> Run<TResult>(Func<TResult> function) => _taskFactory.StartNew(function, CancellationToken.None, TaskCreationOptions.DenyChildAttach, _taskScheduler );
    public Task Run(Func<System.Threading.Tasks.Task?> function) => _taskFactory.StartNew(function, CancellationToken.None, TaskCreationOptions.DenyChildAttach, _taskScheduler );


    public Engine(engine.IPlatform platform)
    {
        _taskScheduler = new LimitedConcurrencyLevelTaskScheduler(
            Int32.Max(3, 
                Environment.ProcessorCount - 2)
            );
        _taskFactory = new TaskFactory(_taskScheduler);
        
        engine.Unit u = new();
        u.RunStartupTest();

        _nextId = 0;
        _platform = platform;
        _ecsWorld = new DefaultEcs.World();
        // _entityCommandRecorder = new(4096, 1024 * 1024);
        
        I.Register<engine.ModuleFactory>(() => new engine.ModuleFactory());
        
        I.Register<engine.Timeline>(() => new engine.Timeline());
        I.Register<engine.news.SubscriptionManager>(() => new SubscriptionManager());
        I.Register<engine.news.EventQueue>(() => new EventQueue());
        // I.Register<engine.news.InputEventPipeline>(() => new InputEventPipeline());

        I.Register<engine.joyce.TransformApi>(() => new joyce.TransformApi());
        I.Register<engine.joyce.HierarchyApi>(() => new joyce.HierarchyApi());
        I.Register<engine.physics.API>(() => new physics.API(this));
        I.Register<engine.ObjectRegistry<joyce.Material>>(() => new ObjectRegistry<joyce.Material>());
        I.Register<engine.ObjectRegistry<joyce.Renderbuffer>>(() => new ObjectRegistry<joyce.Renderbuffer>());
        I.Register<engine.Resources>(() => new Resources());
        I.Register<engine.SceneSequencer>(() => new SceneSequencer(this));
        I.Register<engine.physics.ObjectCatalogue>(() => new engine.physics.ObjectCatalogue());
        I.Register<System.Net.Http.HttpClient>(() => new System.Net.Http.HttpClient());

        _mouseEnabler = new(_mouseEnableFunc);
        _keyboardEnabler = new(_keyboardEnableFunc);
        _pauseEnabler = new(_pauseEnablerFunc);
        
#if DEBUG
        if (engine.GlobalSettings.Get("engine.physics.TraceCalls") == "true") {
            string filename = $"joyce-physics-dump-{DateTime.UtcNow.ToString("yyyyMMddHHmmss")}.json";
            PLog = new physics.actions.Log()
            {
                DumpPath = GlobalSettings.Get("Engine.RWPath")
            };
        }
#endif

        _isSingleThreaded = false;
        
        State = EngineState.Starting;

        u.RunEngineTest(this);
        
        _loadDefaultResources();
    }
}