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
    
    
    private void _useTexture(SkTexture? skTexture)
    {
        if (_currentSkTexture != skTexture)
        {
            if (null != _currentSkTexture)
            {
                _currentSkTexture.ActiveAndUnbind(_textureUnit);
                _currentSkTexture = null;
            }
            if (skTexture != null)
            {
                _currentSkTexture = skTexture;
                _currentSkTexture.ActiveAndBind(_textureUnit);
            }
        }
    }
    
    
    public void UseTextureEntry(SkTextureEntry? skTextureEntry)
    {
        SkTexture? skTexture = null;
        
        if (skTextureEntry != null && skTextureEntry.State >= AResourceEntry.ResourceState.Using)
        {
            skTexture = skTextureEntry.SkTexture;
        }
        
        _useTexture(skTexture);
    }


    public SilkTextureChannelState(GL gl, TextureUnit textureUnit)
    {
        _gl = gl;
        _textureUnit = textureUnit;
    }
}