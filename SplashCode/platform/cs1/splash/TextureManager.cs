using Raylib_CsLo;
using System;
using System.Collections.Generic;

namespace Karawan.platform.cs1.splash
{
    public class TextureManager
    {
        private TextureGenerator _textureGenerator;
        private Dictionary<engine.joyce.Texture, splash.RlTextureEntry> _dictTextures;

        public unsafe splash.RlTextureEntry FindRlTexture(in engine.joyce.Texture jTexture)        
        {
            splash.RlTextureEntry rlTextureEntry;
            if (_dictTextures.TryGetValue(jTexture, out rlTextureEntry))
            {
            }
            else
            {
                _textureGenerator.CreateRaylibTexture(jTexture, out rlTextureEntry);
                _dictTextures.Add(jTexture, rlTextureEntry);
            }
            return rlTextureEntry;
        }

        private void _purgeLoadTexture(in engine.joyce.Texture jTexture, bool doDownload)
        {
            splash.RlTextureEntry rlTextureEntry;
            if (_dictTextures.TryGetValue(jTexture, out rlTextureEntry))
            {
                if (doDownload)
                {
                    Image image = Raylib.LoadImageFromTexture(rlTextureEntry.RlTexture);
                }
                Raylib.UnloadTexture(rlTextureEntry.RlTexture);
                _dictTextures.Remove(jTexture);
                rlTextureEntry.RlTexture = new();
            }
            else
            {
                Console.WriteLine("problem");
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
