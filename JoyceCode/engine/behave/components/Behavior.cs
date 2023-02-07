using System;
using System.Collections.Generic;
using System.Text;

namespace engine.behave.components
{
    public class Behavior
    {
        public engine.IBehavior Provider;
        public Behavior(engine.IBehavior provider)
        {
            Provider = provider;
        }
    }
}
