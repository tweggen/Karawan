using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using DefaultEcs.Serialization;
using engine;
using engine.world;
using Creator = engine.world.components.Creator;

using static engine.Logger;

namespace builtin;


public class EntitySaver : AModule
{
    public override IEnumerable<IModuleDependency> ModuleDepends() => new List<IModuleDependency>()
    {
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
                JOComponents.Add(
                    typeof(T).ToString(),
                    JsonSerializer.SerializeToNode(component, SerializerOptions)
                );
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

        JsonSerializerOptions serializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
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
     * Must be called from logical thread
     */
    public Func<Task> LoadAll(JsonElement jeAll) => new(async () =>
    {
        JsonSerializerOptions serializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        foreach (var jpEntity in jeAll.EnumerateObject())
        {
            var strEntityId = jpEntity.Name;
            var jeEntity = jpEntity.Value;

            var e = I.Get<Engine>().GetEcsWorld().CreateEntity();

            if (jeEntity.TryGetProperty("components", out var jeComponents))
            {
                foreach (var jpComponent in jeComponents.EnumerateObject())
                {
                    var strComponentName = jpComponent.Name;
                    var jeComponent = jpComponent.Value;

                    var type = Type.GetType(strComponentName)!;
                    var comp = JsonSerializer.Deserialize(jeComponent.GetRawText(), type, serializerOptions)!;
                    // TXWTODO: The only way I know to call set on entity is via reflection.
                    MethodInfo baseMethod = typeof(DefaultEcs.Entity).GetMethods()
                        .Where(m => m.Name == "Set" && m.GetParameters().Length == 1)
                        .FirstOrDefault(m => true);
                    var genericMethod = baseMethod.MakeGenericMethod(type);
                    genericMethod.Invoke(e, new object[] { comp });

                    MethodInfo? setupFromMethod = type.GetMethod("SetupFrom");
                    if (null != setupFromMethod)
                    {
                        Func<Task>? taskSetup;
                        try
                        {
                             taskSetup = (Func<Task>)setupFromMethod.Invoke(comp, new object[] { jeComponent });
                             if (null != taskSetup)
                             {
                                 await taskSetup();
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
            }

        }
    });
}