using BepuPhysics;
using System;
using System.Collections.Generic;
using System.Text;

namespace engine
{
    public class ConsoleLogger : ILogTarget
    {
        private object _lo = new();
        private System.Threading.Thread _loggingThread;
        private List<string> _listBuffer = new();
        private engine.Engine _engine;

        private const int DEBUG_CHUNK_LINES = 30;

        public void AddLogEntry(in engine.Logger.Level level, in string logEntry)
        {
#if !DEBUG
            if( level > engine.Logger.Level.Warning )
            {
                return;
            }
#endif
            lock(_lo)
            {
                _listBuffer.Add(logEntry);
            }
        }

        private void _dumpWhatWeHave()
        {
            int startIndex = 0;
            List<string> outlist;
            lock (_lo)
            {
                int rightNow = DEBUG_CHUNK_LINES;

                if (_listBuffer.Count == 0)
                {
                    return;
                }

                if (_listBuffer.Count > 5000)
                {
                    rightNow = _listBuffer.Count;
                }
                startIndex = _listBuffer.Count;
                if (startIndex > rightNow)
                {
                    startIndex -= rightNow;
                    outlist = _listBuffer.GetRange(startIndex, rightNow);
                    _listBuffer.RemoveRange(startIndex, rightNow);
                }
                else
                {
                    outlist = _listBuffer;
                    _listBuffer = new();
                }

                foreach (var line in outlist)
                {
                    Console.WriteLine(line);
                }
            }
        }

        private void _loggingThreadFunction()
        {
            while (_engine.IsRunning())
            {
                System.Threading.Thread.Sleep(100);
                _dumpWhatWeHave();
            }
        }

        public ConsoleLogger(engine.Engine engine) 
        {
            _engine = engine;
            _loggingThread = new(_loggingThreadFunction);
            _loggingThread.Priority = System.Threading.ThreadPriority.Lowest;
            _loggingThread.Start();
        }
    }
}
