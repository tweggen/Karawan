using BepuPhysics;
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;

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
        public ILogger Logger { get; private set; }

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

                foreach (var logEntry in outlist)
                {
#if true
                    Console.WriteLine($"{logEntry.Level}: {logEntry.Message}");
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
        }

        private void _loggingThreadFunction()
        {
            while (_engine.IsRunning())
            {
                System.Threading.Thread.Sleep(100);
                _dumpWhatWeHave();
            }
        }

        public ConsoleLogger(in engine.Engine engine, in ILogger logger) 
        {
            _engine = engine;
#if false
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddOpenTelemetry(options =>
                {
                    options.AddConsoleExporter();

                });
            });
            Logger = loggerFactory.CreateLogger<ConsoleLogger>();
#else
            Logger = logger;
#endif
            _loggingThread = new(_loggingThreadFunction);
            _loggingThread.Priority = System.Threading.ThreadPriority.Lowest;
            _loggingThread.Start();
        }
    }
}
