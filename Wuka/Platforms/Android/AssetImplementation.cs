
using System.IO;
using Android.Content.Res;

namespace Wuka;


public class AssetImplementation : engine.IAssetImplementation
{
    private AssetManager _assetManager;

    public System.IO.Stream Open(in string filename)
    {
        string realName = /*"Platforms/Android/" + */ filename;
        using (var orgStream = _assetManager.Open(realName))
        {
            using (var streamReader = new StreamReader(orgStream))
            {
                var memoryStream = new MemoryStream();
                streamReader.BaseStream.CopyTo(memoryStream);
                memoryStream.Position = 0;
                return memoryStream;
            }
        }
    }

    
    public bool Exists(in string filename)
    {
        try
        {
            using (var orgStream = _assetManager.Open(filename))
            {
                return true;
            }
        }
        catch (Exception e)
        {
        }
        return false;
    }


    public void AddAssociation(string tag, string uri)
    {
        /*
         * We don't need that on android 
         */
    }

    public IReadOnlyDictionary<string, string> GetAssets()
    {
        throw new NotImplementedException();
    }

    public AssetImplementation(in AssetManager assetManager)
    {
        _assetManager = assetManager;
    }
}
