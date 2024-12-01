using System.Text.Json;
using System.Text.Json.Nodes;


namespace engine.world;

public interface ICreator
{
    /**
     * Setup a given entity from serializable data.
     * This is required e.g. after loading an entity from disk.
     */
    public void SetupEntityFrom(DefaultEcs.Entity eLoaded, in JsonElement je);

    public void SaveEntityTo(DefaultEcs.Entity eLoader, out JsonNode jn);
}