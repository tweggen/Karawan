using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using builtin.entitySaver;
using DefaultEcs.Serialization;
using engine;
using engine.world;
using Creator = engine.world.components.Creator;

using static engine.Logger;
using Context = builtin.entitySaver.Context;
using static builtin.extensions.EntityTypedCalls;

namespace builtin;


public class EntitySaver : AModule
{
    private readonly Dictionary<string, Type> _typeCache = new();

    private Type ResolveType(string typeName)
    {
        if (_typeCache.TryGetValue(typeName, out var cached))
            return cached;

        var type = Type.GetType(typeName);
        if (type == null)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = asm.GetType(typeName);
                if (type != null) break;
            }
        }

        if (type != null)
            _typeCache[typeName] = type;

        return type;
    }

    public override IEnumerable<IModuleDependency> ModuleDepends() => new List<IModuleDependency>()
    {
        new SharedModule<ConverterRegistry>() {},
        new SharedModule<CreatorRegistry>() {}
    };


    private class SaveComponentsReader : IComponentReader
    {
        public bool IsEmpty = true;
        public JsonObject JOComponents { get; } = new JsonObject();
        public JsonSerializerOptions SerializerOptions { get; set; }
        
        public void OnRead<T>(in T component, in DefaultEcs.Entity e)
        {
            /*
             * First serialize all fields to the object.
             */
            if (Attribute.IsDefined(typeof(T), typeof(engine.IsPersistable)))
            {
                IsEmpty = false;
                try
                {
                    var jnComponent = JsonSerializer.SerializeToNode(component, SerializerOptions);
                    JOComponents.Add(
                        typeof(T).ToString(), jnComponent);
                }
                catch (Exception exception)
                {
                    Error($"Unable to serialize component {typeof(T)} for entity {e}: {exception}");
                }
            }
            
            /*
             * Then, if we have a serialization method, use it.
             */
            Type componentType = typeof(T);
            MethodInfo? saveToMethod = componentType.GetMethod("SaveTo");
            if (null != saveToMethod)
            {
                saveToMethod.Invoke(component, new object[] { JOComponents });
            }
        }
    }

    public async Task<JsonNode> SaveAll()
    {
        var oreg = M<CreatorRegistry>();
        var context = new Context();

        JsonSerializerOptions serializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new ConverterFactory(M<ConverterRegistry>(), context) }
        };
        
        
        JsonNode jnAll = new JsonObject();

        await _engine.TaskMainThread(async () =>
        {
            /*
             * Iterate through everything that has a creator associated. That way, it might
             * be subject to later recreation.
             */
            var enumCreated =
                I.Get<Engine>().GetEcsWorld().GetEntities()
                    .With<Creator>().AsEnumerable();
            foreach (var eCreated in enumCreated)
            {
                context.Entity = eCreated;

                /*
                 * Save that we have an entity, with a certain id, that by
                 * incident matches the id the actual entity has.
                 */
                string strEntity = eCreated.ToString();
                var joAll = jnAll.AsObject();

                var componentReader = new SaveComponentsReader() { SerializerOptions = serializerOptions };
                eCreated.ReadAllComponents(componentReader);

                /*
                 * This is what we might need to store. We take care to save a record only for entities
                 * that do exist in some way.
                 */
                JsonObject joComponents = null;
                JsonObject joCreator = null;

                if (!componentReader.IsEmpty)
                {
                    joComponents = componentReader.JOComponents;
                }

                /*
                 * Now, if we have a creator, have it save everything it wants to this
                 * object.
                 */
                var cCreator = eCreated.Get<engine.world.components.Creator>();
                if (cCreator.CreatorId > Creator.CreatorId_HardcodeMax)
                {
                    var iCreator = oreg.GetCreator(cCreator.CreatorId);
                    iCreator.SaveEntityTo(eCreated, out var jnEntityByCreator);
                    if (null != jnEntityByCreator &&
                        jnEntityByCreator.GetValueKind() != JsonValueKind.Null &&
                        jnEntityByCreator.GetValueKind() != JsonValueKind.Undefined)
                    {
                        joCreator = new JsonObject();
                        joCreator.Add(iCreator.GetType().ToString(), jnEntityByCreator);
                    }
                }

                if (joComponents != null || joCreator != null)
                {
                    JsonNode jnEntity;
                    JsonObject joEntity;
                    if (!joAll.TryGetPropertyValue(strEntity, out jnEntity))
                    {
                        joEntity = new JsonObject();
                        jnEntity = joEntity;
                        joAll.Add(strEntity, joEntity);
                    }
                    else
                    {
                        jnEntity = joAll[strEntity];
                        joEntity = jnEntity.AsObject();
                    }

                    if (joComponents != null) joEntity.Add("components", joComponents);
                    if (joCreator != null) joEntity.Add("creator", joCreator);
                }
            }
        });


        return jnAll;
    }


    /**
     * Load all entities. See readme
     */
    public async Task LoadAll(JsonElement jeAll)
    {
        List<Task> listSetupTasks = new();
        List<(DefaultEcs.Entity, ICreator, JsonElement)> listCreatorEntities = new();
        
        var context = new Context();
        JsonSerializerOptions serializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new ConverterFactory(M<ConverterRegistry>(), context) }
        };

        await _engine.TaskMainThread(async () =>
        {
            foreach (var jpEntity in jeAll.EnumerateObject())
            {
                var strEntityId = jpEntity.Name;
                var jeEntity = jpEntity.Value;

                var e = I.Get<Engine>().GetEcsWorld().CreateEntity();
                context.Entity = e;

                if (jeEntity.TryGetProperty("components", out var jeComponents))
                {
                    /*
                     * First, setup all components from json.
                     */
                    foreach (var jpComponent in jeComponents.EnumerateObject())
                    {
                        var strComponentName = jpComponent.Name;
                        var jeComponent = jpComponent.Value;
                        var type = ResolveType(strComponentName);
                        if (type == null)
                        {
                            Error($"Unable to find type '{strComponentName}' for deserialization.");
                            continue;
                        }

                        Object comp = null;
                        try
                        {
                            string rawText = jeComponent.GetRawText();
                            comp = JsonSerializer.Deserialize(rawText, type, serializerOptions)!;
                        }
                        catch (Exception exception)
                        {
                            Error(
                                $"Unable to deserialize entity component {strComponentName} from {jeComponent.GetRawText()}: {exception}");
                            continue;
                        }

                        /*
                         * If creating an object from properties failed or no properties had been given.
                         */
                        if (null == comp)
                        {
                            comp = Activator.CreateInstance(type);
                        }

                        /*
                         * Set the deserialized component to the entity.
                         */
                        e.Set(type, comp);

                        /*
                         * Finally, call setupfrom for each component that defines it.
                         */
                        MethodInfo? setupFromMethod = type.GetMethod("SetupFrom");
                        if (null != setupFromMethod)
                        {
                            try
                            {
                                Func<Task>? taskSetup;
                                taskSetup = (Func<Task>)setupFromMethod.Invoke(comp, new object[] { jeComponent });
                                if (null != taskSetup)
                                {
                                    listSetupTasks.Add(taskSetup());
                                }
                            }
                            catch (Exception exception)
                            {
                                Error($"Unable to create and execute deser method for {type.Name}: {exception}");
                            }
                        }

                    }

                }

                if (jeEntity.TryGetProperty("creator", out var jeCreator))
                {
                    if (e.Has<Creator>())
                    {
                        var cCreator = e.Get<Creator>();
                        ICreator? iCreator = I.Get<CreatorRegistry>().GetCreator(cCreator.CreatorId);
                        if (null != iCreator)
                        {
                            listCreatorEntities.Add((e, iCreator, jeCreator));
                        }
                    }
                }
            }
        });
        
        await Task.WhenAll(listSetupTasks);
        
        /*
         * So everything has been loaded into the vartious components and been individually
         * set up. Now we can call creators for setting up their content which now may depend
         * on other entities.
         */
        var iCreatorTasks = listCreatorEntities 
            .Select(async tuple =>
            {
                var (e, iCreator, jeCreator) = tuple; 
                var action = await iCreator.SetupEntityFrom(e, jeCreator); 
                _engine.QueueMainThreadAction(action);
            }) .ToList();
        
        /*
         * Colntinue as soon everything has been queued.
         */
        await Task.WhenAll(iCreatorTasks);
    }
}