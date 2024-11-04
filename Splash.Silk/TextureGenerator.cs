
using System.Diagnostics;
using engine;
using engine.draw;
using static engine.Logger;

namespace Splash.Silk
{
    public class TextureGenerator
    {
        private readonly engine.Engine _engine;
        private readonly SilkThreeD _silkThreeD;
        private static Byte[] _arrBlack = { 0, 0, 0, 0 };

        
        /**
         * Create a GPU texture object for the TextureEntry if it does
         * not yet exist.
         *
         * Fill it with data from the texture source, so either a file
         * or a framebuffer.
         */
        public void LoadUploadTextureEntry(in SkTextureEntry skTextureEntry)
        {
            engine.joyce.Texture jTexture = skTextureEntry.JTexture;
                
            if (jTexture.Source != null)
            {
                string texturePath = jTexture.Source;
                if (texturePath.StartsWith("framebuffer:"))
                {
                    /*
                     * Do not fill anything.
                     */
                } else if (texturePath == engine.joyce.Texture.BLACK)
                {
                    if (null == skTextureEntry.SkTexture)
                    {
                        skTextureEntry.SkTexture = new SkTexture(_silkThreeD.GetGL(), jTexture, jTexture.FilteringMode);
                    }
                    skTextureEntry.SkTexture.SetFrom(0, _arrBlack, 1, 1);
                }
                else
                {
                    if (null == skTextureEntry.SkTexture)
                    {
                        skTextureEntry.SkTexture = new SkTexture(_silkThreeD.GetGL(), jTexture, jTexture.FilteringMode);
                    }
                    skTextureEntry.SkTexture.SetFrom(jTexture.Source, jTexture.HasMipmap);
                }
            } 
            else if (jTexture.Framebuffer != null)
            {
                IFramebuffer framebuffer = jTexture.Framebuffer;
                Span<byte> spanBytes;
                framebuffer.GetMemory(out spanBytes);
                if (null == skTextureEntry.SkTexture)
                {
                    skTextureEntry.SkTexture = new SkTexture(_silkThreeD.GetGL(), jTexture, jTexture.FilteringMode);
                }
                skTextureEntry.SkTexture.SetFrom(
                    framebuffer.Generation,
                    spanBytes, framebuffer.Width, framebuffer.Height);
            }
        }

        public TextureGenerator()
        {
            _engine = I.Get<Engine>();
            _silkThreeD = I.Get<IThreeD>() as SilkThreeD;
        }
    }
}
