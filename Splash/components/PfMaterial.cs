using System;
using System.Collections.Generic;
using System.Text;

namespace Splash.components
{
    struct PfMaterial
    {
        public AMaterialEntry MaterialEntry;
        public PfMaterial(AMaterialEntry materialEntry
        )
        {
            MaterialEntry = materialEntry;
        }
    }
}
