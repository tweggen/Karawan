using System.Collections.Generic;
using System.IO;
using engine;
using static engine.Logger;

namespace Karawan.GenericLauncher;

/// <summary>
/// Desktop asset implementation for the generic launcher.
/// </summary>
public class AssetImplementation : AAssetImplementation
{
    private object _lo = new();
    private SortedDictionary<string, string> _mapAssociations = new();
    private IReadOnlyDictionary<string, string>? _mapRoAssociations = null;

    public override void AddAssociation(string tag, string uri)
    {
        lock (_lo)
        {
            _mapAssociations[tag] = uri;
        }
    }

    public override IReadOnlyDictionary<string, string> GetAssets()
    {
        if (null == _mapRoAssociations)
        {
            lock (_lo)
            {
                _mapRoAssociations = _mapAssociations.AsReadOnly();
            }
        }
        return _mapRoAssociations;
    }

    public override bool Exists(in string filename)
    {
        lock (_lo)
        {
            return _mapAssociations.ContainsKey(filename);
        }
    }

    /// <summary>
    /// Open the resource with the given filename or tag.
    /// </summary>
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
        
        // First try to open by tag directly
        try
        {
            string fullPath = Path.Combine(resourcePath, tag);
            stream = File.OpenRead(fullPath);
            return stream;
        }
        catch (System.Exception)
        {
            // Try the URI instead
        }

        // If tag didn't work, try the URI from associations
        if (uri != null)
        {
            string fullPath = Path.Combine(resourcePath, uri);
            stream = File.OpenRead(fullPath);
        }

        return stream;
    }

    public AssetImplementation()
    {
    }
}
