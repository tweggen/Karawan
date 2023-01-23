using System;
using Raylib_CsLo;

namespace Karawan.platform.cs1.splash
{
    class TextureGenerator
    {
        public void CreateRaylibTexture(engine.joyce.Texture jTexture, out RlTextureEntry rlTextureEntry)
        {
            rlTextureEntry = new();
            Image imgNew;
            if (jTexture.Source.Length==0 || jTexture.Source == "joyce://check")
            {
                imgNew = Raylib.GenImageChecked(2, 2, 1, 1, Raylib.RED, Raylib.GREEN);
            } else if(jTexture.Source=="joyce://64MB")
            {
                imgNew = Raylib.GenImageColor(4096, 4096, Raylib.WHITE);

            } else
            {
                Console.WriteLine("Problem.");
                return;
            }

            rlTextureEntry.RlTexture = Raylib.LoadTextureFromImage(imgNew);
            Raylib.UnloadImage(imgNew);
        }
    }
}
