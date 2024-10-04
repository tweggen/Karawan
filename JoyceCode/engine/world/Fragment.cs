using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using BepuPhysics;
using engine.geom;
using engine.joyce;
using static engine.Logger;

namespace engine.world;

/**
 * TXWTODO: Maybe terrain generation is a bit too interweaved into this class.
 */
public class Fragment : IDisposable
{
    static private object _lo = new();

    // TXWTODO: A fragment should decouple its look from its data structural nature.

    public engine.Engine Engine { get; }
    public world.Loader Loader { get; }

    private string _myKey;
    private int _id;

    public int NumericalId
    {
        get => _id;
    }

    public DateTime LoadedAt { get; private set; }

    public int LastIteration { get; set; }

    public Vector3 Position { get; }
    public geom.AABB AABB;

    public static AABB GetAABB(in Index3 idxFragment)
    {
        return new geom.AABB(
            new Vector3(
                idxFragment.I * MetaGen.FragmentSize,
                idxFragment.J * MetaGen.FragmentSize,
                idxFragment.K * MetaGen.FragmentSize
            ), 
            MetaGen.FragmentSize
        );
    }
    
    
    public Index3 IdxFragment { get; set; }

    
    static public Index3 PosToIndex3(in Vector3 pos)
    {
        float x = pos.X + world.MetaGen.FragmentSize / 2f;
        float z = pos.Z + world.MetaGen.FragmentSize / 2f;
        x /= world.MetaGen.FragmentSize;
        z /= world.MetaGen.FragmentSize;

        return new Index3(
            (int)Math.Floor(x),
            0,
           (int) Math.Floor(z)
        );
    }


    static public Vector3 Index3ToPos(in Index3 idx)
    {
        var fs = world.MetaGen.FragmentSize;
        return new Vector3(
            idx.I * fs,
            0f,
            idx.K * fs
        );
    }
    
    /**
     * Our array of elevations.
     */
    private engine.elevation.ElevationPixel[,] _elevations;

    private readonly int _groundResolution;
    private readonly int _groundNElevations;


    public FragmentVisibility Visibility;

    private SortedDictionary<string, object> _operatorData = new();


    public T FindOperatorData<T>(string op) where T : class, new()
    {
        T? data = GetOperatorData<T>(op);
        if (null == data)
        {
            data = new T();
            lock (_lo)
            {
                _operatorData[op] = data;
            }
        }

        return data;
    }
    
    
    public T? GetOperatorData<T>(string op) where T: class
    {
        lock (_lo)
        {
            if (_operatorData.TryGetValue(op, out var data))
            {
                return data as T;
            }
            else
            {
                return null;
            }
        }
    }
    

    public void SetOperatorData<T>(string op, T data) where T: class
    {
        lock (_lo)
        {
            _operatorData[op] = data;
        }
    }
    

    public uint PosKey
    {
        get => Visibility.PosKey();
    }

    public string Key(in Index3 i3Pos) => $"fragxy-{i3Pos.I}_{i3Pos.J}_{i3Pos.K}";

    /**
     * Test, wether the given world coordinate is inside the cluster.
     */
    public bool IsInsideLocal(in Vector3 posLocal)
    {

        float fsh = MetaGen.FragmentSize / 2f;
        if (
            (posLocal.X) >= (fsh)
            || (posLocal.X) < (-fsh)
            || (posLocal.Z) >= (fsh)
            || (posLocal.Z) < (-fsh)
        )
        {
            return false;
        }
        else
        {
            return true;
        }

    }


    public bool IsInside(in Vector3 posGlobal)
    {
        var localPos = posGlobal - Position;
        return IsInsideLocal(localPos);
    }


    public bool IsInsideLocal(float x, float y)
    {
        return IsInsideLocal(new Vector3(x, 0f, y));
    }

    public bool IsInside(in Vector2 pos2Global)
    {
        var pos3 = new Vector3(pos2Global.X, 0f, pos2Global.Y);
        return IsInside(pos3);
    }


