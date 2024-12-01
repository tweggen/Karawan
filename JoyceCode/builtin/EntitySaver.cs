using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using DefaultEcs.Serialization;
using engine;
using engine.joyce;
using Creator = engine.world.components.Creator;

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
        public JsonObject JOComponents { get; }= new JsonObject();
        public JsonSerializerOptions SerializerOptions { get; set; }
        
        public void OnRead<T>(in T component, in DefaultEcs.Entity e)
        {
            if (Attribute.IsDefined(typeof(T), typeof(engine.IsPersistable)))
            {
                IsEmpty = true;
                JOComponents.Add(
                    typeof(T).ToString(),
                    JsonSerializer.SerializeToNode(component, SerializerOptions)
                );
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
                if (jnEntityByCreator.GetValueKind() != JsonValueKind.Null &&
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
    public void RecreateAll(JsonElement jeAll)
    {
        foreach (var jpCreator in jeAll.EnumerateObject().GetEnumerator())
        {
            var strCreatorType = jpCreator.Name;
            var jeValue = jpCreator.Value;
            
            
            foreach (var jpEntity in jeValue.EnumerateObject().GetEnumerator())
            {
                var strEntityId = jpEntity.Name;
                var jeEntity = jpEntity.Value;
                
                
            }
        }
    }
}