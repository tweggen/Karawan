
using Raylib_CsLo;

namespace Splash.Raylib
{
    public class RlTextureEntry : Splash.ATextureEntry
    {
        public Raylib_CsLo.Texture RlTexture;

        public override bool IsUploaded()
        {
            return RlTexture.width != 0;
        }
    }
}
