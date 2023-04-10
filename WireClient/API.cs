using System;
using Grpc.Core;
using Wire;

namespace WireClient
{
    public class API
    {
        private Svc.SvcClient? _client = null;
        public long Calculate(in int x, in int y, in string op)
        {
            if (null == _client)
            {
                return 0;
            }
            var reply = _client.Calculate(new CalculateRequest {
                X = 10, Y = 20, Op = "+" });
            // Console.WriteLine($"The calculated result is: {reply.Result}");
            return reply.Result;
        }
        
        public API(in string host, in ushort port)
        {
            var channel = new Channel(
                host, port, ChannelCredentials.Insecure
            );
            _client = new Svc.SvcClient(channel);
        }
    }
}