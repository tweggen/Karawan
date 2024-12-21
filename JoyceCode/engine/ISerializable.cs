using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace engine;

public interface ISerializable
{
    public void SaveTo(ref JsonObject jo);
    public System.Func<Task> SetupFrom(JsonElement je);
}
