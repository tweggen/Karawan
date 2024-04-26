using System;
using System.IO;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.Json;
using SkiaSharp;

namespace CmdLine
{
    public class JsonTextureDesc {
        public string Uri { get; set; }
        public string Tag { get; set; }
        public string AtlasTag { get; set; }
        public float U { get; set; }
        public float V { get; set; }
        public float UScale { get; set; }
        public float VScale { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }

    public class JsonAtlasDesc
    {
        public string Uri { get; set; }
        public string Tag { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public Dictionary<string,JsonTextureDesc> Textures { get; set; }
    }


    public class AtlasFileDesc
    {
        public string Uri { get; set; }
        public string Tag { get; set; }
    }
    
    
    public class JsonAtlassesDesc
    {
        public Dictionary<string, JsonAtlasDesc> Atlasses { get; set; }
        public Dictionary<string, AtlasFileDesc> AtlasFiles { get; set; }
    }
    
    /// <summary>
    /// Represents a Texture in an atlas
    /// </summary>
    public class TextureInfo
    {
        public Resource Resource;
        /// <summary>
        /// Path of the source texture on disk
        /// </summary>
        public string FullPath;

        /// <summary>
        /// Width in Pixels
        /// </summary>
        public int Width;

        /// <summary>
        /// Height in Pixels
        /// </summary>
        public int Height;
    }

    /// <summary>
    /// Indicates in which direction to split an unused area when it gets used
    /// </summary>
    public enum SplitType
    {
        /// <summary>
        /// Split Horizontally (textures are stacked up)
        /// </summary>
        Horizontal,

        /// <summary>
        /// Split verticaly (textures are side by side)
        /// </summary>
        Vertical,
    }

    /// <summary>
    /// Different types of heuristics in how to use the available space
    /// </summary>
    public enum BestFitHeuristic
    {
        /// <summary>
        /// 
        /// </summary>
        Area,

        /// <summary>
        /// 
        /// </summary>
        MaxOneAxis,
    }

    /// <summary>
    /// A node in the Atlas structure
    /// </summary>
    public class Node
    {
        /// <summary>
        /// Bounds of this node in the atlas
        /// </summary>
        public Rectangle Bounds;

        /// <summary>
        /// Texture this node represents
        /// </summary>
        public TextureInfo Texture;

        /// <summary>
        /// If this is an empty node, indicates how to split it when it will  be used
        /// </summary>
        public SplitType SplitType;
    }

    /// <summary>
    /// The texture atlas
    /// </summary>
    public class Atlas
    {
        /// <summary>
        /// Width in pixels
        /// </summary>
        public int Width;

        /// <summary>
        /// Height in Pixel
        /// </summary>
        public int Height;

        /// <summary>
        /// List of the nodes in the Atlas. This will represent all the textures that are packed into it and all the remaining free space
        /// </summary>
        public List<Node> Nodes;
    }

    /// <summary>
    /// Objects that performs the packing task. Takes a list of textures as input and generates a set of atlas textures/definition pairs
    /// </summary>
    public class Packer
    {
        public string DestinationAtlas;
        public string DestinationTexture;

        /// <summary>
        /// List of all the textures that need to be packed
        /// </summary>
        public List<TextureInfo> SourceTextures;

        /// <summary>
        /// Stream that recieves all the info logged
        /// </summary>
        public StringWriter Log;

        /// <summary>
        /// Stream that recieves all the error info
        /// </summary>
        public StringWriter Error;

        /// <summary>
        /// Number of pixels that separate textures in the atlas
        /// </summary>
        public int Padding;

        /// <summary>
        /// Size of the atlas in pixels. Represents one axis, as atlases are square
        /// </summary>
        public int AtlasSize;

        /// <summary>
        /// Toggle for debug mode, resulting in debug atlasses to check the packing algorithm
        /// </summary>
        public bool DebugMode;

        /// <summary>
        /// Which heuristic to use when doing the fit
        /// </summary>
        public BestFitHeuristic FitHeuristic;

        /// <summary>
        /// List of all the output atlases
        /// </summary>
        public List<Atlas> Atlasses;

        public List<Resource> StandaloneTextures = new List<Resource>();

        public Packer()
        {
            SourceTextures = new List<TextureInfo>();
            Log = new StringWriter();
            Error = new StringWriter();
        }

