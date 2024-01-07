using System;
using System.Collections.Generic;
using System.Text;

namespace engine.behave.components
{
    public struct Behavior
    {
        public IBehavior Provider;
        public float MaxDistance = 150f;
        
        public override string ToString()
        {
            return $"Provider={Provider.GetType()}";
        }
        
        public Behavior(IBehavior provider)
        {
            Provider = provider;
        }
    }
}
