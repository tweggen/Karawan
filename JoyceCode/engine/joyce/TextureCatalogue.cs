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

    public void AddAtlasEntry(string textureTag, string atlasTag, in Vector2 uvOffset, in Vector2 uvScale)
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
                UVScale = uvScale
            };

            atlas.AtlasEntries.Add(tae);
            _dictTextures[textureTag] = tae;
        }
    }
    
    /**
     * Look up a texture for the given texture tag.
     */
    public Texture FindTexture(string tag)
    {
        Texture jTexture;
        jTexture = new Texture(tag);
        return jTexture;
    }
}