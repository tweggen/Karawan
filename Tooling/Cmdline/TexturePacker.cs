using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
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
        public float UScaleCell { get; set; }
        public float VScaleCell { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int CellWidth { get; set; }
        public int CellHeight { get; set; }
        
        public int PixelX { get; set; }
        public int PixelY { get; set; }
    }

    public class JsonAtlasDesc
    {
        public string Uri { get; set; }
        public string Tag { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public bool IsMipmap { get; set; }
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


        public int ImageWidth;
        public int ImageHeight;
        
        /// <summary>
        ///  How important is this texture? Textures with prio 1 are laid out first.
        /// </summary>
        public int Priority = 0;
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
        public readonly int Width;

        /// <summary>
        /// Height in Pixel
        /// </summary>
        public readonly int Height;

        /// <summary>
        /// List of the nodes in the Atlas. This will represent all the textures that are packed into it and all the remaining free space
        /// </summary>
        public readonly List<Node> Nodes;

        public readonly List<Node> FreeList;

        
        public Atlas(int width, int height)
        {
            Nodes = new List<Node>();
            FreeList = new List<Node>();
            Width = width;
            Height = height;
            Node root = new Node();
            root.Bounds.Size = new Size(Width, Height);
            root.SplitType = SplitType.Horizontal;

            FreeList.Add(root);
        }


        public Atlas(Atlas o)
        {
            Width = o.Width;
            Height = o.Height;
            Nodes = new List<Node>(o.Nodes);
            FreeList = new List<Node>(o.FreeList);
        }
    }

    /// <summary>
    /// Objects that performs the packing task. Takes a list of textures as input and generates a set of atlas textures/definition pairs
    /// </summary>
    public class Packer
    {
        /// <summary>
        /// List of all the textures that need to be packed
        /// </summary>
        public List<TextureInfo> SourceTextures;

        public string DestinationTexture;

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
        public int Padding { get; set; } = 0;

        /// <summary>
        /// Size of the atlas in pixels. Represents one axis, as atlases are square
        /// </summary>
        public int AtlasSize { get; set; } = 1024;

        /// <summary>
        /// Which heuristic to use when doing the fit
        /// </summary>
        public BestFitHeuristic FitHeuristic;

        /// <summary>
        /// List of all the output atlases
        /// </summary>
        public List<Atlas> Atlasses;

        public string CurrentPath;

        public List<Resource> StandaloneTextures = new List<Resource>();

        
        private void _horizontalSplit(Node _ToSplit, int _Width, int _Height, List<Node> _List)
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

        
        private void _verticalSplit(Node _ToSplit, int _Width, int _Height, List<Node> _List)
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
         * 
         * x == gg aaa0
         * y == ab bbg0
         * 
         */
        private SKImage _createRGBA16Image()
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
                        uint b = (y & 0x0e) << 4;
                        b |= b >> 4;
                        uint g = ((y & 0x1)<<7) | ((x&0x18u)<<2);
                        g |= g >> 4;
                        uint r = (x & 0x07) << 5;
                        r |= r >> 4;
                        paint.Color = (a << 24) | (b << 16) | (g << 8) | r;
                        skiaSurface.Canvas.DrawRect(2 * x, 2 * y, 2, 2, paint);
                    }
                }

                paint.Dispose();
                image = skiaSurface.Snapshot();
            }

            return image;
        }
        

        private SKImage _loadImage(string path)
        {
            if (path == "rgba") return _createRGBA16Image();
            else return SKImage.FromEncodedData(path);
        }
        
        
        private SKBitmap _halfBitmap(in ReadOnlySpan<byte> spanPixels, int fullWidth, int fullHeight, int fullRowBytes)
        {
            var pf = spanPixels;
            int yFullOffset1 = 0;
            int yFullOffset2 = fullRowBytes;

            int halfWidth = fullHeight / 2;
            int halfHeight = fullHeight / 2;

            int halfRowBytes = halfWidth * 4; 
            int yHalfOffset = 0;

            const int pixelBytes = 4;

            byte[] p = new byte[halfRowBytes * halfHeight];            
            
            for (int y = 0; y < halfHeight; y++)
            {
                int x2 = 0;
                int xDest = 0;
                for (int x = 0; x < halfWidth; x++)
                {
                    p[yHalfOffset + xDest] = 
                        (byte)(
                            (pf[yFullOffset1 + x2] 
                            + pf[yFullOffset1 + x2 + pixelBytes]
                            + pf[yFullOffset2 + x2] 
                            + pf[yFullOffset2 + x2 + pixelBytes])/4);
                    ++x2;
                    ++xDest;
                    p[yHalfOffset + xDest] = 
                        (byte)(
                            (pf[yFullOffset1 + x2] 
                             + pf[yFullOffset1 + x2 + pixelBytes]
                             + pf[yFullOffset2 + x2] 
                             + pf[yFullOffset2 + x2 + pixelBytes])/4);
                    ++x2;
                    ++xDest;
                    p[yHalfOffset + xDest] = 
                        (byte)(
                            (pf[yFullOffset1 + x2] 
                             + pf[yFullOffset1 + x2 + pixelBytes]
                             + pf[yFullOffset2 + x2] 
                             + pf[yFullOffset2 + x2 + pixelBytes])/4);
                    ++x2;
                    ++xDest;
                    p[yHalfOffset + xDest] = 
                        (byte)(
                            (pf[yFullOffset1 + x2] 
                             + pf[yFullOffset1 + x2 + pixelBytes]
                             + pf[yFullOffset2 + x2] 
                             + pf[yFullOffset2 + x2 + pixelBytes])/4);
                    ++x2;
                    ++xDest;
                    
                    x2 += pixelBytes;
                }

                yFullOffset1 += 2 * fullRowBytes;
                yFullOffset2 += 2 * fullRowBytes;
                yHalfOffset += halfRowBytes;
            }

            SKBitmap bmHalf = new SKBitmap();
            GCHandle gcHandle = GCHandle.Alloc(p, GCHandleType.Pinned);
            SKImageInfo ii = new SKImageInfo(halfWidth, halfHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
            bmHalf.InstallPixels(ii, gcHandle.AddrOfPinnedObject(), ii.RowBytes, delegate { gcHandle.Free(); });
            return bmHalf;
        }

        private SKSurface _createMipmapSurface(SKSurface sksAtlas, Atlas atlas)
        {
            var info = new SKImageInfo(atlas.Width*2, atlas.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul);

            /*
             * The number of mipmap levels.
             */
            const int nMipmaps = 8;
            
            /*
             * The original image
             */
            SKImage skiAtlas = sksAtlas.Snapshot();

            /*
             * This is the overall destination for all mipmap levels including the original.
             * Clear it out to leave room for compression.
             */
            var sksMipmap = SKSurface.Create(info);
            using (var paint = new SKPaint
                   {
                       Color = 0x00000000,
                       Style = SKPaintStyle.Fill
                   })
            {
                sksMipmap.Canvas.DrawRect(0, 0, atlas.Width * 2 - 1, atlas.Height - 1, paint);
            }
            
            /*
             * Creaete the root bitmap.
             */
            SKBitmap skbSource = SKBitmap.FromImage(skiAtlas);
            
            /*
             * This is the offset of the current mipmap level.
             */
            int xOffset = 0;
            for (int level = 0; level < nMipmaps; level++)
            {
                sksMipmap.Canvas.DrawBitmap(skbSource, xOffset, 0);
                if (skbSource.Width <= 1)
                {
                    break;
                }
                SKBitmap skbHalf = _halfBitmap(skbSource.GetPixelSpan(), skbSource.Width, skbSource.Height, skbSource.RowBytes);
                xOffset += skbSource.Width;
                skbSource.Dispose();
                skbSource = skbHalf;
            }
            skbSource.Dispose();
            
            return sksMipmap;
        }


        private SKSurface _createAtlasImage(Atlas _Atlas)
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
                    using (var image = _loadImage(n.Texture.FullPath))
                    {
                        using (SKBitmap bm = SKBitmap.FromImage(image))
                        {
                            skiaSurface.Canvas.DrawBitmap(bm, new SKPoint(n.Bounds.X, n.Bounds.Y));
                        }
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


        private TextureInfo _findBestFitForNode(Node _Node, List<TextureInfo> _Textures)
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
        

        private List<TextureInfo> _layoutAtlas(List<TextureInfo> textures0, Atlas orgAtlas, out Atlas atlas)
        {
            /*
             * We operate on a copy.
             */
            atlas = new Atlas(orgAtlas);
            
            List<TextureInfo> textures = new List<TextureInfo>(textures0);
            
            Node root = new Node();
            root.Bounds.Size = new Size(atlas.Width, atlas.Height);
            root.SplitType = SplitType.Horizontal;

            while (atlas.FreeList.Count > 0 && textures.Count > 0)
            {
                List<Node> sortedFreeList = atlas.FreeList.ToList();
                
                sortedFreeList.Sort((n1, n2) =>
                {
                    return n1.Bounds.Width * n1.Bounds.Height - n2.Bounds.Width * n2.Bounds.Height;
                });
                Node node = sortedFreeList[0];
                atlas.FreeList.Remove(node);

                TextureInfo bestFit = _findBestFitForNode(node, textures);
                if (bestFit != null)
                {
                    if (node.SplitType == SplitType.Horizontal)
                    {
                        _horizontalSplit(node, bestFit.Width, bestFit.Height, atlas.FreeList);
                    }
                    else
                    {
                        _verticalSplit(node, bestFit.Width, bestFit.Height, atlas.FreeList);
                    }

                    node.Texture = bestFit;
                    node.Bounds.Width = bestFit.Width;
                    node.Bounds.Height = bestFit.Height;

                    textures.Remove(bestFit);
                }

                atlas.Nodes.Add(node);
            }

            return textures;
        }


        public void SaveAtlasses()
        {
            int atlasCount = 0;
            
            string prefix = DestinationTexture;
            if (Path.HasExtension(prefix))
            {
                prefix = prefix.Replace(Path.GetExtension(prefix), ""); 
            }

            JsonAtlassesDesc jAtlasses = new JsonAtlassesDesc()
            {
                Atlasses = new Dictionary<string, JsonAtlasDesc>()
            };
            
            foreach (Atlas atlas in Atlasses)
            {
                string atlasName = String.Format(prefix + "{0:000}" + ".png", atlasCount);
                string atlasTag = System.IO.Path.GetFileName(atlasName);

                /*
                 * Now create the atlas from the atlas surface.
                 */
                using(SKSurface skSurface = _createAtlasImage(atlas))

                /*
                 * Create the mipmaps from the atlas
                 */
                using(SKSurface skMipmapSurface = _createMipmapSurface(skSurface, atlas))
                using (var image = skMipmapSurface.Snapshot())
                using (var data = image.Encode(SKEncodedImageFormat.Png, 80))
                using (var stream = File.OpenWrite(Path.Combine(CurrentPath,atlasName)))
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
                    IsMipmap = true,
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
                            PixelX = n.Bounds.X,
                            PixelY = n.Bounds.Y,
                            U = ((float)n.Bounds.X / atlas.Width),
                            V = ((float)n.Bounds.Y / atlas.Height),
                            UScaleCell = ((float)n.Bounds.Width / atlas.Width),
                            VScaleCell = ((float)n.Bounds.Height / atlas.Height),
                            UScale = ((float)n.Texture.ImageWidth / atlas.Width),
                            VScale = ((float)n.Texture.ImageHeight / atlas.Height),
                            CellWidth = (int)n.Bounds.Width,
                            CellHeight = (int)n.Bounds.Height,
                            Width = n.Texture.ImageWidth,
                            Height = n.Texture.ImageHeight
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
                    Textures = new Dictionary<string, JsonTextureDesc>(),
                    IsMipmap = false
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

            string descFile = DestinationTexture;
            StreamWriter tw = new StreamWriter(Path.Combine(CurrentPath,descFile));
            {
                var options = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                string jsonString = JsonSerializer.Serialize(jAtlasses, options);
                tw.Write(jsonString);
            }
            tw.Close();

            tw = new StreamWriter(Path.Combine(CurrentPath,prefix + ".log"));
            tw.WriteLine("--- LOG -------------------------------------------");
            tw.WriteLine(Log.ToString());
            tw.WriteLine("--- ERROR -----------------------------------------");
            tw.WriteLine(Error.ToString());
            tw.Close();
        }

        private Atlas _currentAtlas = null;

        /**
         * Process the incoming textures, creating as many atlasses as required on the go.
         */
        public void ProcessTextures()
        {
            /*
             * Fetch current state.
             */
            var atlas = _currentAtlas;
            _currentAtlas = null;
            
            while (SourceTextures.Count > 0)
            {
                if (null == atlas)
                {
                    atlas = new Atlas(AtlasSize, AtlasSize);
                }

                /*
                 * Collect as much as we can into this atlas.
                 */
                List<TextureInfo> leftovers = _layoutAtlas(SourceTextures, atlas, out var newAtlas);

                /*
                 * We unconditionally use the newly created atlas.
                 */
                atlas = newAtlas;
                
                if (leftovers.Count > 0)
                {
                    /*
                     * We do not minimize the last texture, only after the last packing
                     */
                    Atlasses.Add(atlas);
                    atlas = null;
                }

                SourceTextures = leftovers;
            }

            /*
             * Write back current state
             */
            _currentAtlas = atlas;
        }


        public void FinishTextures()
        {
            /*
             * Try to minimize the last atlas.
             */
            var atlas = _currentAtlas;
            _currentAtlas = null;
            if (atlas != null)
            {
                Atlasses.Add(atlas);
            }
        }
        

        public void Prepare()
        {
            Atlasses = new List<Atlas>();
            _currentAtlas = null; 
        }


        public void AddTexture(Resource resourceTexture, int prio)
        {
            AddTexture("", resourceTexture, prio);
        }


        public void AddTexture(string CurrentPath, Resource resourceTexture, int prio)
        {
            Func<int,int> nextPOT = (int n) =>
            {
                int r = n - 1;
                r |= r >> 1;
                r |= r >> 2;
                r |= r >> 4;
                r |= r >> 8;
                r |= r >> 16;
                return r;
            };
                
            if (resourceTexture.Uri == "rgba")
            {
                TextureInfo ti = new TextureInfo();

                ti.Resource = resourceTexture;
                ti.FullPath = "rgba";
                ti.Width = 64;
                ti.Height = 64;
                ti.ImageWidth = 64;
                ti.ImageHeight = 64;
                ti.Priority = prio;

                SourceTextures.Add(ti);

                Log.WriteLine($"Added \"{resourceTexture.Tag}\" (found at \"{ti.FullPath}\")");
            }
            else
            {
                FileInfo fi = new FileInfo(Path.Combine(CurrentPath,resourceTexture.Uri));
                if (!fi.Exists)
                {
                    Log.WriteLine($"Error: File \"{fi.FullName}\" does not exist.");
                    return;
                }
                using (var image = SKImage.FromEncodedData(fi.FullName))
                {
                    int cellWidth = nextPOT(image.Width)+1;
                    int cellHeight = nextPOT(image.Height)+1;

                    if (image.Width <= AtlasSize && image.Height <= AtlasSize)
                    {
                        TextureInfo ti = new TextureInfo();

                        ti.Resource = resourceTexture;
                        ti.FullPath = fi.FullName;
                        ti.Width = cellWidth;
                        ti.Height = cellHeight;
                        ti.ImageWidth = image.Width;
                        ti.ImageHeight = image.Height;
                        
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

        
        public Packer()
        {
            SourceTextures = new List<TextureInfo>();
            Log = new StringWriter();
            Error = new StringWriter();
        }
    }
}