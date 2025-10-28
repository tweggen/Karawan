using System.Diagnostics;
using engine;
using engine.joyce;
using static engine.Logger;

namespace Chushi;

public class ConsoleMain
{
    public static void Main(string[] args)
    {
        // System.Environment.SetEnvironmentVariable("ALSOFT_LOGLEVEL", "3");

        var cwd = System.IO.Directory.GetCurrentDirectory();
        string jsonPath;
        /*
         * Setup globals and statics
         */
        engine.GlobalSettings.Set("platform.threeD.API", "OpenGL");
        engine.GlobalSettings.Set("platform.threeD.API.version", "430");
        engine.GlobalSettings.Set("engine.NailLogicalFPS", "true");
        if (Directory.Exists("assets"))
        {
            /*
             * This is when we start installed on windows.
             */
            engine.GlobalSettings.Set("Engine.ResourcePath", "./assets/");
            jsonPath = "./";
        }
        else
        {
            if (Path.Exists("./models/nogame.json"))
            {
                /*
                 * I don't know this case.
                 */
                engine.GlobalSettings.Set("Engine.ResourcePath", "./nogame/");
                jsonPath = "../models/";
            }
            else if (Path.Exists("../../../../nogame/"))
            {
                /*
                 * This is when we start from the debugger on windows in Karawan
                 */
                jsonPath = "../models/";
                engine.GlobalSettings.Set("Engine.ResourcePath", "../../../../nogame/");
            } else if (Path.Exists("../../../../../nogame/"))
            {
                /*
                 * This is in Chushi on windows.
                 */
                jsonPath = "../models/";
                engine.GlobalSettings.Set("Engine.ResourcePath", "../../../../../nogame/");
            } else if (Path.Exists("../../../../../../nogame/"))
            {
                /*
                 * This is in Chushi on windows.
                 */
                jsonPath = "../models/";
                engine.GlobalSettings.Set("Engine.ResourcePath", "../../../../../../nogame/");
            }
            else
            {
                Console.Error.WriteLine($"Running in unknown environment, cwd=={cwd}");
                System.Environment.Exit(-1);
                return;
            }
        }

        {
            string userRWPath = System.Environment.GetFolderPath(
                Environment.SpecialFolder.ApplicationData);
            string vendorRWPath = Path.Combine(userRWPath, "nassau records");
            string appRWPath = Path.Combine(vendorRWPath, "silicondesert2");
            System.IO.Directory.CreateDirectory(appRWPath);
            engine.GlobalSettings.Set("Engine.RWPath", appRWPath);
        }

        // TXWTODO: How to know the underlying platform does have touch events?
        engine.GlobalSettings.Set("splash.touchControls", "false");
        engine.GlobalSettings.Set("platform.suspendOnUnfocus", "false");
        engine.GlobalSettings.Set("platform.initialZoomState", "0");

        engine.GlobalSettings.Set("joyce.CompileMode", "true");


        I.Register<engine.joyce.TextureCatalogue>(() => new engine.joyce.TextureCatalogue());
        I.Register<engine.joyce.ModelCache>(() => new engine.joyce.ModelCache());


        /*
         * Bootstrap game by directly reading game config, setting up
         * asset implementation with the pathes.
         */
        var iassetDesktop = new Chushi.AssetImplementation();
        engine.casette.Loader cassetteLoader;
        Console.WriteLine($"CWD is {cwd}");
        using (var streamJson =
               File.OpenRead(
                   Path.Combine(
                       engine.GlobalSettings.Get("Engine.ResourcePath"),
                       jsonPath + "nogame.json")))
        {
            cassetteLoader = new engine.casette.Loader(streamJson);
        }

        engine.Assets.SetAssetImplementation(iassetDesktop);
        cassetteLoader.SetAssetLoaderAssociations(iassetDesktop);


        I.Register<engine.Engine>(() => new engine.Engine(null));
        engine.Engine e = I.Get<engine.Engine>();
        e.SetupDone();
        e.PlatformSetupDone();

        cassetteLoader.InterpretConfig();
        Trace($"Starting engine...");
        e.Execute();
        Trace($"Running compilation tasks...");
        List<Task> listTasks = new();
        
        
        
        // var mazuAnimationCompiler = new Mazu.AnimationCompiler();
        Task[] tasks = new Task[]
        {
            Task.Run(async () =>
            {
                //await mazuAnimationCompiler.Compile();
            })
        };
        Task.WaitAll(listTasks);
        Trace($"Done running compilation tasks.");
        Trace($"Stopping engine...");
        e.Exit();
        Trace($"Engine stopped.");
        
        //mazuAnimationCompiler.Dispose();
        
        System.Environment.Exit(0);
    }
}