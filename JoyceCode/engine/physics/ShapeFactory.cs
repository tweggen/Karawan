using System.Collections.Generic;

namespace engine.physics;

public class ShapeFactory
{
    private object _classLock = new();

    private engine.Engine _engine;

    private SortedDictionary<float, BepuPhysics.Collidables.CollidableDescription> _mapSphereCollidables = new();
    private SortedDictionary<float, BepuPhysics.Collidables.TypedIndex> _mapPshapeSphere = new();
    private SortedDictionary<float, BepuPhysics.Collidables.Sphere> _mapPbodySphere = new();
    
    public BepuPhysics.Collidables.TypedIndex GetSphereShape(float radius, out BepuPhysics.Collidables.Sphere pbodySphere)
    {
        lock (_classLock)
        {
            BepuPhysics.Collidables.TypedIndex pshapeSphere;
            if (_mapPshapeSphere.TryGetValue(radius, out pshapeSphere))
            {
                pbodySphere = _mapPbodySphere[radius];
                return pshapeSphere;
            }

            pbodySphere = new(radius); 
            lock (_engine.Simulation)
            {
                pshapeSphere = _engine.Simulation.Shapes.Add(pbodySphere);
            }

            _mapPbodySphere[radius] = pbodySphere;
            _mapPshapeSphere[radius] = pshapeSphere;
            
            return pshapeSphere;
        }
    }


    public BepuPhysics.Collidables.TypedIndex GetSphereShape(float radius)
    {
        return GetSphereShape(radius, out var _);
    }

    
    private SortedDictionary<float, BepuPhysics.Collidables.TypedIndex> _mapPshapeCylinder = new();
    private SortedDictionary<float, BepuPhysics.Collidables.Cylinder> _mapPbodyCylinder = new();
    public BepuPhysics.Collidables.TypedIndex GetCylinderShape(float radius)
    {
        lock (_classLock)
        {
            BepuPhysics.Collidables.TypedIndex pshapeCylinder;
            if (_mapPshapeCylinder.TryGetValue(radius, out pshapeCylinder))
            {
                return pshapeCylinder;
            }

            BepuPhysics.Collidables.Cylinder pbodyCylinder = new(radius, 200.0f); 
            lock (_engine.Simulation)
            {
                pshapeCylinder = _engine.Simulation.Shapes.Add(pbodyCylinder);
            }

            _mapPbodyCylinder[radius] = pbodyCylinder;
            _mapPshapeCylinder[radius] = pshapeCylinder;
            
            return pshapeCylinder;
        }
    }

    public BepuPhysics.Collidables.CollidableDescription GetSphereCollidable(float radius)
    {
        lock (_classLock)
        {
            BepuPhysics.Collidables.CollidableDescription coll;
            if (_mapSphereCollidables.TryGetValue(radius, out coll))
            {
                return coll;
            }

            coll = new BepuPhysics.Collidables.CollidableDescription(
                GetSphereShape(radius),
                0.1f);  
            _mapSphereCollidables.Add(radius, coll);
            return coll;
        }
    }

    public ShapeFactory()
    {
        _engine = I.Get<engine.Engine>();
    }
}