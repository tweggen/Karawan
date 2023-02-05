using System;
using System.Collections.Generic;
using System.Text;

namespace Karawan.platform.cs1.splash.components
{
    struct RlMaterial
    {
        public RlMaterialEntry MaterialEntry;
        public RlMaterial(
            RlMaterialEntry materialEntry
        )
        {
            MaterialEntry = materialEntry;
        }
    }
}
