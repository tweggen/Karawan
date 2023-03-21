
namespace Splash.Silk
{
    public class SkTextureEntry : Splash.ATextureEntry
    {
        //public Raylib_CsLo.Texture RlTexture;

        public override bool IsUploaded()
        {
            //return RlTexture.width != 0;
            return false;
        }

        public SkTextureEntry(in engine.joyce.Texture jTexture)
            : base(jTexture)
        {
        }
    }
}