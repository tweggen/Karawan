using System;
using System.Collections.Generic;
using System.Text;

namespace engine.behave.components
{
    public class Behavior
    {
        public engine.IBehavior Provider;
        public override string ToString()
        {
            return $"Provider={Provider.GetType()}";
        }
        
        public Behavior(engine.IBehavior provider)
        {
            Provider = provider;
        }
    }
}
