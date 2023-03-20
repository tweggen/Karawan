using Raylib_CsLo;
using System;
using System.Collections.Generic;

namespace Karawan.platform.cs1.splash
{
    public class TextureManager
    {
        private object _lock = new ();
        private TextureGenerator _textureGenerator;
        private Dictionary<string, splash.RlTextureEntry> _dictTextures;


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

        public unsafe splash.RlTextureEntry FindRlTexture(in engine.joyce.Texture jTexture)        
        {
            splash.RlTextureEntry rlTextureEntry;
            string textureKey = _textureKey(jTexture);
            lock (_lock)
            {
                if (_dictTextures.TryGetValue(textureKey, out rlTextureEntry))
                {
                    return rlTextureEntry;
                } else
                {
                    // TXWTODO: Should be async and not with proxy held.
                    _textureGenerator.CreateRaylibTexture(jTexture, out rlTextureEntry);
                    _dictTextures.Add(textureKey, rlTextureEntry);
                }
            }
            return rlTextureEntry;
        }

        private void _purgeLoadTexture(in engine.joyce.Texture jTexture, bool doDownload)
        {
            splash.RlTextureEntry rlTextureEntry;
            string textureKey = _textureKey(jTexture);
            lock (_lock)
            {
                if (_dictTextures.TryGetValue(textureKey, out rlTextureEntry))
                {
                    if (doDownload)
                    {
                        Image image = Raylib.LoadImageFromTexture(rlTextureEntry.RlTexture);
                    }
                    Raylib.UnloadTexture(rlTextureEntry.RlTexture);
                    _dictTextures.Remove(textureKey);
                    rlTextureEntry.RlTexture = new();
                }
                else
                {
                    Console.WriteLine("problem");
                }
            }
        }


        public void LoadBackTexture(in engine.joyce.Texture jTexture)
        {
            _purgeLoadTexture(jTexture, true);
        }


        public void PurgeTexture(in engine.joyce.Texture jTexture)
        {
            _purgeLoadTexture(jTexture, false);
        }

        public TextureManager(in TextureGenerator textureGenerator)
        {
            _dictTextures = new();
            _textureGenerator = textureGenerator;
        }
    }
}
