using builtin.loader;

namespace engine.joyce;

public class ModelCacheParams
{
    public string Url { get; set; } 
    public ModelProperties? Properties { get; set; }
    public InstantiateModelParams? Params { get; set; }
}