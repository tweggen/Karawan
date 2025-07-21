﻿
using System.IO;
using Android.Content.Res;

namespace Wuka;


public class AssetImplementation : engine.IAssetImplementation
{
    private AssetManager _assetManager;

    public System.IO.Stream Open(in string filename)
    {
        string realName = /*"Platforms/Android/" + */ filename;
        var orgStream = _assetManager.Open(realName);
        var streamReader = new StreamReader(orgStream);
        var memoryStream = new MemoryStream();
        streamReader.BaseStream.CopyTo(memoryStream);
        memoryStream.Position = 0;
        return memoryStream;
    }

    public void AddAssociation(string tag, string uri)
    {
        /*
         * We don't need that on android 
         */
    }

    public AssetImplementation(in AssetManager assetManager)
    {
        _assetManager = assetManager;
    }
}
