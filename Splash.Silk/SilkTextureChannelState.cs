using System.ComponentModel;
using engine;
using engine.joyce;
using Silk.NET.OpenGL;
using Texture = engine.joyce.Texture;
using static engine.Logger;

namespace Splash.Silk;

public class SilkTextureChannelState
{
    private GL _gl;
    private SkTexture _currentSkTexture;
    private TextureUnit _textureUnit;

    private SkTexture _transparentSkTexture;
    
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
            else
            {
                /*
                 * We need to set something if skTexture is null, that is
                 * rendered instead of the skTextureObject.
                 */
                _transparentSkTexture.ActiveAndBind(_textureUnit);
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
        byte[] _arrBlack = { 0, 0, 0, 0 };
        
        _gl = gl;
        _textureUnit = textureUnit;
        _transparentSkTexture =
            new SkTexture(gl, new Texture(engine.joyce.Texture.BLACK), Texture.FilteringModes.Pixels);
        _transparentSkTexture.SetFrom(0, _arrBlack, 1, 1);
        
        /*
         * Default to transparent nothing on this channel.
         */
        _transparentSkTexture.ActiveAndBind(textureUnit);
    }
}