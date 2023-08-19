using System;
using System.Collections.Generic;
using System.Text;

namespace engine
{
    public interface IInputPart : IModule
    {
        public void InputPartOnInputEvent(engine.news.Event ev);
    }
}
