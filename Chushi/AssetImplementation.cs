using System.Collections.Generic;
using engine;
using static engine.Logger;

namespace Chushi;

public class AssetImplementation : AAssetImplementation
{
    private object _lo = new();
    private SortedDictionary<string, string> _mapAssociations = new();

    public override void AddAssociation(string tag, string uri)
    {
        lock (_lo)
        {
            _mapAssociations[tag] = uri;
        }
    }

    public override IReadOnlyDictionary<string, string> GetAssets()
    {
        throw new NotImplementedException();
    }


    public override bool Exists(in string filename)
    {
        lock (_lo)
        {
            return _mapAssociations.ContainsKey(filename);
        }
    }

    /**
     * Open the resource with the given filename or tag.
     *
     * We have this loading strategy:
     * - first, we try to access the tag name in the resource Path.
     *   (this would be used in production probably).
     * - then, we try to access the uri in the resource path.
     *   (this would be used while debugging).
     */
    public override System.IO.Stream Open(in string tag)
    {
        string? uri;
        lock (_lo)
        {
            if (!_mapAssociations.TryGetValue(tag, out uri))
            {
                Warning($"Attempt to open unknown resource \"{tag}\".");
            }
        }
        string resourcePath = engine.GlobalSettings.Get("Engine.ResourcePath");

        System.IO.Stream? stream = null;
        try
        {
            stream = System.IO.File.OpenRead(resourcePath + tag);
            return stream;
        }
        catch (System.Exception e)
        {
            Error($"Unable to open file \"{resourcePath + tag}\" for reading: {e}");
        }

        if (uri != null)
        {
            try {
                stream = System.IO.File.OpenRead(resourcePath + uri);
                return stream;
            } catch (System.Exception e)
            {
            Error($"Unable to open file \"{resourcePath + uri}\" for reading: {e}");

            }
        }

        return stream;
    }
}