using System;
using System.Collections.Generic;
using System.Text;

namespace engine
{
    public interface IBehavior
    {
        public void OnBehave(float dt);
    }
}
