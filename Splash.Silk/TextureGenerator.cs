﻿

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
            string resourcePath = _engine.GetConfigParam("Engine.ResourcePath");

            if (texturePath == "joyce://col00000000")
            {
                skTextureEntry.SkTexture = new SkTexture(_silkThreeD.GetGL(), _arrBlack, 1, 1);
            } else
            {
                string path = resourcePath + jTexture.Source;
                skTextureEntry.SkTexture = new SkTexture(_silkThreeD.GetGL(), path);
            }

#if false
            bool canFreeImage = true;

            Image imgNew = new Image();
            imgNew.width = 0;
            if (jTexture.Source.Length==0 || jTexture.Source == "joyce://chessboard")
            {
                if( _imgChessboard.width == 0 )
                {
                    _imgChessboard = Raylib_CsLo.Raylib.GenImageChecked(2, 2, 1, 1, Raylib_CsLo.Raylib.RED, Raylib_CsLo.Raylib.GREEN);
                }
                imgNew = _imgChessboard;
                canFreeImage = false;
            } else if(jTexture.Source=="joyce://64MB")
            {
                if( _img64MB.width == 0)
                {
                    _img64MB = Raylib_CsLo.Raylib.GenImageColor(4096, 4096, Raylib_CsLo.Raylib.WHITE);
                }
                imgNew = _img64MB;
                canFreeImage = false;

            } else if(jTexture.Source=="joyce://col00000000" )
            {
                if (_imgBlack.width == 0)
                {
                    _imgBlack = Raylib_CsLo.Raylib.GenImageColor(1, 1, new Color(0, 0, 0, 0));
                }
                imgNew = _imgBlack;
                canFreeImage = false;

            }
            {
                string path = resourcePath + jTexture.Source;
                rlTextureEntry.RlTexture = Raylib_CsLo.Raylib.LoadTexture(path);
                canFreeImage = false;
            }

            if (imgNew.width > 0)
            {
                rlTextureEntry.RlTexture = Raylib_CsLo.Raylib.LoadTextureFromImage(imgNew);
            }
            if (canFreeImage)
            {
                Raylib_CsLo.Raylib.UnloadImage(imgNew);
            }
#endif
        }

        public TextureGenerator(engine.Engine engine, in SilkThreeD silkThreeD)
        {
            _engine = engine;
            _silkThreeD = silkThreeD;
        }
    }
}