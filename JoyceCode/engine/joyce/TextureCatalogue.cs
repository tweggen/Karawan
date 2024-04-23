namespace engine.joyce;


/**
 * Look up a certain texture by its tag in the set of available textures.
 * Returns the actual texture object for use in materials.
 */
public class TextureCatalogue
{
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