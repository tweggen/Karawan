using System.Collections.Generic;

namespace engine.physics;

public class ShapeFactory
{
    private static object _classLock = new();

    private static SortedDictionary<float, BepuPhysics.Collidables.CollidableDescription> _mapSphereCollidables = new();
    private static SortedDictionary<float, BepuPhysics.Collidables.TypedIndex> _mapPshapeSphere = new();
    private static SortedDictionary<float, BepuPhysics.Collidables.Sphere> _mapPbodySphere = new();
    
    public static BepuPhysics.Collidables.TypedIndex GetSphereShape(float radius, in Engine engine)
    {
        lock (_classLock)
        {
            BepuPhysics.Collidables.TypedIndex pshapeSphere;
            if (_mapPshapeSphere.TryGetValue(radius, out pshapeSphere))
            {
                return pshapeSphere;
            }

            BepuPhysics.Collidables.Sphere pbodySphere = new(radius); 
            lock (engine.Simulation)
            {
                pshapeSphere = engine.Simulation.Shapes.Add(pbodySphere);
            }

            _mapPbodySphere[radius] = pbodySphere;
            _mapPshapeSphere[radius] = pshapeSphere;
            
            return pshapeSphere;
        }
    }

    private static SortedDictionary<float, BepuPhysics.Collidables.TypedIndex> _mapPshapeCylinder = new();
    private static SortedDictionary<float, BepuPhysics.Collidables.Cylinder> _mapPbodyCylinder = new();
    public static BepuPhysics.Collidables.TypedIndex GetCylinderShape(float radius, in Engine engine)
    {
        lock (_classLock)
        {
            BepuPhysics.Collidables.TypedIndex pshapeCylinder;
            if (_mapPshapeCylinder.TryGetValue(radius, out pshapeCylinder))
            {
                return pshapeCylinder;
            }

            BepuPhysics.Collidables.Cylinder pbodyCylinder = new(radius, 200.0f); 
            lock (engine.Simulation)
            {
                pshapeCylinder = engine.Simulation.Shapes.Add(pbodyCylinder);
            }

            _mapPbodyCylinder[radius] = pbodyCylinder;
            _mapPshapeCylinder[radius] = pshapeCylinder;
            
            return pshapeCylinder;
        }
    }

    public static BepuPhysics.Collidables.CollidableDescription GetSphereCollidable(float radius, in Engine engine)
    {
        lock (_classLock)
        {
            BepuPhysics.Collidables.CollidableDescription coll;
            if (_mapSphereCollidables.TryGetValue(radius, out coll))
            {
                return coll;
            }

            coll = new BepuPhysics.Collidables.CollidableDescription(
                GetSphereShape(radius, engine),
                0.1f);  
            _mapSphereCollidables.Add(radius, coll);
            return coll;
        }
    }
}