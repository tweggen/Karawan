
using System.IO;
using Android.Content.Res;

namespace Wuka;


public class AssetImplementation : engine.AAssetImplementation
{
    private AssetManager _assetManager;

    public override System.IO.Stream Open(in string filename)
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

    
    public override bool Exists(in string filename)
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


    public override void AddAssociation(string tag, string uri)
    {
        /*
         * We don't need that on android 
         */
    }

    public override IReadOnlyDictionary<string, string> GetAssets()
    {
        throw new NotImplementedException();
    }

    public AssetImplementation(in AssetManager assetManager)
    {
        _assetManager = assetManager;
    }
}
