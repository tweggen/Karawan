using System;
using Google.Protobuf;
using Grpc.Core;
using Wire;

namespace WireClient
{
    public class API
    {
        private object _lo = new();
        private Svc.SvcClient? _client = null;

        public event EventHandler<EngineExecutionStatus> ExecutionStatusChanged;
        private EngineExecutionStatus? _engineExecutionStatus = null;


        private void _maybeNewEngineExecutionStatus( EngineExecutionStatus engineExecutionStatus )
        {
            bool emitChange = false;
            EngineExecutionStatus? newStatus = null;
            lock( _lo )
            {
                if (!engineExecutionStatus.Equals(_engineExecutionStatus))
                {
                    _engineExecutionStatus = engineExecutionStatus.Clone();
                    newStatus = _engineExecutionStatus;
                    emitChange = true;
                }
            }
            if (emitChange)
            {
                if (null != newStatus)
                {
                    ExecutionStatusChanged?.Invoke(this, newStatus);
                }
            }
        }


        private void _checkClient()
        {
            if (null == _client)
            {
                throw new InvalidOperationException("Trying to use client while not connected");
            }
            return;
        }


        public void Continue()
        {
            _checkClient();
            var reply = _client?.Continue(new ContinueParams());
            _maybeNewEngineExecutionStatus(reply);

        }


        public void Pause()
        {
            _checkClient();
            var reply = _client?.Pause(new PauseParams());
            _maybeNewEngineExecutionStatus(reply);

        }

        public IList<Wire.Entity> GetEntities()
        {
            _checkClient();
            var reply = _client?.GetEntityList(new GetEntityListParams());
            return reply.Entities;
        }


        public API(in string host, in ushort port)
        {
            _engineExecutionStatus = new EngineExecutionStatus();
            _engineExecutionStatus.State = EngineExecutionState.Initialized;

            var channel = new Channel(
                host, port, ChannelCredentials.Insecure
            );

            _client = new Svc.SvcClient(channel);
        }
    }
}