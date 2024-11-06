
using engine.draw;

namespace Splash.Silk
{
    public class SkTextureEntry : Splash.ATextureEntry
    {
        public SkTexture? SkTexture = null;

        public override ResourceState State
        {
            get
            {
                if (SkTexture != null)
                {
                    return SkTexture.ResourceState;
                }
                else
                {
                    return ResourceState.Created;
                }
            }
        }

        
        public SkTextureEntry(in engine.joyce.Texture jTexture)
            : base(jTexture)
        {
        }
    }
}