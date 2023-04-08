
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Grpc.Core;
using Wire;

namespace WireServer
{
    public class ServerImplementation : Svc.SvcBase
    {
       public override Task<CalculateReply> Calculate(CalculateRequest request, ServerCallContext context)
        {
            long result = -1;
            switch (request.Op)
            {
                case "+":
                    result = request.X + request.Y;
                    break;
                case "-":
                    result = request.X - request.Y;
                    break;
                case "*":
                    result = request.X * request.Y;
                    break;
                case "/":
                    if (request.Y != 0)
                    {
                        result = (long)request.X / request.Y;
                    }
                    break;
                default:
                    break;
            }
            return Task.FromResult(new CalculateReply { Result = result });
        }

        public override async Task Median(IAsyncStreamReader<Temperature> requestStream, IServerStreamWriter<Temperature> responseStream, ServerCallContext context)
        {
            Console.WriteLine("Median");
            var vals = new List<double>();
            while (await requestStream.MoveNext())
            {
                var temp = requestStream.Current;
                vals.Add(temp.Value);
                double med = 0;
                if (vals.Count == 10)
                {
                    var arr = vals.ToArray();
                    Array.Sort(arr);
                    med = (arr[4] + arr[5]) / 2;
                    vals.Clear();
                    await responseStream.WriteAsync(new Temperature { Timestamp = temp.Timestamp, Value = med });
                }
            }
        }
    }

}