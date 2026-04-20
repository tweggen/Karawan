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
    private static readonly engine.Dc _dc = engine.Dc.AssetLoading;

    private bool _traceLoadingResources = true;
    private bool _traceLoadingAnimations = false;
    private bool _traceLoadingTextures = false;
    
    
    public SortedSet<string> AvailableAnimations = new();

    /// <summary>
    /// Scenarios that should be baked at build time. Populated by
    /// _whenLoadedScenarios from the /scenarios/categories tree in the game
    /// config. Chushi reads this list to drive its scenario bake loop, the same
    /// way it iterates AvailableAnimations to drive animation baking.
    /// </summary>
    public readonly List<ScenarioBakeRequest> AvailableScenarios = new();


    /// <summary>
    /// One scenario the build pipeline should produce. Carries everything the
    /// engine-side ScenarioCompiler needs (category, index, NPC count, seed),
    /// without forcing Chushi to re-parse the JSON config.
    /// </summary>
    public sealed class ScenarioBakeRequest
    {
        public string CategoryName { get; init; }
        public int Index { get; init; }
        public int NpcCount { get; init; }
        public int Seed { get; init; }
        public int SimulationDays { get; init; }
    }


    private void _whenLoadedResources(string path, JsonNode? node)
    {
        Trace(_dc, $"Loading resources...");
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
                        Trace(_dc,$"LoadResourcesTo: Added Resource \"{tag}\" from {uri}.");

                    string pathProbe = Path.Combine(engine.GlobalSettings.Get("Engine.ResourcePath"), uri);
                    if (!File.Exists(pathProbe))
                    {
                        Trace(_dc,$"Warning: resource file for {pathProbe} does not exist.");
                    }
                    this.AddAssociation(tag!, uri);
                }
            }
        }
        catch (Exception e)
        {
            Trace(_dc,$"Error loading resource: {e}");
        }
    }

    
    private void _whenLoadedAnimations(string path, JsonNode? node)
    {
        Trace(_dc, $"Loading animations...");
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
                        Trace(_dc,$"LoadAnimationsTo: Added Animation \"{uriModel}\" from {uriModel}.");

                    string probeModel = Path.Combine(engine.GlobalSettings.Get("Engine.ResourcePath"), uriModel);
                    if (!File.Exists(probeModel))
                    {
                        Trace(_dc,$"Warning: animation file for {probeModel} does not exist.");
                    }

                    AvailableAnimations.Add($"{uriModel};{uriAnimations}");

                    string? uriBaked = null;
                    var strFileName =
                        ModelAnimationCollectionReader.ModelAnimationCollectionFileName(
                            Path.GetFileName(uriModel),
                            uriAnimations);

                    if (_traceLoadingAnimations)
                        Trace(_dc,$"LoadAnimationsTo: Added Animation {uriModel} with {uriAnimations} at {strFileName}.");

                    /*
                     * If we are not compiling, probe for the baked animation file.
                     */
                    if (GlobalSettings.Get("joyce.CompileMode") != "true") {
                        uriBaked = Path.Combine(
                            GlobalSettings.Get("Engine.GeneratedResourcePath"),
                            strFileName);
                        if (!File.Exists(uriBaked))
                        {
                            Trace(_dc,$"Warning: resource file for {uriBaked} does not exist.");
                        }
                    }

                    if (uriBaked != null)
                    {
                        this.AddAssociation(strFileName, uriBaked);
                    }
                    else
                    {
                        Trace(_dc,$"Warning: Unable to bake animations for {strFileName}.");
                    }
                }
            }
        }
        catch (Exception e)
        {
            Trace(_dc,$"Error loading animation: {e}");
        }
    }

    
    private void _loadTextureAtlas(JsonNode? nodeAtlas)
    {
        Trace(_dc, $"Loading texture atlas...");
        var tc = I.Get<TextureCatalogue>();

        string? atlasTag = nodeAtlas?["tag"]?.GetValue<string>();
        string? atlasUri = nodeAtlas?["uri"]?.GetValue<string>();

        if (string.IsNullOrEmpty(atlasTag) || string.IsNullOrEmpty(atlasUri))
        {
            Warning("Atlas missing required 'tag' or 'uri' property.");
            return;
        }

        if (_traceLoadingResources)
            Trace(_dc,$"LoadTextureAtlas: Added Resource \"{atlasTag}\" from {atlasUri}.");

        string pathProbe = Path.Combine(engine.GlobalSettings.Get("Engine.ResourcePath"), atlasUri);
        if (!File.Exists(pathProbe))
        {
            Trace(_dc,$"Warning: resource file for {pathProbe} does not exist.");
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
        Trace(_dc, $"Loading textures...");
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

    
    private void _whenLoadedScenarios(string path, JsonNode? node)
    {
        Trace(_dc,$"Loading scenario categories from path '{path}'...");
        if (null == node)
        {
            Trace(_dc, $"_whenLoadedScenarios: node is null");
            return;
        }
        try
        {
            if (node is not JsonArray arr)
            {
                Trace(_dc,$"_whenLoadedScenarios: node is not array, it's {node.GetType().Name}");
                return;
            }
            int simulationDays = 365; // Could be overridden via a sibling node; left fixed for now.

            foreach (var catNode in arr)
            {
                string? name = catNode?["name"]?.GetValue<string>();
                if (string.IsNullOrEmpty(name))
                {
                    Trace(_dc, $"Warning: scenario category without 'name' skipped.");
                    continue;
                }
                int count = catNode?["count"]?.GetValue<int>() ?? 0;
                int baseSeed = catNode?["baseSeed"]?.GetValue<int>() ?? 0;
                int npcMin = 0, npcMax = 0;
                if (catNode?["npcCountRange"] is JsonArray rangeArr && rangeArr.Count == 2)
                {
                    npcMin = rangeArr[0]?.GetValue<int>() ?? 0;
                    npcMax = rangeArr[1]?.GetValue<int>() ?? 0;
                }
                if (count <= 0 || npcMin <= 0 || npcMax < npcMin)
                {
                    Trace(_dc,$"Warning: scenario category '{name}' has invalid count={count} or range=[{npcMin},{npcMax}]; skipped.");
                    continue;
                }

                for (int i = 0; i < count; i++)
                {
                    // Linearly interpolate NPC count across the range so each
                    // scenario in a category covers a different population size
                    // deterministically.
                    int npcCount = (count == 1)
                        ? (npcMin + npcMax) / 2
                        : npcMin + (i * (npcMax - npcMin)) / (count - 1);
                    int seed = baseSeed + i;

                    AvailableScenarios.Add(new ScenarioBakeRequest
                    {
                        CategoryName = name,
                        Index = i,
                        NpcCount = npcCount,
                        Seed = seed,
                        SimulationDays = simulationDays
                    });

                    string fileName = engine.tale.bake.ScenarioFileName.Of(name, i, seed);

                    // Mirrors _whenLoadedAnimations: in CompileMode (Chushi) the
                    // file does not yet exist, so we skip the probe and skip the
                    // association. At runtime we probe and warn if missing —
                    // Phase D2's ScenarioLibrary will fall back to in-process
                    // baking exactly the way Model.BakeAnimations does for
                    // missing animation collections.
                    if (GlobalSettings.Get("joyce.CompileMode") != "true")
                    {
                        string probeBaked = Path.Combine(
                            GlobalSettings.Get("Engine.GeneratedResourcePath"),
                            fileName);
                        if (!File.Exists(probeBaked))
                        {
                            Trace(_dc,$"Warning: scenario file for {probeBaked} does not exist.");
                        }
                        this.AddAssociation(fileName, probeBaked);
                    }
                }
            }
            Trace(_dc,$"_whenLoadedScenarios: successfully registered {AvailableScenarios.Count} scenarios total.");
        }
        catch (Exception e)
        {
            Trace(_dc,$"Error loading scenarios: {e}");
        }
    }


    public void WithLoader()
    {
        Trace(_dc, $"AAssetImplementation.WithLoader(): Registering JSON callbacks...");
        I.Get<engine.casette.Loader>().WhenLoaded("/resources/list", _whenLoadedResources);
        Trace(_dc, $"  - Registered /resources/list");
        I.Get<engine.casette.Loader>().WhenLoaded("/animations/list", _whenLoadedAnimations);
        Trace(_dc, $"  - Registered /animations/list");
        I.Get<engine.casette.Loader>().WhenLoaded("/textures", _whenLoadedTextures);
        Trace(_dc, $"  - Registered /textures");
        I.Get<engine.casette.Loader>().WhenLoaded("/scenarios/categories", _whenLoadedScenarios);
        Trace(_dc, $"  - Registered /scenarios/categories");
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