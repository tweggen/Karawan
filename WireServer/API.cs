
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Grpc.Core;
using Wire;
using static engine.Logger;

namespace WireServer;

public class API
{
    private Server _wireServer;
    private ServerImplementation _serverImplementation;

    
    public API(engine.Engine engine, ushort port)
    {
        // var pair = new KeyCertificatePair(
        //     File.ReadAllText("cert/service.pem"),
        //     File.ReadAllText("cert/service-key.pem")
        // );
        // var creds = new SslServerCredentials(new[] { pair });
        _serverImplementation = new ServerImplementation(engine);
        _wireServer = new Server
        {
            Services = { Svc.BindService(_serverImplementation) },
            Ports = { new ServerPort("127.0.0.1", port, ServerCredentials.Insecure) }
        };
        _wireServer.Start();
        Wonder($"Server listening at port {port}.");
    }
}