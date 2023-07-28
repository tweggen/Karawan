
using System;
using System.Numerics;
using System.Threading.Tasks;
using builtin.loader;
using engine.geom;
using engine.joyce;

namespace engine;

public class InstantiateModelParams
{
    public static int CENTER_X = 0x0001;
    public static int CENTER_Y = 0x0002;
    public static int CENTER_Z = 0x0004;
    public static int CENTER_X_POINTS = 0x0010;
    public static int CENTER_Y_POINTS = 0x0020;
    public static int CENTER_Z_POINTS = 0x0040;
    public static int ROTATE_X90 = 0x0100;
    public static int ROTATE_Y90 = 0x0200;
    public static int ROTATE_Z90 = 0x0400;
    public static int ROTATE_X180 = 0x1000;
    public static int ROTATE_Y180 = 0x2000;
    public static int ROTATE_Z180 = 0x4000;

    public int GeomFlags = 0;

    public string Hash()
    {
        return $"InstantiateModelParams({GeomFlags})";
    }
}


/**
 * Represent a loaded or generated model.
 */
public class Model
{
    public InstanceDesc InstanceDesc;
    public ModelInfo ModelInfo;
}