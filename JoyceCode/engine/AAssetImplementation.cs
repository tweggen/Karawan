using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using builtin.baking;
using engine.joyce;
using static engine.Logger;

namespace engine;

public abstract class AAssetImplementation : IAssetImplementation
{
    private bool _traceLoadingResources = true;
    private bool _traceLoadingAnimations = false;
    private bool _traceLoadingTextures = false;
    
    
    public SortedSet<string> AvailableAnimations = new();

    
    
    private void _whenLoadedResources(string path, JsonNode? node)
    {
        Trace("Loading resources...");
        if (null == node) return;
        try
        {
            if (node is JsonArray arr)
            {
                foreach (var resNode in arr)
                {
                    string? uri = resNode?["uri"]?.GetValue<string>();
                    if (uri is null)
                    {
                        throw new InvalidDataException("no uri specified in resource.");
                    }

                    string? tag = resNode?["tag"]?.GetValue<string>();
                    if (string.IsNullOrEmpty(tag))
                    {
                        int idx = uri.LastIndexOf('/');
                        if (idx != -1 && idx != uri.Length - 1)
                        {
                            tag = uri[(idx + 1)..];
                        }
                        else
                        {
                            tag = uri;
                        }
                    }

                    if (_traceLoadingResources)
                        Trace($"LoadResourcesTo: Added Resource \"{tag}\" from {uri}.");

                    string pathProbe = Path.Combine(engine.GlobalSettings.Get("Engine.ResourcePath"), uri);
                    if (!File.Exists(pathProbe))
                    {
                        Trace($"Warning: resource file for {pathProbe} does not exist.");
                    }
                    this.AddAssociation(tag!, uri);
                }
            }
        }
        catch (Exception e)
        {
            Trace($"Error loading resource: {e}");
        }
    }

    
    private void _whenLoadedAnimations(string path, JsonNode? node)
    {
        Trace("Loading animations...");
        if (null == node) return;
        
        string pathProbe;
        try
        {
            if (node is JsonArray arr)
            {
                foreach (var resNode in arr)
                {
                    string? uriModel = resNode?["modelUrl"]?.GetValue<string>();
                    if (uriModel is null)
                    {
                        throw new InvalidDataException("no modelUrl specified in resource.");
                    }

                    string? uriAnimations = resNode?["animationUrls"]?.GetValue<string>();
                    if (uriAnimations is null)
                    {
                        throw new InvalidDataException("no animationsUrl specified in resource.");
                    }

                    if (_traceLoadingAnimations)
                        Trace($"LoadAnimationsTo: Added Animation \"{uriModel}\" from {uriModel}.");

                    string probeModel = Path.Combine(engine.GlobalSettings.Get("Engine.ResourcePath"), uriModel);
                    if (!File.Exists(probeModel))
                    {
                        Trace($"Warning: animation file for {probeModel} does not exist.");
                    }

                    AvailableAnimations.Add($"{uriModel};{uriAnimations}");

                    string? uriBaked = null;
                    var strFileName =
                        ModelAnimationCollectionReader.ModelAnimationCollectionFileName(
                            Path.GetFileName(uriModel),
                            uriAnimations);

                    if (_traceLoadingAnimations)
                        Trace($"LoadAnimationsTo: Added Animation {uriModel} with {uriAnimations} at {strFileName}.");

                    /*
                     * If we are not compiling, probe for the baked animation file.
                     */
                    if (GlobalSettings.Get("joyce.CompileMode") != "true") {
                        uriBaked = Path.Combine(
                            GlobalSettings.Get("Engine.GeneratedResourcePath"), 
                            strFileName);
                        string probeBaked = Path.Combine(
                            GlobalSettings.Get("Engine.ResourcePath"),
                            uriBaked);
                        if (!File.Exists(probeBaked))
                        {
                            Trace($"Warning: resource file for {probeBaked} does not exist.");
                        }
                    }

                    if (uriBaked != null)
                    {
                        this.AddAssociation(strFileName, uriBaked);
                    }
                    else
                    {
                        Trace($"Warning: Unable to bake animations for {strFileName}.");
                    }
                }
            }
        }
        catch (Exception e)
        {
            Trace($"Error loading animation: {e}");
        }
    }

    
    private void _loadTextureAtlas(JsonNode? nodeAtlas)
    {
        Trace("Loading texture atlas...");
        var tc = I.Get<TextureCatalogue>();

        string? atlasTag = nodeAtlas?["tag"]?.GetValue<string>();
        string? atlasUri = nodeAtlas?["uri"]?.GetValue<string>();

        if (string.IsNullOrEmpty(atlasTag) || string.IsNullOrEmpty(atlasUri))
        {
            Warning("Atlas missing required 'tag' or 'uri' property.");
            return;
        }

        if (_traceLoadingResources)
            Trace($"LoadTextureAtlas: Added Resource \"{atlasTag}\" from {atlasUri}.");

        string pathProbe = Path.Combine(engine.GlobalSettings.Get("Engine.ResourcePath"), atlasUri);
        if (!File.Exists(pathProbe))
        {
            Trace($"Warning: resource file for {pathProbe} does not exist.");
        }
        this.AddAssociation(atlasTag, atlasUri);

        var texturesNode = nodeAtlas?["textures"];
        if (texturesNode is JsonObject objTextures)
        {
            foreach (var pairTexture in objTextures)
            {
                var jet = pairTexture.Value;
                string textureTag = pairTexture.Key;

                float u      = jet?["u"]?.GetValue<float>()      ?? 0f;
                float v      = jet?["v"]?.GetValue<float>()      ?? 0f;
                float uScale = jet?["uScale"]?.GetValue<float>() ?? 1f;
                float vScale = jet?["vScale"]?.GetValue<float>() ?? 1f;

                int width  = jet?["width"]?.GetValue<int>()  ?? 0;
                int height = jet?["height"]?.GetValue<int>() ?? 0;

                bool isMipmap = nodeAtlas?["isMipmap"]?.GetValue<bool>() ?? false;

                tc.AddAtlasEntry(
                    textureTag, atlasTag,
                    new Vector2(u, v),
                    new Vector2(uScale, vScale),
                    width,
                    height,
                    isMipmap
                );
            }
        }
    }

    
    /**
     * Read the textures: each of the object keys contains the resource
     * tag for a json file containing the texture atlas. We shall try
     * to open that one.
     */
    private void _whenLoadedTextures(string path, JsonNode? nodeTextures)
    {
        Trace("Loading textures...");
        if (null == nodeTextures) return;
        
        try
        {
            var channelsNode = nodeTextures?["channels"];
            if (channelsNode is JsonObject objChannels)
            {
                foreach (var kvpChannels in objChannels)
                {
                    var channelDescNode = kvpChannels.Value;
                    string? file = channelDescNode?["file"]?.GetValue<string>();
                    if (string.IsNullOrEmpty(file))
                    {
                        Warning($"Channel {kvpChannels.Key} missing 'file' property.");
                        continue;
                    }

                    try
                    {
                        using var jdocAtlas = JsonDocument.Parse(
                            engine.Assets.Open(file),
                            new JsonDocumentOptions { AllowTrailingCommas = true });

                        // Convert RootElement to JsonNode for consistency
                        var atlasNode = JsonNode.Parse(jdocAtlas.RootElement.GetRawText());
                        var atlasListNode = atlasNode?["atlasses"];

                        if (atlasListNode is JsonObject objAtlasList)
                        {
                            foreach (var pairAtlas in objAtlasList)
                            {
                                _loadTextureAtlas(pairAtlas.Value);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Warning($"Unable to parse resource object for texture '{file}': {e}");
                    }
                }
            }
        }
        catch (Exception e)
        {
            Warning($"Unable to load textures: {e}");
        }
    }

    
    public void WithLoader()
    {
        I.Get<engine.casette.Loader>().WhenLoaded("/resources/list", _whenLoadedResources);
        I.Get<engine.casette.Loader>().WhenLoaded("/animations/list", _whenLoadedAnimations);
        I.Get<engine.casette.Loader>().WhenLoaded("/textures", _whenLoadedTextures);
    }

    
    public AAssetImplementation()
    {
        engine.Assets.SetAssetImplementation(this);
    }
    

    public abstract Stream Open(in string filename);
    public abstract bool Exists(in string filename);
    public abstract void AddAssociation(string tag, string uri);
    public abstract IReadOnlyDictionary<string, string> GetAssets();
}