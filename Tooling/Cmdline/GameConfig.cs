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


        public void LoadResourceList(JsonElement je)
        {
            Trace($"LoadResourceList();");
            try
            {
                foreach (var jeRes in je.EnumerateArray())
                {
                    string uri = jeRes.GetProperty("uri").GetString();
                    if (null == uri)
                    {
                        throw new InvalidDataException("no uri specified in resource.");
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

                    Trace($"GameConfig: Added Resource \"{tag}\" from {uri}.");
                    if (!File.Exists(uri))
                    {
                        Trace($"Warning: resource file for {uri} does not exist.");
                    }
                    MapResources[tag] = new Resource() { Uri = uri };
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


        public GameConfig(string jsonPath)
        {
            _jsonPath = jsonPath;
        }

    }
}