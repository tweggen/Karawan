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
            string strLevel;

            if( level == Level.Fatal )
            {
                strLevel = "Fatal";
            } else if( level == Level.Error )
            {
                strLevel = "Error";
            } else if( level == Level.Warning )
            {
                strLevel = "Warning";
            } else if( level == Level.Wonder) 
            {
                strLevel = "Wonder";
            } else if( level == Level.Trace ) 
            {
                strLevel = "Trace";
            } else if( level == Level.Detail )
            {
                strLevel = "Detail";
            } else
            {
                strLevel = $"{(int)level}";
            }

            StackFrame stackFrame = new StackFrame(2);
            var fileName = stackFrame.GetFileName();
            var lineNumber = stackFrame.GetFileLineNumber();
            var type = stackFrame.GetMethod().ReflectedType.Name;
            var methodName = stackFrame.GetMethod().Name;
            return $"{fileName}:{lineNumber}: {type}:{methodName}: {strLevel}: {msg}";
        }

        public static void Log(in Level level, in string logEntry)
        {
            Console.WriteLine(logEntry);
        }

        public static void Trace(in string msg)
        {
            Log(Level.Trace, _createLogEntry(Level.Trace, msg));
        }

        public static void Wonder(in string msg)
        {
            Log(Level.Wonder, _createLogEntry(Level.Wonder, msg));
        }

        public static void Warning(in string msg)
        {
            Log(Level.Warning, _createLogEntry(Level.Warning, msg));
        }

        public static void Error(in string msg)
        {
            Log(Level.Error, _createLogEntry(Level.Error, msg));
        }

        public static void ErrorThrow(in string msg, Func<string, SystemException> excFunc )
        {
            var logEntry = _createLogEntry(Level.Error, msg);
            Log(Level.Error, logEntry);
            throw excFunc(logEntry);
        }

        public static void Fatal(in string module, in string msg)
        {
            var logEntry = _createLogEntry(Level.Error, msg);
            Log(Level.Fatal, msg);
            throw new InvalidOperationException(logEntry);
        }
    }
}
