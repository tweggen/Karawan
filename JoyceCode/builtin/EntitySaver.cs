using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
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


    public JsonNode SaveAll()
    {
        JsonNode jnAll = new JsonObject();

        var oreg = M<CreatorRegistry>();
        
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
            
            // TXWTODO: Automatically persist all persistable components.
            
            ref var cCreator = ref eCreated.Get<engine.world.components.Creator>();
            var iCreator = oreg.GetCreator(cCreator.CreatorId);
            iCreator.SaveEntityTo(eCreated, out var jnEntityByCreator);

            jnAll[iCreator.GetType().ToString()][eCreated.ToString()] = jnEntityByCreator;
        }
        return jnAll;
    }
    
    /**
     * Must be called from logical thread
     */
    public void RecreateAll(JsonElement jeAll)
    {
    }
}