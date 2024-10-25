using Silk.NET.OpenGL;

namespace Splash.Silk;

public class SilkTextureChannelState
{
    private GL _gl;
    private SkTexture _currentSkTexture;
    private TextureUnit _textureUnit;


    public void UnbindTexture()
    {
        if (null != _currentSkTexture)
        {
            _currentSkTexture.ActiveAndUnbind(_textureUnit);
            _currentSkTexture = null;
        }
    }
    
    
    public void UseTexture(SkTexture skTexture)
    {
        if (_currentSkTexture != skTexture)
        {
            UnbindTexture();
            _currentSkTexture = skTexture;
            _currentSkTexture.ActiveAndBind(_textureUnit);
        }
    }
    
    
    public void UseTextureEntry(SkTextureEntry? skTextureEntry)
    {
        SkTexture? skTexture = null;
        
        if (skTextureEntry != null && skTextureEntry.IsUploaded())
        {
            skTexture = skTextureEntry.SkTexture;
        }
        

        if (_currentSkTexture == skTexture)
        {
            return;
        }
        if (skTexture != null)
        {
            UseTexture(skTexture);
        }
        else
        {
            UnbindTexture();
        }
    }


    public SilkTextureChannelState(GL gl, TextureUnit textureUnit)
    {
        _gl = gl;
        _textureUnit = textureUnit;
    }
}