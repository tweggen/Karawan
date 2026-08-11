using System;
using System.IO;

namespace engine;

/**
 * Finds the game's content root by SEARCHING UPWARD for a marker, rather than counting
 * "../.." from wherever the process happens to have been started.
 *
 * WHY THIS EXISTS
 *
 * Every launcher used to carry its own hardcoded chain, tuned by hand against one launch
 * method on one machine:
 *
 *     Karawan/DesktopMain.cs      "../../../../../models/"        (5)
 *     Karawan/DesktopMain.cs      "../../../../nogame/generated/" (4)
 *     Chushi/ConsoleMain.cs       4, then 5, then 6, tried in turn
 *
 * Chushi is the tell. Three escalating fallbacks are what happens when the answer is
 * "however deep this particular checkout happens to be": somebody hit it, added another
 * level, and moved on. The count is not a property of the code, it is a property of the
 * directory the process was launched from - which varies with the launch METHOD (dotnet
 * run vs. running the built exe vs. an IDE profile), with configuration and RID segments
 * in the output path, and with how deep the repository sits under the user's home.
 *
 * THE FAILURE IT PRODUCES
 *
 * `dotnet run` from Karawan/ resolved the game config to C:\Users\<user>\models\game.json
 * - outside the repository entirely - and threw DirectoryNotFoundException naming a path
 * no human ever configured. The five dots were calibrated for a CWD of
 * <repo>/Karawan/bin/Debug/<tfm>/<rid>, which is exactly five below the root; from the
 * project directory the same five walk out of the checkout and into the home directory.
 *
 * The distance varied by machine too, which is what made it look like a machine problem:
 * a repo at ~/coding/github/Karawan and one at ~/coding/twg/github/Karawan land on
 * different wrong answers.
 *
 * WHAT THIS DOES INSTEAD
 *
 * Walk up from the current directory, then from the directory the assembly was loaded
 * from, looking for a marker that only the content root has. Depth stops mattering, and
 * so does the launch method.
 */
public static class GameRoot
{
    /**
     * Relative paths that exist at the content root and nowhere above it. The models
     * markers come first: an installed build has models content but no solution file.
     */
    private static readonly string[] _markers =
    {
        Path.Combine("models", "nogame.json"),
        Path.Combine("models", "game.launch.json"),
        "Karawan.sln",
    };

    /**
     * The content root, or null when nothing matched - which is the normal answer for an
     * installed build, where content sits beside the executable rather than above it.
     *
     * Callers should treat null as "use the installed layout", not as an error.
     */
    public static string? Find()
    {
        foreach (string? start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            if (string.IsNullOrEmpty(start)) continue;

            DirectoryInfo? dir;
            try
            {
                dir = new DirectoryInfo(start);
            }
            catch (Exception)
            {
                continue;
            }

            while (null != dir)
            {
                foreach (string marker in _markers)
                {
                    if (File.Exists(Path.Combine(dir.FullName, marker)))
                    {
                        return dir.FullName;
                    }
                }

                dir = dir.Parent;
            }
        }

        return null;
    }

    /**
     * Content root joined with a relative path, with a trailing separator, or null if the
     * root could not be found. Trailing separator because several callers concatenate
     * rather than Path.Combine.
     */
    public static string? PathTo(string relative)
    {
        string? root = Find();
        if (null == root) return null;

        string full = Path.GetFullPath(Path.Combine(root, relative));
        if (!full.EndsWith(Path.DirectorySeparatorChar))
        {
            full += Path.DirectorySeparatorChar;
        }

        return full;
    }
}
