using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace CmdLine
{
    public class GameConfig
    {
        private JsonElement _jeRoot;
        private string _jsonPath;

        public Action<string> Trace; // = (msg) => Debug.WriteLine(msg);

        public SortedDictionary<string, Resource> MapResources = new SortedDictionary<string, Resource>();
        public SortedDictionary<string, AtlasSpec> MapAtlasSpecs = new SortedDictionary<string, AtlasSpec>();


        public Resource LoadResource(JsonElement jeRes)
        {
            string uri = jeRes.GetProperty("uri").GetString();
            if (null == uri)
            {
                throw new InvalidDataException("no uri specified in resource.");
            }

            string type = null;
            if (jeRes.TryGetProperty("type", out var jpType))
            {
                type = jpType.GetString();
            }
            else
            {
                type = "file";
            }

            string tag = null;
            if (jeRes.TryGetProperty("tag", out var jpTag))
            {
                tag = jpTag.GetString();
            }
            if (null == tag)
            {
                int idx = uri.LastIndexOf('/');
                if (idx != -1 && idx != uri.Length - 1)
                {
                    tag = uri.Substring(idx + 1);
                }
                else
                {
                    tag = uri;
                }
            }

            Trace($"GameConfig: Loaded Resource \"{tag}\" from {uri}.");
            if (!File.Exists(uri))
            {
                Trace($"Warning: resource file for {uri} does not exist.");
            }
            return new Resource() { Type = type, Uri = uri, Tag = tag };
        }
        
        
        public void LoadResourceList(JsonElement je)
        {
            Trace($"LoadResourceList();");
            try
            {
                foreach (var jeRes in je.EnumerateArray())
                {
                    Resource resource = LoadResource(jeRes);
                    string tag = resource.Tag;
                    MapResources[tag] = resource;
                    Trace($"GameConfig: Added Resource \"{tag}\".");
                }
            }
            catch (Exception e)
            {
                Trace($"Error loading resource: {e}");
            }
        }


        public void LoadResources(JsonElement je)
        {
            Trace($"LoadGameResources();");
            try
            {
                if (je.TryGetProperty("list", out var jeResourceList))
                {
                    LoadResourceList(jeResourceList);
                }
                else
                {
                    Trace($"GameConfig: Unable to find \"list\" in game config json.");
                }
            }
            catch (Exception e)
            {
                Trace($"GameConfig: Unable to load resource list: {e}");
            }
        }


        public void LoadAtlas(string path, JsonElement je)
        {
            AtlasSpec atlasSpec = new AtlasSpec() { Path = path};
            try
            {
                foreach (var jeRes in je.EnumerateArray())
                {
                    Resource resource = LoadResource(jeRes);
                    atlasSpec.TextureResources.Add(resource);
                }
                MapAtlasSpecs[path] = atlasSpec;
            }
            catch (Exception e)
            {
                Trace($"GameConfig: Unable to load texture atlas: {e}");
            }
        }
        
        
        public void LoadTextures(JsonElement je)
        {
            Trace($"LoadTextures():");
            try
            {
                foreach (var jpAtlas in je.EnumerateObject())
                {
                    Trace($"LoadTextures(): Loading atlas {jpAtlas.Name}");
                    LoadAtlas(jpAtlas.Name, jpAtlas.Value);
                }
            }
            catch (Exception e)
            {
                Trace($"GameConfig: Unable to load texture list: {e}");
            }
        }

        
        public void LoadGameConfig(JsonElement je)
        {
            Trace($"LoadGameConfig();");
            if (je.TryGetProperty("resources", out var jeResources))
            {
                LoadResources(jeResources);
            }
            else
            {
                Trace($"GameConfig: Unable to find \"resources\" in game config json.");
            }
            if (je.TryGetProperty("textures", out var jeTextures))
            {
                LoadTextures(jeTextures);
            }
            else
            {
                Trace($"GameConfig: Unable to find \"textures\" in game config json.");
            }
        }


        private void _loadGameConfigFile(string jsonPath)
        {
            Trace($"_loadGameConfigFile(\"{jsonPath}\");");
            using (var stream = new FileStream(jsonPath, FileMode.Open))
            {
                JsonDocument jdocGame = JsonDocument.Parse(stream, new JsonDocumentOptions()
                {
                    AllowTrailingCommas = true
                });
                _jeRoot = jdocGame.RootElement;
            }

            LoadGameConfig(_jeRoot);
        }


        public void Load()
        {
            _loadGameConfigFile(_jsonPath);
        }


        /**
         * Add the resources required by this atlas.
         * This includes the atlas file itself as well as its contants.
         */
        public void LoadAtlasResource(Resource atlasListResource)
        {
            JsonElement jeAtlasFile;
            using (var stream = new FileStream(atlasListResource.Uri, FileMode.Open))
            {
                JsonDocument jdocAtlas = JsonDocument.Parse(stream, new JsonDocumentOptions()
                {
                    AllowTrailingCommas = true
                });
                jeAtlasFile = jdocAtlas.RootElement;
            }

            if (jeAtlasFile.TryGetProperty("atlasses", out var jpAtlasses))
            {
                foreach (var kvpAtlasses in jpAtlasses.EnumerateObject())
                {
                    var jeAtlas = kvpAtlasses.Value;
                    if (!jeAtlas.TryGetProperty("uri", out var jeAtlasUri))
                    {
                        continue;
                    }

                    string uriAtlas = jeAtlasUri.GetString();
                    if (null == uriAtlas)
                    {
                        continue;
                    }
                    
                    /*
                     * We do not need to read the textures, however, we need to add the atlas file
                     * as a resource.
                     */
                    string tag = System.IO.Path.GetFileName(uriAtlas);
                    Resource atlasResource = new Resource()
                        { Uri = uriAtlas, Tag = tag };
                    MapResources[tag] = atlasResource;
                }
            }
            
        }


        /**
         * Resolve the resources referenced indirectly
         */
        public void LoadIndirectResources()
        {
            foreach (var kvp in MapResources)
            {
                var resource = kvp.Value;
                if (resource.Type == "atlas")
                {
                    LoadAtlasResource(resource);
                }
            }
        }


        public GameConfig(string jsonPath)
        {
            _jsonPath = jsonPath;
        }

    }
}