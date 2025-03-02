using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using static engine.Logger;

namespace engine.joyce;


public class TextureAtlas
{
    public string AtlasTag;
    public bool HasMipmap;
    public List<TextureAtlasEntry> AtlasEntries = new();
}

public class TextureAtlasEntry
{
    public string TextureTag;
    public readonly TextureAtlas TextureAtlas;
    public Vector2 UVOffset;
    public Vector2 UVScale;
    public int Width, Height;
    public bool HasMipmap;

    public TextureAtlasEntry(TextureAtlas atlas)
    {
        TextureAtlas = atlas;
    }
}


/**
 * Look up a certain texture by its tag in the set of available textures.
 * Returns the actual texture object for use in materials.
 */
public class TextureCatalogue
{
    private object _lo = new();
    private SortedDictionary<string, TextureAtlasEntry> _dictTextures = new();
    private SortedDictionary<string, TextureAtlas> _dictAtlasses = new();

    public void AddAtlasEntry(string textureTag, string atlasTag, in Vector2 uvOffset, in Vector2 uvScale, int Width, int Height, bool hasMipmap)
    {
        // Trace($"About to add texture {textureTag} in atlas {atlasTag}.");
        lock (_lo)
        {
            if (_dictTextures.TryGetValue(textureTag, out _))
            {
                ErrorThrow<InvalidDataException>($"Encountered duplicate texture tag {textureTag}");
                return;
            }

            TextureAtlas atlas;
            if (!_dictAtlasses.TryGetValue(atlasTag, out atlas))
            {
                atlas = new TextureAtlas() { AtlasTag = atlasTag, HasMipmap = hasMipmap };
                _dictAtlasses[atlasTag] = atlas;
            }

            TextureAtlasEntry tae = new(atlas)
            {
                TextureTag = textureTag,
                UVOffset = uvOffset,
                UVScale = uvScale,
                Width = Width,
                Height = Height,
                HasMipmap = hasMipmap
            };

            atlas.AtlasEntries.Add(tae);
            _dictTextures[textureTag] = tae;
        }
    }


    public Texture FindColorTexture(uint color)
    {
        uint r = ((color      ) & 0xff);
        uint g = ((color >>  8) & 0xff);
        uint b = ((color >> 16) & 0xff);
        uint a = (((color >> 24) & 0xff) > 0xcc) ? 0xffu : 0x00u;

        uint y = (a >> 2) & 0x20 | (b >> 3) & 0x1c | (g >> 6) & 0x02;
        uint x = (g >> 1) & 0x30 | (r >> 4) & 0x0e;
        
        /*
         * We return uv between the middle of the (x,y) pixel and the middle of
         * the (x+1, y+1) pixel.
         *
         * We know the texture resolution is 64 pixel.
         */
        Vector2 uvColorOffset = (new Vector2(x, y) + 0.5f * Vector2.One) / 64f;
        Vector2 uvColorScale = 1f / 64f * Vector2.One;
        
        /*
         * Now we need to resolve the position of the RGBA texture and combine
         * it with the uv value we computed for the color. 
         */
        
        TextureAtlasEntry tae;
        lock (_lo)
        {
            if (!_dictTextures.TryGetValue("rgba", out tae))
            {
                ErrorThrow<InvalidDataException>($"No rgba texture found although it was requested.");
                return null;
            }
        }

        Texture jTexture;
        jTexture = new Texture(tae.TextureAtlas.AtlasTag)
        {
            UVOffset = tae.UVOffset + (uvColorOffset*tae.UVScale),
            UVScale = tae.UVScale * uvColorScale,
            Width = 2,
            Height = 2,
            // TXWTODO: We need to set the filtering to none, i.e. framebuffer
            HasMipmap = true
        };

        return jTexture;
    }


    public bool TryGetTexture(string tag, Action<Texture>? action, out Texture jTexture)
    {
        TextureAtlasEntry tae;
        lock (_lo)
        {
            if (!_dictTextures.TryGetValue(tag, out tae))
            {
                jTexture = null;
                return false;
            }
        }

        jTexture = new Texture(tae.TextureAtlas.AtlasTag)
        {
            UVOffset = tae.UVOffset,
            UVScale = tae.UVScale,
            Width = tae.Width,
            Height = tae.Height,
            HasMipmap = tae.HasMipmap
        };
        if (null != action)
        {
            action(jTexture);
        }
        
        return true;
    }
    
    
    /**
     * Look up a texture for the given texture tag.
     */
    public Texture FindTexture(string tag, Action<Texture>? action = null)
    {
        if (!TryGetTexture(tag, action, out var jTexture))
        {
            return new Texture(Texture.BLACK);
        }

        return jTexture;
    }
}