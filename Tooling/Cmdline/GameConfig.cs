using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CmdLine
{
    public class GameConfig
    {
        public string CurrentPath;
        public string DestinationPath;
        private string _jsonPath;

        public Action<string> Trace; // = (msg) => Debug.WriteLine(msg);

        public SortedDictionary<string, Resource> MapResources = new SortedDictionary<string, Resource>();
        public SortedDictionary<string, AtlasSpec> MapAtlasSpecs = new SortedDictionary<string, AtlasSpec>();
        public TextureSection TextureSection;

        private Mix _mix;


        private static SHA256 _sha256 = SHA256.Create();
        
        public static string ModelAnimationCollectionFileName(string localUrlModel, string urlAnimations)
        {
            string strModelAnims;
            if (!String.IsNullOrWhiteSpace(urlAnimations))
            {
                strModelAnims = $"{localUrlModel};{urlAnimations}";
            }
            else
            {
                strModelAnims = $"{localUrlModel}";
            }
        
            string strHash = 
                Convert.ToBase64String(_sha256.ComputeHash(Encoding.UTF8.GetBytes(strModelAnims)))
                    .Replace('+', '-')
                    .Replace('/', '_')
                    .Replace('=', '~');
            Console.Error.WriteLine($"Returning hash {strHash} for {strModelAnims}");

            return  $"ac-{strHash}";;
        }


        public Resource LoadResource(JsonNode nodeRes)
        {
            if (nodeRes == null)
            {
                throw new ArgumentNullException(nameof(nodeRes));
            }

            // Zugriff auf Properties über JsonObject
            var obj = nodeRes.AsObject();

            string uri = obj["uri"]?.GetValue<string>();
            if (uri is null)
            {
                throw new InvalidDataException("no uri specified in resource.");
            }

            string type = obj["type"]?.GetValue<string>() ?? "file";

            string tag = obj["tag"]?.GetValue<string>();
            if (tag is null)
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

            Trace($"GameConfig: Loaded Resource \"{tag}\" from {uri} type \"{type}\".");
            if (!File.Exists(Path.Combine(CurrentPath, uri)))
            {
                Trace($"Warning: resource file for {uri} does not exist.");
            }

            return new Resource { Type = type, Uri = uri, Tag = tag };
        }
        
        
        public Resource LoadAnimation(JsonNode nodeRes)
        {
            if (nodeRes is null)
            {
                throw new ArgumentNullException(nameof(nodeRes));
            }

            var obj = nodeRes.AsObject();

            string modelUrl = obj["modelUrl"]?.GetValue<string>();
            if (modelUrl is null)
            {
                throw new InvalidDataException("no uri specified in resource.");
            }

            // null animationUrls are perfectly ok
            string animationUrls = obj["animationUrls"]?.GetValue<string>();

            string tag = obj["tag"]?.GetValue<string>();
            if (tag is null)
            {
                int idx = modelUrl.LastIndexOf('/');
                if (idx != -1 && idx != modelUrl.Length - 1)
                {
                    tag = modelUrl.Substring(idx + 1);
                }
                else
                {
                    tag = modelUrl;
                }
            }

            Trace($"GameConfig: Generating Animation Resource \"{tag}\" from {modelUrl} animations \"{animationUrls}\".");
            if (!File.Exists(Path.Combine(CurrentPath, modelUrl)))
            {
                Trace($"Warning: resource file for {modelUrl} does not exist.");
            }

            string strFilename = ModelAnimationCollectionFileName(tag, animationUrls);
            return new Resource
            {
                Type = "bakedAnimationCollection",
                Uri = Path.Combine(DestinationPath + "/", strFilename),
                Tag = strFilename
            };
        }

        
        public void LoadResourceList(JsonNode node)
        {
            Trace($"LoadResourceList();");
            try
            {
                if (node is null)
                {
                    throw new ArgumentNullException(nameof(node));
                }

                var arr = node.AsArray();

                foreach (var nodeRes in arr)
                {
                    Resource resource = LoadResource(nodeRes);
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

        
        public void LoadAnimationList(JsonNode node)
        {
            Trace($"LoadAnimationList()");
            try
            {
                if (node is null)
                {
                    throw new ArgumentNullException(nameof(node));
                }

                var arr = node.AsArray();

                foreach (var nodeRes in arr)
                {
                    // assumes you already adapted LoadAnimation(JsonNode?) similarly
                    Resource resource = LoadAnimation(nodeRes);
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

        
        public Texture LoadTexture(JsonNode nodeTexture, string name)
        {
            if (nodeTexture is null)
            {
                throw new ArgumentNullException(nameof(nodeTexture));
            }

            var texture = new Texture { Name = name };

            /*
             * Now load the channels within.
             */
            try
            {
                var obj = nodeTexture.AsObject();

                foreach (var kvp in obj)
                {
                    try
                    {
                        string strChannel = kvp.Key;
                        Resource resource = LoadResource(kvp.Value);
                        texture.Channels[strChannel] = resource;
                    }
                    catch (Exception e)
                    {
                        Trace($"LoadTextures(): Exception while loading texture: {e}.");
                    }
                }
            }
            catch (Exception e)
            {
                Trace($"LoadTextures(): Exception while loading textures: {e}.");
            }

            return texture;
        }

        
        public void LoadTextures(TextureSection ts, JsonNode nodeTextures)
        {
            Trace($"LoadTextures(): Loading texture definitions.");
            try
            {
                if (nodeTextures is null)
                {
                    throw new ArgumentNullException(nameof(nodeTextures));
                }

                var obj = nodeTextures.AsObject();

                foreach (var kvp in obj)
                {
                    try
                    {
                        Texture texture = LoadTexture(kvp.Value, kvp.Key);
                        ts.Textures[kvp.Key] = texture;
                    }
                    catch (Exception e)
                    {
                        Trace($"LoadTextures(): Exception while loading texture: {e}.");
                    }
                }
            }
            catch (Exception e)
            {
                Trace($"LoadTextures(): Exception while loading textures: {e}.");
            }
        }

        
        public Channel LoadChannel(JsonNode nodeChannel)
        {
            if (nodeChannel is null)
            {
                throw new ArgumentNullException(nameof(nodeChannel));
            }

            var obj = nodeChannel.AsObject();
            string strFile = obj["file"]?.GetValue<string>();

            if (strFile is null)
            {
                throw new InvalidDataException("Channel missing required 'file' property.");
            }

            return new Channel { File = strFile };
        }
        

        public void LoadChannels(TextureSection ts, JsonNode nodeChannels)
        {
            Trace($"LoadChannels(): Loading channel definitions.");
            try
            {
                if (nodeChannels is null)
                {
                    throw new ArgumentNullException(nameof(nodeChannels));
                }

                var obj = nodeChannels.AsObject();

                foreach (var kvp in obj)
                {
                    try
                    {
                        Channel channel = LoadChannel(kvp.Value);
                        ts.Channels[kvp.Key] = channel;
                    }
                    catch (Exception e)
                    {
                        Trace($"LoadChannels(): Exception while loading channel: {e}.");
                    }
                }
            }
            catch (Exception e)
            {
                Trace($"LoadChannels(): Exception while loading channels: {e}.");
            }
        }

        
        public void LoadAtlas(string path, JsonNode node)
        {
            Trace($"LoadAtlas(): Loading atlas {path}.");
            AtlasSpec atlasSpec = new AtlasSpec { Path = path };
            try
            {
                if (node is null)
                {
                    throw new ArgumentNullException(nameof(node));
                }

                var arr = node.AsArray();

                foreach (var nodeRes in arr)
                {
                    // assumes LoadResource(JsonNode?) is already adapted
                    Resource resource = LoadResource(nodeRes);
                    atlasSpec.TextureResources.Add(resource);
                }

                MapAtlasSpecs[path] = atlasSpec;
            }
            catch (Exception e)
            {
                Trace($"GameConfig: Unable to load texture atlas: {e}");
            }
        }

        
        public void LoadTextureSection(JsonNode node)
        {
            Trace($"LoadTextures():");
            TextureSection ts = new TextureSection();
            try
            {
                if (node is null)
                {
                    throw new ArgumentNullException(nameof(node));
                }

                var obj = node.AsObject();

                foreach (var kvp in obj)
                {
                    if (kvp.Key == "textures")
                    {
                        LoadTextures(ts, kvp.Value);
                    }
                    else if (kvp.Key == "channels")
                    {
                        LoadChannels(ts, kvp.Value);
                    }
                    else
                    {
                        LoadAtlas(kvp.Key, kvp.Value);
                    }
                }

                ts.Digest();
                TextureSection = ts;
            }
            catch (Exception e)
            {
                Trace($"GameConfig: Unable to load texture list: {e}");
            }
        }

        
        public void LoadGameConfig()
        {
            Trace($"LoadGameConfig();");
            _mix.GetTree("/resources/list", LoadResourceList);
            _mix.GetTree("/animations/list", LoadAnimationList);
            _mix.GetTree("/textures", LoadTextureSection);
        }


        private void _loadGameConfigFile(string jsonPath)
        {
            Trace($"_loadGameConfigFile(\"{jsonPath}\");");
            using (var stream = new FileStream(Path.Combine(CurrentPath,jsonPath), FileMode.Open))
            {
                _mix = new Mix();
                _mix.UpsertFragment("/", stream);
            }

            /*
             * There are resources included from the upsert. Add them.
             */
            foreach (string file in _mix.AdditionalFiles)
            {
                string tag = null;
                string uri = file;
                if (tag is null)
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

                MapResources.Add(file, new Resource { Type = "file", Uri = uri, Tag = file });
            }


            LoadGameConfig();
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
            Trace($"LoadAtlasResource({atlasListResource.Uri});");
            JsonElement jeAtlasFile;
            using (var stream = new FileStream(Path.Combine(CurrentPath,atlasListResource.Uri), FileMode.Open))
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
                        Trace($"LoadAtlasResource({atlasListResource.Uri}): No uri in element");
                        continue;
                    }

                    string uriAtlas = jeAtlasUri.GetString();
                    if (null == uriAtlas)
                    {
                        Trace($"LoadAtlasResource({atlasListResource.Uri}): null uri in element");
                        continue;
                    }
                    
                    /*
                     * We do not need to read the textures, however, we need to add the atlas file
                     * as a resource.
                     */
                    string tag = System.IO.Path.GetFileName(uriAtlas);
                    Resource atlasResource = new Resource()  { Uri = uriAtlas, Tag = tag };
                    Trace($"Adding atlas resource with tag \"{tag}\" at location \"{uriAtlas}\"");                   
                    MapResources[tag] = atlasResource;
                }
            }
            else
            {
                Trace($"LoadAtlasResource({atlasListResource.Uri}): no atlasses in element");
            }
            
        }


        /**
         * Resolve the resources referenced indirectly
         */
        public void LoadIndirectResources()
        {
            List<Resource> listAtlasResources = new List<Resource>();
            foreach (var kvp in MapResources)
            {
                var resource = kvp.Value;
                if (resource.Type == "atlas")
                {
                    listAtlasResources.Add(resource);
                }
            }

            foreach (var atlasResource in listAtlasResources)
            {
                LoadAtlasResource(atlasResource);
            }
        }


        public GameConfig(string jsonPath)
        {
            _jsonPath = jsonPath;
        }

    }
}