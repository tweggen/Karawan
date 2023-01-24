using System;
using Raylib_CsLo;

namespace Karawan.platform.cs1.splash
{
    class TextureGenerator
    {
        private Image _img64MB;
        private Image _imgChessboard;

        public void CreateRaylibTexture(engine.joyce.Texture jTexture, out RlTextureEntry rlTextureEntry)
        {
            bool canFreeImage = true;
            rlTextureEntry = new();
            Image imgNew;
            if (jTexture.Source.Length==0 || jTexture.Source == "joyce://chessboard")
            {
                if( _imgChessboard.width == 0 )
                {
                    _imgChessboard = Raylib.GenImageChecked(2, 2, 1, 1, Raylib.RED, Raylib.GREEN);
                }
                imgNew = _imgChessboard;
            } else if(jTexture.Source=="joyce://64MB")
            {
                if( _img64MB.width == 0)
                {
                    _img64MB = Raylib.GenImageColor(4096, 4096, Raylib.WHITE);
                }
                imgNew = _img64MB;
                canFreeImage = false;

            } else
            {
                Console.WriteLine("Problem.");
                return;
            }

            rlTextureEntry.RlTexture = Raylib.LoadTextureFromImage(imgNew);
            if (canFreeImage)
            {
                Raylib.UnloadImage(imgNew);
            }
        }
    }
}
