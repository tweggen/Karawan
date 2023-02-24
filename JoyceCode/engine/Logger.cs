using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text;

namespace engine
{
    static class Logger
    {
        public enum Level
        {
            Fatal = 0,
            Error = 100,
            Warning = 200,
            Wonder = 300,
            Trace = 400,
            Detail = 500,
            All = 1000
        }

        private static string _createLogEntry(in Level level, in string msg)
        {
            StackFrame stackFrame = new StackFrame(2);
            var fileName = stackFrame.GetFileName();
            var lineNumber = stackFrame.GetFileLineNumber();
            var type = stackFrame.GetType();
            var methodName = stackFrame.GetMethod().Name;
            return $"{fileName}:{lineNumber}: {type}:{methodName}: {level}: {msg}";
        }

        private static void _log(in Level level, in string logEntry)
        {
            Console.WriteLine(logEntry);
        }

        public static void Trace(in string msg)
        {
            _log(Level.Trace, _createLogEntry(Level.Trace, msg));
        }

        public static void Wonder(in string msg)
        {
            _log(Level.Wonder, _createLogEntry(Level.Wonder, msg));
        }

        public static void Warning(in string msg)
        {
            _log(Level.Warning, _createLogEntry(Level.Warning, msg));
        }

        public static void Error(in string msg)
        {
            _log(Level.Error, _createLogEntry(Level.Error, msg));
        }

        public static void ErrorThrow(in string msg, Func<string, SystemException> excFunc )
        {
            var logEntry = _createLogEntry(Level.Error, msg);
            _log(Level.Error, logEntry);
            throw excFunc(logEntry);
        }

        public static void Fatal(in string module, in string msg)
        {
            var logEntry = _createLogEntry(Level.Error, msg);
            _log(Level.Fatal, msg);
            throw new InvalidOperationException(logEntry);
        }
    }
}
