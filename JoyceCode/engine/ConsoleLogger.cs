using System;
using System.Collections.Generic;
using System.Text;

namespace engine
{
    class LogEntry
    {
        public engine.Logger.Level Level;
        public string Message;

        public LogEntry(in engine.Logger.Level level, in string message)
        {
            Level = level;
            Message = message;
        }
    };
    
    public class ConsoleLogger : ILogTarget
    {
        private object _lo = new();
        private System.Threading.Thread _loggingThread;
        private List<LogEntry> _listBuffer = new();
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
                _listBuffer.Add(new LogEntry(level, logEntry));
            }
        }

        private void _dumpWhatWeHave()
        {
            int startIndex = 0;
            List<LogEntry> outlist;
            lock (_lo)
            {
                if (_listBuffer.Count == 0)
                {
                    return;
                }

                int rightNow;
                if (_listBuffer.Count > 5000)
                {
                    rightNow = _listBuffer.Count;
                }
                else
                {
                    rightNow = Int32.Min(DEBUG_CHUNK_LINES, _listBuffer.Count);

                }

                outlist = _listBuffer.GetRange(startIndex, rightNow);
                _listBuffer.RemoveRange(startIndex, rightNow);
            }

            foreach (var logEntry in outlist)
            {
#if true
                Console.Error.WriteLine($"{logEntry.Level}: {logEntry.Message}");
#else
                string message = logEntry.Message;
                switch (logEntry.Level)
                {
                    default:
                    case engine.Logger.Level.All:
                        Logger.LogInformation(message);
                        break;
                    case engine.Logger.Level.Detail:
                        Logger.LogDebug(message);
                        break;
                    case engine.Logger.Level.Error:
                        Logger.LogError(message);
                        break;
                    case engine.Logger.Level.Fatal:
                        Logger.LogCritical(message);
                        break;
                    case engine.Logger.Level.Trace:
                        Logger.LogTrace(message);
                        break;
                    case engine.Logger.Level.Warning:
                        Logger.LogWarning(message);
                        break;
                    case engine.Logger.Level.Wonder:
                        Logger.LogInformation(message);
                        break;
                }
#endif
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

        public ConsoleLogger(in engine.Engine engine) 
        {
            _engine = engine;
            _loggingThread = new(_loggingThreadFunction);
            _loggingThread.Priority = System.Threading.ThreadPriority.Lowest;
            _loggingThread.Start();
        }
    }
}
