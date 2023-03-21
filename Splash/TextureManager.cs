using System;
using System.Collections.Generic;

namespace Splash
{
    public class TextureManager
    {
        private readonly object _lock = new ();

        private readonly IThreeD _threeD;
        private Dictionary<string, ATextureEntry> _dictTextures;


        private string _textureKey(in engine.joyce.Texture jTexture)
        {
            if( jTexture.Source == null)
            {
                return "(null)";
            }
            else
            {
                return jTexture.Source;
            }
        } 

        public ATextureEntry FindATexture(in engine.joyce.Texture jTexture)        
        {
            ATextureEntry aTextureEntry;
            string textureKey = _textureKey(jTexture);
            lock (_lock)
            {
                if (_dictTextures.TryGetValue(textureKey, out aTextureEntry))
                {
                    return aTextureEntry;
                } else
                {
                    aTextureEntry = _threeD.CreateTextureEntry(jTexture);
                    _dictTextures.Add(textureKey, aTextureEntry);
                    // TXWTODO: Should be async and not with proxy held.
                    _threeD.FillTextureEntry(aTextureEntry);
                }
            }
            return aTextureEntry;
        }
        
        public TextureManager(in IThreeD threeD)
        {
            _dictTextures = new();
            _threeD = threeD;
        }
    }
}
