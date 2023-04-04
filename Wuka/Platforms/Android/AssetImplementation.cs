
using Android.Content.Res;

namespace Wuka;


public class AssetImplementation : engine.IAssetImplementation
{
    private AssetManager _assetManager;

    public System.IO.Stream Open(in string filename)
    {
        return _assetManager.Open(filename);
    }

    public AssetImplementation(in AssetManager assetManager)
    {
        _assetManager = assetManager;
    }
}
