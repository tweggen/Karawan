using System;
using System.Collections.Generic;
using static engine.Logger;

namespace engine.joyce;

public class MaterialCache : ObjectRegistry<Material>
{
    public Material FindMaterial(in Material referenceMaterial) => FindLike(referenceMaterial);
}