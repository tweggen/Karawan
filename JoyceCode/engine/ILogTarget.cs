using System;
using System.Collections.Generic;
using System.Text;

namespace engine
{
    public interface ILogTarget
    {
        public void AddLogEntry(in engine.Logger.Level level, in string logEntry);
    }
}
