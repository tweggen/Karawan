using System;
using System.Collections.Generic;
using System.Numerics;
using BepuPhysics;
using engine.joyce;
using static engine.Logger;

namespace engine.world
{
    public class Fragment : IDisposable
    {
        static private object _lock = new();

        // TXWTODO: A fragment should decouple its look from its data structural nature.

        public engine.Engine Engine { get; }
        public world.Loader Loader { get; }

        private string _myKey;
        private int _id;
        public int NumericalId { get => _id; }
        public DateTime LoadedAt { get; private set; }
        
        public int LastIteration { get; set; }

        private builtin.tools.RandomSource _rnd;

        public Vector3 Position { get; }
        public geom.AABB AABB; 
        
        public Index3 IdxFragment;

        /**
         * Our array of elevations.
         */
        private engine.elevation.ElevationPixel[,] _elevations;

        private int _groundResolution;
        private int _groundNElevations;

        
        /**
         * Test, wether the given world coordinate is inside the cluster.
         */
        public bool IsInsideLocal( in Vector3 posLocal )
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


        public bool IsInside( in Vector3 posGlobal )
        {
            var localPos = posGlobal - Position;
            return IsInsideLocal(localPos);
        }


        public bool IsInsideLocal( float x, float y )
        {
            return IsInsideLocal(new Vector3(x, 0f, y));
        }

        public bool IsInside( in Vector2 pos2Global )
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
        public void WorldFragmentSetGroundArray(
            in engine.elevation.ElevationPixel[,] groundArray,
            int groundResolution,
            int ax, int ay, int bx, int by,
            int dx, int dy )
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
                var h = ax; ax = bx; bx = h;
                dx -= bx - ax;
            }
            if (by < ay)
            {
                var h = ay; ay = by; by = h;
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
                    _elevations[dy + y,dx + x] = groundArray[ay + y,ax + x];
                    ++x;
                }
                ++y;
            }
        }


        private void _createGround()
        {
            joyce.Mesh jMeshTerrain = world.TerrainKnitter.BuildMolecule(_elevations, 1);
            var jInstanceDesc = InstanceDesc.CreateFromMatMesh(new MatMesh(I.Get<ObjectRegistry<Material>>().Get("engine.world.fragment.materials.ground"), jMeshTerrain), 3000f);
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
        public int WorldFragmentLoadGround()
        {
            // Re-seed to what we are.
            _rnd.Clear();
            _createGround();

            return 0;
        }


        public void WorldFragmentRemove()
        {
            Engine.QueueMainThreadAction(() =>
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
            });
        }


        /**
         * Create an array capable of holding the elevation data
         * of the given resolution.
         */
        private void _createElevationArray()
        {
            var plusone = _groundNElevations+1;
            _elevations = new engine.elevation.ElevationPixel[plusone, plusone];
        }
        

        /**
         * Add a geometry atom to this fragment.
         */
        public void AddStaticInstance(string staticName, in engine.joyce.InstanceDesc jInstanceDesc)
        {
            AddStaticInstance(staticName, jInstanceDesc, null);
        }

        public void AddStaticInstance(
            string staticName,
            engine.joyce.InstanceDesc jInstanceDesc,
            IList<Func<IList<StaticHandle>, Action>> listCreatePhysics)
        {
            AddStaticInstance(staticName, jInstanceDesc, Vector3.Zero, Quaternion.Identity, listCreatePhysics);
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
            string staticName,
            engine.joyce.InstanceDesc jInstanceDesc,
            Vector3 vPosition, Quaternion qRotation,
            IList<Func<IList<StaticHandle>, Action>> listCreatePhysics)
        {
            /*
             * Schedule execution of entity setup in the logical thread.
             */
            Engine.QueueEntitySetupAction(staticName, (DefaultEcs.Entity entity) =>
            {
                entity.Set(new engine.joyce.components.Instance3(jInstanceDesc));
                engine.joyce.components.Transform3 cTransform3 = new(
                    true, 0x00000001, qRotation, Position+vPosition);
                entity.Set(cTransform3);
                engine.joyce.TransformApi.CreateTransform3ToParent(cTransform3, out var mat);
                entity.Set(new engine.joyce.components.Transform3ToParent(cTransform3.IsVisible, cTransform3.CameraMask, mat));

                if (listCreatePhysics != null)
                {
                    List<BepuPhysics.StaticHandle> listHandles = new();
                    List<Action> listReleaseActions = new();

                    foreach (var fCreatePhysics in listCreatePhysics)
                    {
                        Action action = fCreatePhysics(listHandles);
                        listReleaseActions.Add(action);
                    }
                    entity.Set(new engine.physics.components.Statics(listHandles, listReleaseActions));
                }

                /*
                 * Finally, remember the molecule to be able to remove its contents later again.
                 */
                entity.Set(new engine.world.components.FragmentId(_id));
            });

            _meshesInFragment += jInstanceDesc.Meshes.Count;
            // Trace($"Fragment {_myKey} now has {_meshesInFragment} static meshes.");
        }


        public Fragment(
            in engine.Engine engine0,
            in world.Loader loader,
            in string strKey,
            in Index3 idxFragment0,
            in Vector3 position0)
        {
            Engine = engine0;
            _id = Engine.GetNextId();
            Loader = loader;
            // _listCharacters = null;
            _groundResolution = world.MetaGen.GroundResolution;
            _groundNElevations = _groundResolution + 1;
            _myKey = strKey;
            _rnd = new builtin.tools.RandomSource(_myKey);
            Position = position0;
            {
                Vector3 sh = new(MetaGen.FragmentSize / 2f, MetaGen.FragmentSize / 2f, MetaGen.FragmentSize / 2f);
                AABB = new geom.AABB(Position, MetaGen.FragmentSize);
            }
            IdxFragment = idxFragment0;
            LoadedAt = DateTime.Now;

            // Create an initial elevation array that still is zeroed.
            _createElevationArray();

            I.Get<ObjectRegistry<Material>>().RegisterFactory("engine.world.fragment.materials.ground",
                (name) => new Material()
                {
                    Texture = new Texture("gridlines1.png"),
                    AlbedoColor = (bool) engine.Props.Get("debug.options.flatshading", false) != true
                        ? 0x00000000
                        : 0xff002222
                });
        }

    }
}

