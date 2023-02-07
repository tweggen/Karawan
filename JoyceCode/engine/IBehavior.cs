using System;
using System.Collections.Generic;
using System.Text;

namespace engine
{
    public interface IBehavior
    {
        public void Behave(in DefaultEcs.Entity entity, float dt);
    }
}
