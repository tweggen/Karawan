
namespace Splash.Silk
{
    public class SkTextureEntry : Splash.ATextureEntry
    {
        public SkTexture SkTexture;

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