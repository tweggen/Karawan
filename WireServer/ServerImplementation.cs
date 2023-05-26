
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DefaultEcs.Serialization;
using Grpc.Core;
using Wire;
using static engine.Logger;

namespace WireServer;

public class ServerImplementation : Svc.SvcBase
{
    private readonly engine.Engine _engine;
    private object _lo = new();

    // TXWTODO: This might lose some transitions.
    private EngineExecutionStatus _currentState = new();

    private void _onEngineStateChanged(object sender, engine.Engine.EngineState newEngineState)
    {
        /*
         * Translate to protocol
         */
        EngineExecutionState state;
        switch(newEngineState)
        {
            case engine.Engine.EngineState.Initialized: state = EngineExecutionState.Initialized; break;
            case engine.Engine.EngineState.Starting: state = EngineExecutionState.Starting; break;
            case engine.Engine.EngineState.Running: state = EngineExecutionState.Running; break;
            case engine.Engine.EngineState.Stopping: state = EngineExecutionState.Stopping; break;
            case engine.Engine.EngineState.Stopped: state = EngineExecutionState.Stopped; break;
            default: state = EngineExecutionState.Initialized; break;
        }
        var es = new EngineExecutionStatus { State = state };
        lock (_lo)
        {
            _currentState = es;
            Monitor.Pulse(_lo);
        }
    }

    internal class ComponentInfo
    {
        public Type Type;
        public string ValueAsString;
        public object Value;
    }
    
    internal class EntityComponentTypeReader : DefaultEcs.Serialization.IComponentTypeReader
    {
        public SortedDictionary<string, ComponentInfo> DictComponentTypes = new();
        private DefaultEcs.Entity _entity;

        public void OnRead<T>(int maxCapacity)
        {
            if (_entity.Has<T>())
            {
                Type type = typeof(T);
                string strType = typeof(T).ToString();
                string strValueRepresentation = "(value unprintable)";
                Type t = typeof(T);
                object value = null;
                try
                {
                    strValueRepresentation = _entity.Get<T>().ToString();
                    value = _entity.Get<T>();
                }
                catch (Exception ex)
                {

                }

                DictComponentTypes[strType] = new ComponentInfo()
                {
                    Type = type, 
                    ValueAsString = strValueRepresentation,
                    Value = value
                };
            }
        }

        public EntityComponentTypeReader(DefaultEcs.Entity entity)
        {
            _entity = entity;
        }
    }
    
    
    public override Task<EngineExecutionStatus> Pause(PauseParams pauseParams, ServerCallContext context)
    {
        Trace("Pause called");
        _engine.SetEngineState(engine.Engine.EngineState.Stopped);
        
        
        /*
         * Some fake result.
         */
        var status = new EngineExecutionStatus();
        status.State = EngineExecutionState.Stopped;
        return Task.FromResult(status);
    }

    
    public override Task<EngineExecutionStatus> Continue(ContinueParams pauseParams, ServerCallContext context)
    {
        Trace("Continue called");
        _engine.SetEngineState(engine.Engine.EngineState.Running);

        /*
         * Some fake result.
         */
        var status = new EngineExecutionStatus();
        status.State = EngineExecutionState.Running;
        return Task.FromResult(status);
    }


    /**
     * Very bad simluation of an event queue.
     */
    public override async Task ReadEngineState(EngineStateParams request, IServerStreamWriter<EngineExecutionStatus> responseStream, ServerCallContext context)
    {
        Trace("ReadEngineState");
        while (true)
        {
            EngineExecutionStatus currentState;
            lock (_lo)
            {
                /*
                 * First write the current state, then wait for the next
                 */
                currentState = _currentState.Clone();
            }
            await responseStream.WriteAsync(currentState);

            /*
             * Write out new game states, as they come in.
             */
            while (true)
            {
                EngineExecutionStatus newState = null;
                bool doSend = false;
                lock (_lo)
                {
                    if (currentState.State != _currentState.State)
                    {
                        newState = _currentState.Clone();
                        doSend = true;
                    }
                    else
                    {
                        Monitor.Wait(_lo);
                    }
                }
                currentState = newState;
                if (doSend)
                {
                    await responseStream.WriteAsync(newState);
                }
            }
        }
    }

    
    public override Task<global::Wire.GetEntityResult> GetEntity(Wire.GetEntityParams request, ServerCallContext context)
    {
        Trace($"Called with entity {request.EntityId}.");
        DefaultEcs.Entity entity = _engine.GetEcsWorld().FindEntity(request.EntityId);
        Wire.GetEntityResult entityResult = new();
        entityResult.Entity = new();
        if (entity.IsAlive)
        {
            Trace($"Entity {request.EntityId} is alive.");
            EntityComponentTypeReader reader = new(entity);
            _engine.GetEcsWorld().ReadAllComponentTypes(reader);
            foreach (var (strType,componentInfo) in reader.DictComponentTypes)
            {
                Wire.Component component = new()
                {
                    Type = componentInfo.Type.ToString(),
                    Value = componentInfo.ValueAsString
                };
                System.Reflection.FieldInfo[] fields = componentInfo.Type.GetFields();
         
                foreach(var fieldInfo in fields)
                {
                    Type typeAttr = fieldInfo.FieldType;
                    string strValue = "(not available)";
                    try
                    {
                        strValue = fieldInfo.GetValue(componentInfo.Value).ToString();
                    }
                    catch (Exception e)
                    {
                        
                    }

                    Wire.CompProp compProp = new() { Type = typeAttr.ToString(), Name = fieldInfo.Name, Value = strValue };
                    component.Properties.Add(compProp);
                }
                entityResult.Entity.Components.Add(component);
            }
        }
        Trace($"Returning entity {request.EntityId}.");
        return Task.FromResult(entityResult);
    }

    
    public override Task<global::Wire.GetEntityListResult> GetEntityList(Wire.GetEntityListParams request, ServerCallContext context)
    {
        Trace("Called.");
        Wire.GetEntityListResult entityListResult = new();
        /*
         * TXWTODO: At this point we are accessing the world from outside the main thread
         * context.
         */
        var enumEntities = _engine.GetEcsWorld().GetEntities().AsEnumerable();
        foreach (DefaultEcs.Entity entity in enumEntities)
        {
            entityListResult.EntityIds.Add(entity.GetId());
        }

        Trace("Returning.");
        return Task.FromResult(entityListResult);
    }

    
    public ServerImplementation(in engine.Engine engine) : base()
    {
        _engine = engine;
        _engine.EngineStateChanged += _onEngineStateChanged;
    }
}
