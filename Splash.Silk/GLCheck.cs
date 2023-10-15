using Silk.NET.OpenGL;
using static engine.Logger;

namespace Splash.Silk;

static public class GLCheck
{
    public static int CheckError(GL gl, string what)
    {
        int err = 0;
        while (true)
        {
            var error = gl.GetError();
            if (error != GLEnum.NoError)
            {
                Error($"Found OpenGL {what} error {error}");
                err += (int)error;
            }
            else
            {
                // Console.WriteLine($"OK: {what}");
                return err;
            }
        }
    }
}