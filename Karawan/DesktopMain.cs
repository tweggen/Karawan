using System;
using Raylib_CsLo;

// using Microsoft.AspNetCore.Builder;

namespace Karawan
{
    public class DesktopMain
    {
        public static void Main(string[] args)
        {
            // var builder = WebApplication.CreateBuilder(args);
            //builder.Services.AddGrpc();
            // var app = builder.Build();


            var engine = Karawan.platform.cs1.Platform.EasyCreate(args);

            {
                engine.ConsoleLogger logger = new(engine);
            }

            engine.SetConfigParam("Engine.ResourcePath", "..\\..\\..\\..\\");

            Boom.API boom = new(engine);

            // Add the engine web service to the host.
            // app.MapGrpcService<GreeterService>();
            // app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client.
            // To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");
            // app.Run();

            engine.AddSceneFactory("root", () => new nogame.RootScene());
            engine.AddSceneFactory("logos", () => new nogame.LogosScene());
            engine.SetMainScene("logos");
            boom.SetupDone();
            engine.Execute();
            
            Boom.AudioPlaybackEngine.Instance.Dispose();
        }
    }
}
