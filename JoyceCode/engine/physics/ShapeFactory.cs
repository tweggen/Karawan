using System.Collections.Generic;
using BepuPhysics.Collidables;

namespace engine.physics;

public class ShapeFactory
{
    private object _classLock = new();

    private engine.Engine _engine;

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

            lock (_engine.Simulation)
            {
                pshapeSphere = new TypedIndex()
                {
                    Packed = (uint)engine.physics.actions.CreateSphereShape.Execute(_engine.PLog, _engine.Simulation,
                        radius,  out pbodySphere)
                };
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

    
    private SortedDictionary<(float,float), BepuPhysics.Collidables.TypedIndex> _mapPshapeCylinder = new();
    private SortedDictionary<(float,float), BepuPhysics.Collidables.Cylinder> _mapPbodyCylinder = new();
    public BepuPhysics.Collidables.TypedIndex GetCylinderShape(float radius, float length)
    {
        lock (_classLock)
        {
            BepuPhysics.Collidables.Cylinder pbodyCylinder;
            BepuPhysics.Collidables.TypedIndex pshapeCylinder;
            if (_mapPshapeCylinder.TryGetValue((radius,length), out pshapeCylinder))
            {
                return pshapeCylinder;
            }

            lock (_engine.Simulation)
            {
                pshapeCylinder = new TypedIndex()
                {
                    Packed = (uint)engine.physics.actions.CreateCylinderShape.Execute(_engine.PLog, _engine.Simulation,
                        radius, length, 
                        out pbodyCylinder)
                };
            }

            _mapPbodyCylinder[(radius,length)] = pbodyCylinder;
            _mapPshapeCylinder[(radius,length)] = pshapeCylinder;
            
            return pshapeCylinder;
        }
    }

    public ShapeFactory()
    {
        _engine = I.Get<engine.Engine>();
    }
}