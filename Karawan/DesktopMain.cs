using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using Raylib_CsLo;

using OpenTelemetry;
using OpenTelemetry.Logs;
using System.Diagnostics.Metrics;
using System.Diagnostics;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using OpenTelemetry.Logs;

namespace Karawan
{
    public class DesktopMain
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            //builder.Services.AddGrpc();

            builder.Logging.AddOpenTelemetry(builder =>
            {
                builder.IncludeFormattedMessage = true;
                builder.IncludeScopes = true;
                builder.ParseStateValues = true;
                //builder.AddOtlpExporter();
                builder.AddConsoleExporter();
            });
            builder.AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri("http://localhost:4317"); // Signoz Endpoint
            });

            builder.Logging.AddEventLog();
            builder.Logging.AddConsole();

            var app = builder.Build();

            var e = Karawan.platform.cs1.Platform.EasyCreate(args);

            {
                engine.ConsoleLogger logger = new(e, app.Logger);
                engine.Logger.SetLogTarget(logger);
            }

            e.SetConfigParam("Engine.ResourcePath", "..\\..\\..\\..\\");

            Boom.API boom = new(e);

            // Add the engine web service to the host.
            // app.MapGrpcService<GreeterService>();
            // app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client.
            // To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");
            // app.Run();

            e.AddSceneFactory("root", () => new nogame.RootScene());
            e.AddSceneFactory("logos", () => new nogame.LogosScene());
            e.SetMainScene("logos");
            boom.SetupDone();
            e.Execute();
            
            Boom.AudioPlaybackEngine.Instance.Dispose();
        }
    }
}
