using System.Collections.Generic;
using System.IO;
using System.Numerics;
using static engine.Logger;

namespace engine.joyce;


public class TextureAtlas
{
    public string AtlasTag;
    public List<TextureAtlasEntry> AtlasEntries = new();
}

public class TextureAtlasEntry
{
    public string TextureTag;
    public readonly TextureAtlas TextureAtlas;
    public Vector2 UVOffset;
    public Vector2 UVScale;
    public int Width, Height;

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

    public void AddAtlasEntry(string textureTag, string atlasTag, in Vector2 uvOffset, in Vector2 uvScale, int Width, int Height)
    {
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
                atlas = new TextureAtlas() { AtlasTag = atlasTag};
                _dictAtlasses[atlasTag] = atlas;
            }

            TextureAtlasEntry tae = new(atlas)
            {
                TextureTag = textureTag,
                UVOffset = uvOffset,
                UVScale = uvScale,
                Width = Width,
                Height = Height
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
            Height = 2
        };

        return jTexture;
    }
    
    
    /**
     * Look up a texture for the given texture tag.
     */
    public Texture FindTexture(string tag)
    {
        TextureAtlasEntry tae;
        lock (_lo)
        {
            if (!_dictTextures.TryGetValue(tag, out tae))
            {
                tae = null;
            }
        }

        if (null == tae)
        {
            return new Texture(Texture.BLACK);
        }
        
        Texture jTexture;
        jTexture = new Texture(tae.TextureAtlas.AtlasTag)
        {
            UVOffset = tae.UVOffset,
            UVScale = tae.UVScale,
            Width = tae.Width,
            Height = tae.Height
        };
        
        return jTexture;
    }
}