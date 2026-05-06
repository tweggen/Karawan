using System.Collections.Generic;
using System.IO;

namespace engine.joyce;

public class AnimationPackRegistry
{
    // model filename (no path) → packName → semicolon-joined animation URLs
    private readonly Dictionary<string, Dictionary<string, string>> _packs = new();

    public void RegisterPack(string modelUrl, string packName, string animationUrls)
    {
        string key = Path.GetFileName(modelUrl);
        if (!_packs.TryGetValue(key, out var packs))
            _packs[key] = packs = new();
        packs[packName] = animationUrls;
    }

    // modelUrl may be just the filename or a full/relative path — only the filename part is used.
    // Returns null if the model or pack is not registered.
    public string? GetPackAnimationUrls(string modelUrl, string packName)
    {
        string key = Path.GetFileName(modelUrl);
        if (_packs.TryGetValue(key, out var packs) && packs.TryGetValue(packName, out var urls))
            return urls;
        return null;
    }
}