    /**
     * For performance reasons, we directly receive a ground array. Terrain operators
     * can set the array for this fragment.
     *
     * Clipping is applied so that only parts of the array are used that are
     * meaningful for this fragment.
     *
     * @param ax
     *     Left index in the array
     * @param ay
     *     Top index in the array
     * @param bx
     *     Rightmost index in the array.
     * @param by
     *     Bottom index in the array
     * @param dx
     *     left index in fragment elevation
     * @param dy
     *     top index in fragment elevation
     */
    public void SetFragmentGroundArray(
        in engine.elevation.ElevationPixel[,] groundArray,
        int groundResolution,
        int ax, int ay, int bx, int by,
        int dx, int dy)
    {
        if (groundResolution != _groundResolution)
        {
            throw new ArgumentException(
                "worldFragmentSetGroundArray(): Inconsistent groundResolution");
        }

        /*
         * Compute my maximum bottom right.
         */
        var mx = groundResolution;
        var my = groundResolution;

        /*
         * Sort the source, adjusting destination.
         */
        if (bx < ax)
        {
            var h = ax;
            ax = bx;
            bx = h;
            dx -= bx - ax;
        }

        if (by < ay)
        {
            var h = ay;
            ay = by;
            by = h;
            dy -= by - ay;
        }

        /*
         * Totally out of range?
         */
        if (ax > mx || ay > my || bx < 0 || by < 0)
        {
            /*
             * Does not intersect with this fragment.
             */
            return;
        }

        /*
         * Clip the values with my top left.
         */
        if (dx < 0)
        {
            ax = ax - dx;
            dx = 0;
        }

        if (dy < 0)
        {
            ay = ay - dy;
            dy = 0;
        }

        /*
         * Compute destination bottom right.
         */
        var ex = dx + (bx - ax);
        var ey = dy + (by - ay);

        /*
         * Clip with my bottom right.
         */
        if (ex > mx)
        {
            bx = bx - (ex - mx);
            ex = mx;
        }

        if (ey > my)
        {
            by = by - (ex - my);
            ey = my;
        }

        /*
         * Now, we can loop.
         */
        int y = 0, ymax = (ey - dy);
        while (y <= ymax)
        {
            int x = 0, xmax = (ex - dx);
            while (x <= xmax)
            {
                _elevations[dy + y, dx + x] = groundArray[ay + y, ax + x];
                ++x;
            }

            ++y;
        }
    }


    private void _createGround()
    {
        Material jMaterial = I.Get<ObjectRegistry<Material>>().Get("engine.world.fragment.materials.ground");
        Mesh jMeshTerrain = world.TerrainKnitter.BuildMolecule(_elevations, 1, jMaterial);
        var jInstanceDesc = InstanceDesc.CreateFromMatMesh(
            new MatMesh(jMaterial, jMeshTerrain),
            3000f);
        AddStaticInstance("engine.world.ground", jInstanceDesc);
    }


    public string GetId()
    {
        return _myKey;
    }


    public void Dispose()
    {
    }


    /**
     * Load any ground that shall be applied to this terrain.
     *
     * @return Int
     */
    public int LoadFragmentGround()
    {
        _createGround();

        return 0;
    }


    public void RemoveFragmentEntities()
    {
        /*
         * Create an action of removing all entities with this fragment id.
         */
        // TXWTODO: As long we don't create after this step we're good.

        var enumDoomedEntities = Engine.GetEcsWorld().GetEntities()
            .With<engine.world.components.FragmentId>()
            .AsEnumerable();
        List<DefaultEcs.Entity> listDoomedEntities = new();
        foreach (var entity in enumDoomedEntities)
        {
            if (entity.Get<engine.world.components.FragmentId>().Id == _id)
            {
                listDoomedEntities.Add(entity);
            }
        }

        Engine.AddDoomedEntities(listDoomedEntities);
    }


    /**
     * Create an array capable of holding the elevation data
     * of the given resolution.
     */
    private void _createElevationArray()
    {
        var plusone = _groundNElevations + 1;
        _elevations = new engine.elevation.ElevationPixel[plusone, plusone];
    }


    /**
     * Add a geometry atom to this fragment.
     */
    public void AddStaticInstance(string staticName, in engine.joyce.InstanceDesc jInstanceDesc)
    {
        AddStaticInstance(staticName, jInstanceDesc, null);
    }

    
    public void AddStaticInstance(uint cameraMask, string staticName, in engine.joyce.InstanceDesc jInstanceDesc)
    {
        AddStaticInstance(cameraMask, staticName, jInstanceDesc, Vector3.Zero, Quaternion.Identity, null);
    }

    
    public void AddStaticInstance(
        string staticName,
        engine.joyce.InstanceDesc jInstanceDesc,
        IList<Func<IList<StaticHandle>, Action>> listCreatePhysics)
    {
        AddStaticInstance(0x00000001, staticName, jInstanceDesc, Vector3.Zero, Quaternion.Identity, listCreatePhysics);
    }

    private int _meshesInFragment = 0;

