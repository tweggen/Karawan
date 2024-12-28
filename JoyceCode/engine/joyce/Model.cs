using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using builtin.loader;
using engine.geom;

namespace engine.joyce
{
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
        public static int REQUIRE_ROOT_INSTANCEDESC = 0x10000;
    
        /**
         * With this flag the model builder omits creation of an
         * additional root aentity to ensure a transform component
         * can be added to the entity to have it
         * conttrolled.
         */
        public static int NO_CONTROLLABLE_ROOT = 0x20000;

        /**
         * Have the builder create physics as well
         */
        public static int BUILD_PHYSICS = 0x80000;

        /**
         * Have the physics be detectable by means of collision detection etc.
         */
        public static int PHYSICS_DETECTABLE = 0x100000;
    
        /**
         * Have the physics be tangible by means of collision detection etc.
         */
        public static int PHYSICS_TANGIBLE = 0x100000;

        /**
         * Are the physics intended to be static (as opposed to kinematic or dynamic)?
         */
        public static int PHYSICS_STATIC = 0x200000;
        
        /**
         * Shall we trigger callbacks?
         */
        public static int PHYSICS_CALLBACKS = 0x400000;
        
        /**
         * Shall we trigger callbacks?
         */
        public static int PHYSICS_OWN_CALLBACKS = 0x800000;
    
        public int GeomFlags { get; set; }= 0;
        public float MaxDistance { get; set; } = 10f;

        public string Hash()
        {
            return $"{{\"geomFlags\": {GeomFlags}, \"maxDistance\": {MaxDistance} }}";
        }
    }

    public class Joint
    {
        /**
         * The node this joint controls
         */
        public IList<ModelNode> ControlledNodes;
        public Matrix4x4 InverseBindMatrix;
    }

    public class Skin
    {
        public string Name;
        public IList<Joint> Joints;

    
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
        public ModelNode RootNode;
        public string Name; 
    
        /**
         * Convenience method to create a model from a single InstanceDesc
         */
        public Model(InstanceDesc instanceDesc)
        {
            ModelNode mnRoot = new()
            {
                Model = this,
                InstanceDesc = instanceDesc,
                Transform = new(true, 0xffff, Matrix4x4.Identity)
            };
            RootNode = mnRoot;
        }


        /**
         * Fill my model structure and my root instance desc with the
         * contents from the other model.
         */
        public void FillPlaceholderFrom(Model other)
        {
           /*
            * We will use their rootnode and their name, however use our InstanceDesc
            * as we already gave out our instanceDesc to clients.
            */
            RootNode = other.RootNode;
            Name = other.Name;
        }
 
        
        public Model()
        {
        }
    }
}