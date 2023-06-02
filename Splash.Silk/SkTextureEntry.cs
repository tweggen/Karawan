
using engine.draw;

namespace Splash.Silk
{
    public class SkTextureEntry : Splash.ATextureEntry
    {
        public SkTexture? SkTexture = null;

        public override bool IsUploaded()
        {
            return SkTexture != null;
        }

        public override bool IsOutdated()
        {
            if (SkTexture == null)
            {
                return false;
            }

            IFramebuffer framebuffer = JTexture.Framebuffer;
            if (framebuffer == null)
            {
                return false;
            }

            if (framebuffer.Generation != SkTexture.Generation)
            {
                return true;
            }

            return false;
        }

        public SkTextureEntry(in engine.joyce.Texture jTexture)
            : base(jTexture)
        {
        }
    }
}