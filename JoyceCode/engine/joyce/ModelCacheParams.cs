using builtin.loader;

namespace engine.joyce;

public class ModelCacheParams
{
    public string Url { get; set; } 
    public ModelProperties? Properties { get; set; }
    public InstantiateModelParams? Params { get; set; }
    
    public string GetHashCode()
    {
        string hash = "{";
        hash += $"\"url\": \"{Url}\"";
        if (Properties != null)
        {
            string mpHash = Properties.ToString();
            hash += $", \"modelProperties\": {mpHash}";
        }

        if (Params != null)
        {
            string pHash = Params.Hash();
            hash += $", \"instantiateModelParams\": {pHash}";
        }
        hash += "}";
        return hash;
    }

}