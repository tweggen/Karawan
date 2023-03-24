
namespace Splash.Silk
{
    public class SkTextureEntry : Splash.ATextureEntry
    {
        public SkTexture SkTexture;

        public override bool IsUploaded()
        {
            return SkTexture != null;
        }

        public SkTextureEntry(in engine.joyce.Texture jTexture)
            : base(jTexture)
        {
        }
    }
}