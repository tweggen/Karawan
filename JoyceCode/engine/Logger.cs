using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

/*
 * plent-o-matic: your job is our business
 *
 * 
 * 
 */
namespace engine
{
    static public class Logger
    {
        private static object _lo = new();

        private static ILogTarget _logTarget = null;


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
            var now = DateTime.UtcNow;

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

            StackFrame stackFrame = new StackFrame(2, true);
            var fileName = stackFrame.GetFileName();
            if (null == fileName) fileName = "";
            var lineNumber = stackFrame.GetFileLineNumber();
            string type = "(null type name)";
            string methodName = "(null method name)";
            var method = stackFrame.GetMethod();
            if (method != null)
            {
                var reflectedType = method.ReflectedType;
                if (reflectedType != null)
                {
                    if (reflectedType.Name != null)
                    {
                        type = reflectedType.Name;
                    }
                }

                if (method.Name != null)
                {
                    methodName = method.Name;
                }
            }

            return $"{now}{now.Millisecond:D3}{now.Microsecond:D3} {fileName}:{lineNumber}: {type}:{methodName}: {strLevel}: {msg}";
        }
        

        public static void Log(in Level level, in string logEntry)
        {
            ILogTarget logTarget = null;
            lock (_lo) {
                logTarget = _logTarget;
            }
            if (null != logTarget)
            {
                logTarget.AddLogEntry(level, logEntry);
            }
            else
            {
                Console.WriteLine(logEntry);
            }
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


        [DoesNotReturn]
        public static void ErrorThrow(in string msg, Func<string, SystemException> excFunc )
        {
            var logEntry = _createLogEntry(Level.Error, msg);
            Log(Level.Error, logEntry);
            throw excFunc(logEntry);
        }
        

        [DoesNotReturn]
        public static void ErrorThrow<E>(in string msg) where E : new()
        {
            var logEntry = _createLogEntry(Level.Error, msg);
            Log(Level.Error, logEntry);
            throw (System.Exception) Activator.CreateInstance(typeof(E),msg);
        }
        

        public static void Fatal(in string module, in string msg)
        {
            var logEntry = _createLogEntry(Level.Error, (msg!=null)?msg:"[no message provided]");
            Log(Level.Fatal, msg);
            throw new InvalidOperationException(logEntry);
        }
        

        public static void SetLogTarget( ILogTarget logTarget )
        {
            lock(_lo)
            {
                _logTarget = logTarget;
            }
        }
    }
}
