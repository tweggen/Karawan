using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;


namespace engine.world;

public interface ICreator
{
    /**
     * Setup a given entity from serializable data.
     * This is required e.g. after loading an entity from disk.
     */
    public Func<Task> SetupEntityFrom(DefaultEcs.Entity eLoaded, in JsonElement je);

    public void SaveEntityTo(DefaultEcs.Entity eLoader, out JsonNode jn);
}