        public void Process(string _SourceDir, string _Pattern, int _AtlasSize, int _Padding, bool _DebugMode)
        {
            Padding = _Padding;
            AtlasSize = _AtlasSize;
            DebugMode = _DebugMode;

            List<TextureInfo> textures = new List<TextureInfo>();
            textures = SourceTextures.ToList();

            //2: generate as many atlasses as needed (with the latest one as small as possible)
            Atlasses = new List<Atlas>();
            while (textures.Count > 0)
            {
                Atlas atlas = new Atlas();
                atlas.Width = _AtlasSize;
                atlas.Height = _AtlasSize;

                List<TextureInfo> leftovers = LayoutAtlas(textures, atlas);

                if (leftovers.Count == 0)
                {
                    // we reached the last atlas. Check if this last atlas could have been twice smaller
                    while (leftovers.Count == 0)
                    {
                        atlas.Width /= 2;
                        atlas.Height /= 2;
                        leftovers = LayoutAtlas(textures, atlas);
                    }

                    // we need to go 1 step larger as we found the first size that is to small
                    atlas.Width *= 2;
                    atlas.Height *= 2;
                    leftovers = LayoutAtlas(textures, atlas);
                }

                Atlasses.Add(atlas);

                textures = leftovers;
            }
        }

        public void SaveAtlasses(string _Destination)
        {
            int atlasCount = 0;
            string prefix = _Destination.Replace(Path.GetExtension(_Destination), "");

            JsonAtlassesDesc jAtlasses = new JsonAtlassesDesc()
            {
                Atlasses = new Dictionary<string, JsonAtlasDesc>()
            };
            
            foreach (Atlas atlas in Atlasses)
            {
                string atlasName = String.Format(prefix + "{0:000}" + ".png", atlasCount);
                string atlasTag = System.IO.Path.GetFileName(atlasName);

                //1: Save images
                SKSurface skiaSurface = CreateAtlasImage(atlas);
                using (var image = skiaSurface.Snapshot())
                using (var data = image.Encode(SKEncodedImageFormat.Png, 80))
                using (var stream = File.OpenWrite(atlasName))
                {
                    // save the data to a stream
                    data.SaveTo(stream);
                }
                //img.Save(atlasName, System.Drawing.Imaging.ImageFormat.Png);

                JsonAtlasDesc jAtlas = new JsonAtlasDesc()
                {
                    Uri = atlasName,
                    Tag = atlasTag,
                    Width = atlas.Width,
                    Height = atlas.Height,
                    Textures = new Dictionary<string, JsonTextureDesc>()
                };
                
                //2: save description in file
                foreach (Node n in atlas.Nodes)
                {
                    if (n.Texture != null)
                    {
                        JsonTextureDesc jTexture = new JsonTextureDesc()
                        {
                            Uri = n.Texture.Resource.Uri,
                            Tag = n.Texture.Resource.Tag,
                            AtlasTag = atlasTag,
                            U = ((float)n.Bounds.X / atlas.Width),
                            V = ((float)n.Bounds.Y / atlas.Height),
                            UScale = ((float)n.Bounds.Width / atlas.Width),
                            VScale = ((float)n.Bounds.Height / atlas.Height),
                            Width = (int)n.Bounds.Width,
                            Height = (int)n.Bounds.Height
                        };
                        jAtlas.Textures[jTexture.Tag] = jTexture;
                    }
                }
                jAtlasses.Atlasses[atlasTag] = jAtlas;

                ++atlasCount;
            }

            foreach (var resource in StandaloneTextures)
            {
                JsonAtlasDesc jAtlas = new JsonAtlasDesc()
                {
                    Tag = resource.Tag,
                    Uri = resource.Uri,
                    Textures = new Dictionary<string, JsonTextureDesc>()
                };

                JsonTextureDesc jTexture = new JsonTextureDesc()
                {
                    Uri = resource.Uri,
                    Tag = resource.Tag,
                    AtlasTag = resource.Tag,
                    U = 0f,
                    V = 0f,
                    UScale = 1f,
                    VScale = 1f
                };
                jAtlas.Textures[resource.Tag] = jTexture;
                jAtlasses.Atlasses[resource.Tag] = jAtlas;
            }

            string descFile = _Destination;
            StreamWriter tw = new StreamWriter(descFile);
            {
                var options = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                string jsonString = JsonSerializer.Serialize(jAtlasses, options);
                tw.Write(jsonString);
            }
            tw.Close();

            tw = new StreamWriter(prefix + ".log");
            tw.WriteLine("--- LOG -------------------------------------------");
            tw.WriteLine(Log.ToString());
            tw.WriteLine("--- ERROR -----------------------------------------");
            tw.WriteLine(Error.ToString());
            tw.Close();
        }


