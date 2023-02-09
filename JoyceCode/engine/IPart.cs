using System;
using System.Collections.Generic;
using System.Text;

namespace engine
{
    public interface IPart
    {
        public void PartOnLogicalFrame(float dt);

        public void PartDeactivate();
        public void PartActivate(
            in engine.Engine engine0,
            in engine.IScene scene0 );
    }
}
