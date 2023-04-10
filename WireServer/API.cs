
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Grpc.Core;
using Wire;

namespace WireServer;

public class API
{
    
    public API(ushort port)
    {
        // var pair = new KeyCertificatePair(
        //     File.ReadAllText("cert/service.pem"),
        //     File.ReadAllText("cert/service-key.pem")
        // );
        // var creds = new SslServerCredentials(new[] { pair });
        var server = new Server
        {
            Services = { Svc.BindService(new ServerImplementation()) },
            Ports = { new ServerPort("127.0.0.1", port, ServerCredentials.Insecure) }
        };
        server.Start();
        Console.WriteLine($"Server listening at port {port}.");
    }

}