        public void AddTexture(Resource resourceTexture)
        {
            if (resourceTexture.Uri == "rgba")
            {
                TextureInfo ti = new TextureInfo();

                ti.Resource = resourceTexture;
                ti.FullPath = "rgba";
                ti.Width = 512;
                ti.Height = 512;

                SourceTextures.Add(ti);

                Log.WriteLine($"Added \"{resourceTexture.Tag}\" (found at \"{ti.FullPath}\")");
            }
            else
            {
                FileInfo fi = new FileInfo(resourceTexture.Uri);
                using (var image = SKImage.FromEncodedData(fi.FullName))
                {
                    if (image.Width <= AtlasSize && image.Height <= AtlasSize)
                    {
                        TextureInfo ti = new TextureInfo();

                        ti.Resource = resourceTexture;
                        ti.FullPath = fi.FullName;
                        ti.Width = image.Width;
                        ti.Height = image.Height;

                        SourceTextures.Add(ti);

                        Log.WriteLine($"Added \"{resourceTexture.Tag}\" (found at \"{ti.FullPath}\")");
                    }
                    else
                    {
                        StandaloneTextures.Add(resourceTexture);
                        Log.WriteLine($"Added standalone \"{resourceTexture.Tag}\" (found at \"{fi.FullName}\")");
                    }
                }
            }
        }

        
        private void HorizontalSplit(Node _ToSplit, int _Width, int _Height, List<Node> _List)
        {
            Node n1 = new Node();
            n1.Bounds.X = _ToSplit.Bounds.X + _Width + Padding;
            n1.Bounds.Y = _ToSplit.Bounds.Y;
            n1.Bounds.Width = _ToSplit.Bounds.Width - _Width - Padding;
            n1.Bounds.Height = _Height;
            n1.SplitType = SplitType.Vertical;

            Node n2 = new Node();
            n2.Bounds.X = _ToSplit.Bounds.X;
            n2.Bounds.Y = _ToSplit.Bounds.Y + _Height + Padding;
            n2.Bounds.Width = _ToSplit.Bounds.Width;
            n2.Bounds.Height = _ToSplit.Bounds.Height - _Height - Padding;
            n2.SplitType = SplitType.Horizontal;

            if (n1.Bounds.Width > 0 && n1.Bounds.Height > 0)
                _List.Add(n1);
            if (n2.Bounds.Width > 0 && n2.Bounds.Height > 0)
                _List.Add(n2);
        }

        
        private void VerticalSplit(Node _ToSplit, int _Width, int _Height, List<Node> _List)
        {
            Node n1 = new Node();
            n1.Bounds.X = _ToSplit.Bounds.X + _Width + Padding;
            n1.Bounds.Y = _ToSplit.Bounds.Y;
            n1.Bounds.Width = _ToSplit.Bounds.Width - _Width - Padding;
            n1.Bounds.Height = _ToSplit.Bounds.Height;
            n1.SplitType = SplitType.Vertical;

            Node n2 = new Node();
            n2.Bounds.X = _ToSplit.Bounds.X;
            n2.Bounds.Y = _ToSplit.Bounds.Y + _Height + Padding;
            n2.Bounds.Width = _Width;
            n2.Bounds.Height = _ToSplit.Bounds.Height - _Height - Padding;
            n2.SplitType = SplitType.Horizontal;

            if (n1.Bounds.Width > 0 && n1.Bounds.Height > 0)
                _List.Add(n1);
            if (n2.Bounds.Width > 0 && n2.Bounds.Height > 0)
                _List.Add(n2);
        }
        
        
        private TextureInfo FindBestFitForNode(Node _Node, List<TextureInfo> _Textures)
        {
            TextureInfo bestFit = null;

            float nodeArea = _Node.Bounds.Width * _Node.Bounds.Height;
            float maxCriteria = 0.0f;

            foreach (TextureInfo ti in _Textures)
            {
                switch (FitHeuristic)
                {
                    // Max of Width and Height ratios
                    case BestFitHeuristic.MaxOneAxis:
                        if (ti.Width <= _Node.Bounds.Width && ti.Height <= _Node.Bounds.Height)
                        {
                            float wRatio = (float)ti.Width / (float)_Node.Bounds.Width;
                            float hRatio = (float)ti.Height / (float)_Node.Bounds.Height;
                            float ratio = wRatio > hRatio ? wRatio : hRatio;
                            if (ratio > maxCriteria)
                            {
                                maxCriteria = ratio;
                                bestFit = ti;
                            }
                        }
                        break;

                    // Maximize Area coverage
                    case BestFitHeuristic.Area:

                        if (ti.Width <= _Node.Bounds.Width && ti.Height <= _Node.Bounds.Height)
                        {
                            float textureArea = ti.Width * ti.Height;
                            float coverage = textureArea / nodeArea;
                            if (coverage > maxCriteria)
                            {
                                maxCriteria = coverage;
                                bestFit = ti;
                            }
                        }
                        break;
                }
            }
            return bestFit;
        }

        
        private List<TextureInfo> LayoutAtlas(List<TextureInfo> _Textures, Atlas _Atlas)
        {
            List<Node> freeList = new List<Node>();
            List<TextureInfo> textures = new List<TextureInfo>();

            _Atlas.Nodes = new List<Node>();

            textures = _Textures.ToList();

            Node root = new Node();
            root.Bounds.Size = new Size(_Atlas.Width, _Atlas.Height);
            root.SplitType = SplitType.Horizontal;

            freeList.Add(root);

            while (freeList.Count > 0 && textures.Count > 0)
            {
                Node node = freeList[0];
                freeList.RemoveAt(0);

                TextureInfo bestFit = FindBestFitForNode(node, textures);
                if (bestFit != null)
                {
                    if (node.SplitType == SplitType.Horizontal)
                    {
                        HorizontalSplit(node, bestFit.Width, bestFit.Height, freeList);
                    }
                    else
                    {
                        VerticalSplit(node, bestFit.Width, bestFit.Height, freeList);
                    }

                    node.Texture = bestFit;
                    node.Bounds.Width = bestFit.Width;
                    node.Bounds.Height = bestFit.Height;

                    textures.Remove(bestFit);
                }

                _Atlas.Nodes.Add(node);
            }

            return textures;
        }


