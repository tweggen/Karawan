using engine;

namespace Karawan;

public class AssetImplementation : IAssetImplementation
{
    public System.IO.Stream Open(in string filename)
    {
        string resourcePath = engine.GlobalSettings.Get("Engine.ResourcePath");
        return System.IO.File.OpenRead(resourcePath + "assets/" + filename);
    }

    public AssetImplementation()
    {
        
    }
}