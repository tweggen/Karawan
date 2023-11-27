using System;
using System.Collections.Generic;
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
    public float MaxDistance = 10f;

    public string Hash()
    {
        return $"InstantiateModelParams({GeomFlags},{MaxDistance})";
    }
}


/**
 * Represent a loaded or generated model.
 *
 * This contains
 * - general information about the model.
 * - a hierarchy of InstanceDescs.
 */
public class Model
{
    public ModelInfo ModelInfo;
    public ModelNode RootNode;


    /**
     * Convenience method to create a model from a single InstanceDesc
     */
    public Model(InstanceDesc instanceDesc, ModelInfo modelInfo)
    {
        ModelNode mnRoot = new()
        {
            InstanceDesc = instanceDesc
        };
        RootNode = mnRoot;
        ModelInfo = modelInfo;
    }
    
    
    public Model()
    {
    }
}