        /**
         * Create a dedicated image for fixed palette colors.
         * This image containns all RGBA 3331 color values, i.e. all values
         * for a 3331 bit per component RGBA palette, with a being 8 or f
         * Every value exists on two horizontal and two vertical values.
         * This gives a total of 1k * 4 = 4k pixels, arranged in a 64*64 texture.
         *
         * The texture is first treated as 2x2 pixels.
         * Then, the pixels are filled according to the numeric ABGB numerical
         * values, like ((y>>1)<<8) | (x>>1)).
         */
        private SKImage CreateRGBA16Image()
        {
            
            var info = new SKImageInfo(64, 64, SKColorType.Bgra8888, SKAlphaType.Unpremul);
            SKImage image;
            using (var skiaSurface = SKSurface.Create(info))
            {

                var paint = new SKPaint
                {
                    Color = 0x00000000,
                    Style = SKPaintStyle.Fill
                };
                for (uint y = 0; y < 32; y++)
                {
                    for (uint x = 0; x < 32; x++)
                    {
                        uint a = ((y & 0x10u) != 0) ? 0xffu : 0x88u;
                        uint r = (y & 0x0e) << 4;
                        r |= r >> 4;
                        uint g = ((y & 0x1)<<7) | ((x&0x18u)<<2);
                        g |= g >> 4;
                        uint b = (x & 0x07) << 5;
                        b |= b >> 4;
                        paint.Color = (a << 24) | (r << 16) | (g << 8) | (b);
                        skiaSurface.Canvas.DrawRect(2 * x, 2 * y, 2, 2, paint);
                    }
                }

                paint.Dispose();
                image = skiaSurface.Snapshot();
            }

            return image;
        }

        private SKImage LoadImage(string path)
        {
            if (path == "rgba") return CreateRGBA16Image();
            else return SKImage.FromEncodedData(path);
        }



        private SKSurface CreateAtlasImage(Atlas _Atlas)
        {
            var info = new SKImageInfo(_Atlas.Width, _Atlas.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
            var skiaSurface = SKSurface.Create(info);

            {
                var paint = new SKPaint
                {
                    Color = 0x000000,
                    Style = SKPaintStyle.Fill
                };
                skiaSurface.Canvas.DrawRect(0, 0, _Atlas.Width - 1, _Atlas.Height - 1, paint);
                paint.Dispose();
            }

            foreach (Node n in _Atlas.Nodes)
            {
                if (n.Texture != null)
                {
                    using (var image = LoadImage(n.Texture.FullPath))
                    using (var bm = SKBitmap.FromImage(image))
                    {
                        skiaSurface.Canvas.DrawBitmap(bm, new SKPoint(n.Bounds.X, n.Bounds.Y));
                    }
                }
                else
                {
                    var paint = new SKPaint
                    {
                        Color = 0xff00ff00,
                        Style = SKPaintStyle.Fill
                    };
                    skiaSurface.Canvas.DrawRect(n.Bounds.X, n.Bounds.Y, n.Bounds.Width - 1, n.Bounds.Height - 1, paint);
                    paint.Dispose();
                }
            }

            return skiaSurface;
        }

    }
}