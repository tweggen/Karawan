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
     *
     * We expect the method to return a task that can be awaited from any
     * thread resolving into an action that must run in the main thread.
     */
    public Task<Action> SetupEntityFrom(DefaultEcs.Entity eLoaded, JsonElement je);

    public void SaveEntityTo(DefaultEcs.Entity eLoader, out JsonNode jn);
}