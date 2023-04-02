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

using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Internal;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Diagnostics;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Instrumentation;
using OpenTelemetry.Resources;
using System.Runtime.CompilerServices;
using System.Net.Http;
using System.Reflection.PortableExecutable;
using OpenTelemetry.Instrumentation.AspNetCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Karawan
{
#if false
    public class Instrumentation : IDisposable
    {
        internal const string ActivitySourceName = "Examples.AspNetCore";
        internal const string MeterName = "Examples.AspNetCore";
        private readonly Meter meter;

        public Instrumentation()
        {
            string? version = typeof(Instrumentation).Assembly.GetName().Version?.ToString();
            this.ActivitySource = new ActivitySource(ActivitySourceName, version);
            this.meter = new Meter(MeterName, version);
            this.FreezingDaysCounter = this.meter.CreateCounter<long>("weather.days.freezing", "The number of days where the temperature is below freezing");
        }

        public ActivitySource ActivitySource { get; }

        public Counter<long> FreezingDaysCounter { get; }

        public void Dispose()
        {
            this.ActivitySource.Dispose();
            this.meter.Dispose();
        }
    }
#endif

    public class DesktopMain
    {
        
        public static void Main(string[] args)
        {
            var appBuilder = WebApplication.CreateBuilder(args);

#if false
            // Build a resource configuration action to set service information.
            Action<ResourceBuilder> configureResource = r => r.AddService(
                serviceName: "silicon_desert2",
                serviceVersion: typeof(DesktopMain).Assembly.GetName().Version?.ToString() ?? "unknown",
                serviceInstanceId: Environment.MachineName);

            // Create a service to expose ActivitySource, and Metric Instruments
            // for manual instrumentation
            appBuilder.Services.AddSingleton<Instrumentation>();
#endif
#if false
            var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddHttpClientInstrumentation((options) =>
            {
                // Note: Only called on .NET & .NET Core runtimes.
                options.EnrichWithHttpRequestMessage = (activity, httpRequestMessage) =>
                {
                    Console.WriteLine($"httpRequest: {httpRequestMessage}");
                    activity.SetTag("requestVersion", httpRequestMessage.Version);
                };
                // Note: Only called on .NET & .NET Core runtimes.
                options.EnrichWithHttpResponseMessage = (activity, httpResponseMessage) =>
                {
                    Console.WriteLine($"httpResponse: {httpResponseMessage}");
                    activity.SetTag("responseVersion", httpResponseMessage.Version);
                };
                // Note: Called for all runtimes.
                options.EnrichWithException = (activity, exception) =>
                {
                    Console.WriteLine($"httpException: {exception}");
                    activity.SetTag("stackTrace", exception.StackTrace);
                };
            })
            .AddConsoleExporter()
            .Build();
#endif
#if false
            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddHttpClientInstrumentation(
                    // Note: Only called on .NET & .NET Core runtimes.
                    (options) => options.FilterHttpRequestMessage =
                        (httpRequestMessage) =>
                        {
                            Console.WriteLine($"httpRequestMessage: {httpRequestMessage}");
                            return true;
                        })
                .AddConsoleExporter()
                .Build();
#endif
#if false
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddHttpClientInstrumentation()
                .AddAspNetCoreInstrumentation()
                .AddConsoleExporter()
                .Build();
#endif
#if false
            appBuilder.Logging.ClearProviders();
            appBuilder.Logging.AddOpenTelemetry(opts =>
            {
                var resourceBuilder = ResourceBuilder.CreateDefault();
                configureResource(resourceBuilder);
                opts.SetResourceBuilder(resourceBuilder);
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
                    optsExporter.TimeoutMilliseconds = 1000;
                    // var headers = optsExporter.GetHeaders<Dictionary<string, string>>((d, k, v) => d.Add(k, v));
                    //string apiKey = "RDlHeDRJWUJYTWZWaU5hMDZSaDk6SmgzWHFDblVSa2FTSWZTSlpMNDJpQQ==";
                    string secretToken = "HVCjmPWUTg0xJH689C";
                    optsExporter.Headers = $"Authentication=Bearer%20{secretToken}";
#endif
                });
#endif
                //opts.AddConsoleExporter();
            });
#endif

            appBuilder.Logging.AddEventLog();
            appBuilder.Logging.AddConsole();

            var app = appBuilder.Build();

            // var e = Splash.Raylib.Platform.EasyCreate(args);
            var e = Splash.Silk.Platform.EasyCreate(args);

            {
                engine.ConsoleLogger logger = new(e, app.Logger);
                engine.Logger.SetLogTarget(logger);
            }
            
            engine.GlobalSettings.Set("Engine.ResourcePath", "..\\..\\..\\..\\");
            
            // Boom.API boom = new(e);

            // Add the engine web service to the host.
            // app.MapGrpcService<GreeterService>();
            // app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client.
            // To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");
            
            var threadApp = new Thread( () => app.Run());
            threadApp.Start();

            e.AddSceneFactory("root", () => new nogame.RootScene());
            e.AddSceneFactory("logos", () => new nogame.LogosScene());
            e.SetMainScene("logos");
            // boom.SetupDone();
            e.Execute();

            app.StopAsync();
            Boom.AudioPlaybackEngine.Instance.Dispose();
        }
    }
}
