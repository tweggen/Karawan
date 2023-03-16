using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;

using OpenTelemetry;
using OpenTelemetry.Logs;
using System.Diagnostics.Metrics;
using System.Diagnostics;
using System.Threading;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using OpenTelemetry.Logs;
using OpenTelemetry.Internal;
using System.Diagnostics;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Diagnostics;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Instrumentation;
using System.Runtime.CompilerServices;


namespace Karawan
{
    
    
    public class DesktopMain
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            //builder.Services.AddGrpc();

            var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddHttpClientInstrumentation((options) =>
                {
                    // Note: Only called on .NET & .NET Core runtimes.
                    options.EnrichWithHttpRequestMessage = (activity, httpRequestMessage) =>
                    {
                        activity.SetTag("requestVersion", httpRequestMessage.Version);
                    };
                    // Note: Only called on .NET & .NET Core runtimes.
                    options.EnrichWithHttpResponseMessage = (activity, httpResponseMessage) =>
                    {
                        activity.SetTag("responseVersion", httpResponseMessage.Version);
                    };
                    // Note: Called for all runtimes.
                    options.EnrichWithException = (activity, exception) =>
                    {
                        activity.SetTag("stackTrace", exception.StackTrace);
                    };
                })
                .AddConsoleExporter()
                .Build();
            builder.Logging.AddOpenTelemetry(opts =>
            {
                opts.IncludeFormattedMessage = true;
                opts.IncludeScopes = true;
                opts.ParseStateValues = true;
#if true                
                opts.AddOtlpExporter(optsExporter =>
                {
#if false                    
                    optsExporter.Endpoint = new Uri("https://otlp-gateway-prod-ap-southeast-0.grafana.net/otlp");
                    optsExporter.Protocol = OtlpExportProtocol.HttpProtobuf;
                    string user = "409403";
                    string password =
                        "eyJrIjoiODUzZGRmNWRiMTk0NzcxNjZiZDZkZThiZjc4YzdjN2M3OWJiNzBkMCIsIm4iOiJzaWxpY29uX2Rlc2VydDIgTWV0cmljcyIsImlkIjo4MTQwNjJ9";
                    var authStringUtf8 = System.Text.Encoding.UTF8.GetBytes($"{user}:{password}");
                    var autoStringBase64 = System.Convert.ToBase64String(authStringUtf8);
                    optsExporter.Headers = $"Authorization=Basic {autoStringBase64}";
                    // optsExporter.Headers = "Authorization=Bearer an_apm_secret_token";
                    // api key stack-559779-easystart-prom-publisher
#endif
#if true
                    //string endpoint = "https://silicon-desert.apm.europe-west3.gcp.cloud.es.io:8200";
                    //string endpoint = "https://f3fddad667a54b6bb8c3c14b494ead51.apm.europe-west3.gcp.cloud.es.io:8200";
                    string endpoint = "https://f3fddad667a54b6bb8c3c14b494ead51.apm.europe-west3.gcp.cloud.es.io:443";
                
                    optsExporter.Endpoint = new Uri(endpoint);
                    optsExporter.Protocol = OtlpExportProtocol.HttpProtobuf;
                    //string apiKey = "RDlHeDRJWUJYTWZWaU5hMDZSaDk6SmgzWHFDblVSa2FTSWZTSlpMNDJpQQ==";
                    string secretToken = "HVCjmPWUTg0xJH689C";
                    optsExporter.Headers = $"Authentication=Bearer%20{secretToken}";
#endif
                });
#endif
                //opts.AddConsoleExporter();
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
            
            var threadApp = new Thread( () => app.Run());
            threadApp.Start();

            e.AddSceneFactory("root", () => new nogame.RootScene());
            e.AddSceneFactory("logos", () => new nogame.LogosScene());
            e.SetMainScene("logos");
            boom.SetupDone();
            e.Execute();
            
            Boom.AudioPlaybackEngine.Instance.Dispose();
        }
    }
}
