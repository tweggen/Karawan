using System;
using System.Collections.Generic;
using System.Text;

namespace engine
{
    public interface IBehavior
    {
        public void OnBehave(in DefaultEcs.Entity entity, float dt);
    }
}
