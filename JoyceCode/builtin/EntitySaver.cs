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

namespace builtin;


public class EntitySaver : AModule
{
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

    public JsonNode SaveAll()
    {
        JsonNode jnAll = new JsonObject();

        var oreg = M<CreatorRegistry>();
        var context = new Context();

        JsonSerializerOptions serializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new ConverterFactory(M<ConverterRegistry>(), context) }
        };
        
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
            ref var cCreator = ref eCreated.Get<engine.world.components.Creator>();
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
        return jnAll;
    }


    /**
     * Must be called from logical thread, returns atomically for main thread operation, will
     * triggger streaming operations.
     */
    public Task LoadAll(JsonElement jeAll)
    {
        List<Task> listSetupTasks = new();
        List<(DefaultEcs.Entity, JsonElement)> listCreatorEntites = new();
        
        var context = new Context();
        JsonSerializerOptions serializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new ConverterFactory(M<ConverterRegistry>(), context) }
        };
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
                    var type = Type.GetType(strComponentName)!;

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
                    MethodInfo baseMethod = typeof(DefaultEcs.Entity).GetMethods()
                        .Where(m => m.Name == "Set" && m.GetParameters().Length == 1)
                        .FirstOrDefault(m => true);
                    var genericMethod = baseMethod.MakeGenericMethod(type);
                    genericMethod.Invoke(e, new object[] { comp });

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
                listCreatorEntites.Add((e, jeCreator));
            }
        }
        return Task.WhenAll(listSetupTasks).ContinueWith(tAll =>
        {
            List<Task> listSetupEntityTasks = new();
            
            /*
             * So everything has been loaded into the vartious components and been individually
             * set up. Now we can call creators for setting up their content which now may depend
             * on other entities.
             */
            foreach (var (e, jeCreator) in listCreatorEntites)
            {
                var cCreator = e.Get<Creator>();
                ICreator? iCreator = I.Get<CreatorRegistry>().GetCreator(cCreator.CreatorId);
                if (null == iCreator)
                {
                    continue;
                }
                listSetupEntityTasks.Add(iCreator.SetupEntityFrom(e, jeCreator)());
            }

            /*
             * Let the caller now when all of the setup tasks have been performed.
             */
            return Task.WhenAll(listSetupEntityTasks);
        });
    }
}