    /**
     * Add a static mesh to this fragment.
     *
     * @param listCreatePhysics
     *     A list of functios creating the physics for these meshes, each returning a function
     *     to destroy physics.
     */
    public void AddStaticInstance(
        uint cameraMask,
        string staticName,
        engine.joyce.InstanceDesc jInstanceDesc,
        Vector3 vPosition, Quaternion qRotation,
        IList<Func<IList<StaticHandle>, Action>> listCreatePhysics)
    {
        /*
         * Schedule execution of physics setup in any worker thread,
         * then do the actual entity setup in the logical thread.
         *
         * Physics initialization does not require to be in the logical
         * thread, it just needs to be mutexed with respect to the
         * simulation.
         */
        Engine.Run(() =>
        {
            engine.physics.components.Statics cStatics = default;

            if (listCreatePhysics != null)
            {
                List<BepuPhysics.StaticHandle> listHandles = new();
                List<Action> listReleaseActions = new();

                foreach (var fCreatePhysics in listCreatePhysics)
                {
                    Action action = fCreatePhysics(listHandles);
                    listReleaseActions.Add(action);
                }

                cStatics = new engine.physics.components.Statics(listHandles, listReleaseActions);
            }

            /*
             * Schedule execution of entity setup in the logical thread.
             */
            Engine.QueueEntitySetupAction(staticName, entity =>
            {
                entity.Set(new engine.joyce.components.Instance3(jInstanceDesc));
                engine.joyce.components.Transform3 cTransform3 = new(
                    true, cameraMask, qRotation, Position + vPosition);
                entity.Set(cTransform3);
                engine.joyce.TransformApi.CreateTransform3ToParent(cTransform3, out var mat);
                entity.Set(new engine.joyce.components.Transform3ToParent(
                    cTransform3.IsVisible, cTransform3.CameraMask, mat));

                if (listCreatePhysics != null)
                {
                    entity.Set(cStatics);
                }

                /*
                 * Finally, remember the molecule to be able to remove its contents later again.
                 */
                entity.Set(new engine.world.components.FragmentId(_id));
            });
        });

        lock (_lo)
        {
            _meshesInFragment += jInstanceDesc.Meshes.Count;
        }
        // Trace($"Fragment {_myKey} now has {_meshesInFragment} static meshes.");
    }


    /**
     * Make sure the things we are supposed to load are loaded.
     */
    public void EnsureVisibility(FragmentVisibility visib)
    {
        byte visibChanges;
        byte visibNow;

        lock (_lo)
        {
            if (Visibility == visib)
            {
                return;
            }

            visibChanges = (byte)(visib.How ^ Visibility.How);
            visibNow = visib.How;
        }

        byte visibToLoad = (byte)(visibChanges & visibNow);
        if ((visibToLoad & (FragmentVisibility.Visible3dNow|FragmentVisibility.Visible2dNow)) != 0)
        {
            I.Get<engine.world.MetaGen>().ApplyFragmentOperators(this, visib);
        }

        if ((visibToLoad & FragmentVisibility.Visible2dAny) != 0)
        {
            // TXWTODO: Have this association configurable.
            builtin.map.IMapProvider mapProvider = I.Get<builtin.map.IMapProvider>();
            // TXWTODO: Remove this hard coded camera mask.
            mapProvider.FragmentMapCreateEntities(this, 0x00800000u);
        }
    }


    public Fragment(
        in engine.Engine engine0,
        in world.Loader loader,
        in FragmentVisibility visib)
    {
        Engine = engine0;
        _id = Engine.GetNextId();

        Loader = loader;
        Visibility = visib;
        Visibility.How = 0;

        // _listCharacters = null;
        _groundResolution = world.MetaGen.GroundResolution;
        _groundNElevations = _groundResolution + 1;
        IdxFragment = visib.Pos();
        _myKey = Key(IdxFragment);
        Position = IdxFragment.AsVector3() * MetaGen.FragmentSize;

        {
            Vector3 sh = new(MetaGen.FragmentSize / 2f, MetaGen.FragmentSize / 2f, MetaGen.FragmentSize / 2f);
            AABB = new geom.AABB(Position, MetaGen.FragmentSize);
        }
        LoadedAt = DateTime.Now;

        // Create an initial elevation array that still is zeroed.
        _createElevationArray();

        I.Get<ObjectRegistry<Material>>().RegisterFactory("engine.world.fragment.materials.ground",
            (name) => new Material()
            {
                Texture = I.Get<TextureCatalogue>().FindTexture("gridlines1.png"),
            });
    }

}
