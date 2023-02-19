using Raylib_CsLo;

namespace Karawan.platform.cs1.splash
{
    public class TextureGenerator
    {
        private Image _imgBlack;
        private Image _img64MB;
        private Image _imgChessboard;
        private engine.Engine _engine;

        public void CreateRaylibTexture(in engine.joyce.Texture jTexture, out RlTextureEntry rlTextureEntry)
        {
            string resourcePath = _engine.GetConfigParam("Engine.ResourcePath");

            bool canFreeImage = true;
            rlTextureEntry = new();
            Image imgNew = new Image();
            imgNew.width = 0;
            if (jTexture.Source.Length==0 || jTexture.Source == "joyce://chessboard")
            {
                if( _imgChessboard.width == 0 )
                {
                    _imgChessboard = Raylib.GenImageChecked(2, 2, 1, 1, Raylib.RED, Raylib.GREEN);
                }
                imgNew = _imgChessboard;
                canFreeImage = false;
            } else if(jTexture.Source=="joyce://64MB")
            {
                if( _img64MB.width == 0)
                {
                    _img64MB = Raylib.GenImageColor(4096, 4096, Raylib.WHITE);
                }
                imgNew = _img64MB;
                canFreeImage = false;

            } else if(jTexture.Source=="joyce://col00000000" )
            {
                if (_imgBlack.width == 0)
                {
                    _imgBlack = Raylib.GenImageColor(1, 1, new Color(0, 0, 0, 0));
                }
                imgNew = _imgBlack;
                canFreeImage = false;

            }
            {
                string path = resourcePath + jTexture.Source;
                rlTextureEntry.RlTexture = Raylib.LoadTexture(path);
                canFreeImage = false;
            }

            if (imgNew.width > 0)
            {
                rlTextureEntry.RlTexture = Raylib.LoadTextureFromImage(imgNew);
            }
            if (canFreeImage)
            {
                Raylib.UnloadImage(imgNew);
            }
        }

        public TextureGenerator(engine.Engine engine)
        {
            _engine = engine;
        }
    }
}
