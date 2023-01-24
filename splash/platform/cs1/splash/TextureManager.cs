using Raylib_CsLo;
using System.Collections.Generic;

namespace Karawan.platform.cs1.splash
{
    public class TextureManager
    {
        TextureGenerator _textureGenerator;


        private Dictionary<engine.joyce.Texture, splash.RlTextureEntry> _dictTextures;

        public unsafe splash.RlTextureEntry FindRlTexture(engine.joyce.Texture jTexture)
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

        public void LoadBackTexture( RlTextureEntry rlTextureEntry )
        {
            Image image = Raylib.LoadImageFromTexture(rlTextureEntry.RlTexture);
            Raylib.UnloadTexture(rlTextureEntry.RlTexture);
            rlTextureEntry.RlTexture = new();
            Raylib.UnloadImage(image);
        }

        public TextureManager()
        {
            _dictTextures = new();
            _textureGenerator = new();
        }
    }
}
