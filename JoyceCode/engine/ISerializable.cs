using System.Text.Json.Nodes;

namespace engine;

public interface ISerializable
{
    public void SaveTo(ref JsonObject jo);
    public void SetupFrom(JsonObject jo);
}
