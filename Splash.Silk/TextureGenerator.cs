

namespace Splash.Silk
{
    public class TextureGenerator
    {
        //private Image _imgBlack;
        //private Image _img64MB;
        //private Image _imgChessboard;
        private readonly engine.Engine _engine;
        private readonly SilkThreeD _silkThreeD;
        private static Byte[] _arrBlack = { 0, 0, 0, 0 };

        public void FillTextureEntry(in SkTextureEntry skTextureEntry)
        {
            engine.joyce.Texture jTexture = skTextureEntry.JTexture;
            string texturePath = jTexture.Source;

            if (texturePath == "joyce://col00000000")
            {
                skTextureEntry.SkTexture = new SkTexture(_silkThreeD.GetGL(), _arrBlack, 1, 1);
            } else
            {
                skTextureEntry.SkTexture = new SkTexture(_silkThreeD.GetGL(), jTexture.Source);
            }
        }

        public TextureGenerator(engine.Engine engine, in SilkThreeD silkThreeD)
        {
            _engine = engine;
            _silkThreeD = silkThreeD;
        }
    }
}
