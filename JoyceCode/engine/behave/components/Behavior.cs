using System;
using System.Collections.Generic;
using System.Text;

namespace engine.behave.components
{
    public struct Behavior
    {
        public IBehavior Provider;
        public short MaxDistance = 150;
        public ushort Flags;

        [Flags] 
        public enum BehaviorFlags
        {
            InRange = 0x0001,
            DontVisibInRange = 0x0002,
            DontCallBehave = 0x0004,
            DontEstimateMotion = 0x0008
        }

        
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
