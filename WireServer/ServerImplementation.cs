
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
        private object _lo = new();

        // TXWTODO: This might lose some transitions.
        private EngineExecutionStatus _currentState = new();

        public void OnNewServerState(EngineExecutionStatus newStatus)
        {
            lock (_lo)
            {
                _currentState = newStatus.Clone();
                Monitor.Pulse(_lo);
            }
        }

        public override Task<EngineExecutionStatus> Pause(PauseParams pauseParams, ServerCallContext context)
        {
            Console.WriteLine("Pause called");
            var status = new EngineExecutionStatus();
            status.State = EngineExecutionState.Stopped;
            return Task.FromResult(status);
        }

        public override Task<EngineExecutionStatus> Continue(ContinueParams pauseParams, ServerCallContext context)
        {
            Console.WriteLine("Continue called");
            var status = new EngineExecutionStatus();
            status.State = EngineExecutionState.Running;
            return Task.FromResult(status);
        }

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


        /**
         * Very bad simluation of an event queue.
         */
        public override async Task ReadEngineState(EngineStateParams request, IServerStreamWriter<EngineExecutionStatus> responseStream, ServerCallContext context)
        {
            Console.WriteLine("ReadEngineState");
            while (true)
            {
                EngineExecutionStatus currentState;
                lock (_lo)
                {
                    /*
                     * First write the current state, then wait for the next
                     */
                    currentState = _currentState.Clone();
                }
                await responseStream.WriteAsync(currentState);

                /*
                 * Write out new game states, as they come in.
                 */
                while (true)
                {
                    EngineExecutionStatus newState = null;
                    bool doSend = false;
                    lock (_lo)
                    {
                        if (currentState.State != _currentState.State)
                        {
                            newState = _currentState.Clone();
                            doSend = true;
                        }
                        else
                        {
                            Monitor.Wait(_lo);
                        }
                    }
                    currentState = newState;
                    if (doSend)
                    {
                        await responseStream.WriteAsync(newState);
                    }
                }
            }
        }
    }

}