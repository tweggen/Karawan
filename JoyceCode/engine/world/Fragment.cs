using engine;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using DefaultEcs;
using System.Linq;
using BepuPhysics;
using BepuPhysics.Collidables;

namespace engine.world
{
    public class Fragment
    {
        static private object _lock = new();

        // TXWTODO: A fragment should decouple its look from its data structural nature.
        // TXWTODO: This should be solved by some sort of material factory.
        static private engine.joyce.Material _jMaterialGround = null;
        static private engine.joyce.Material _getGroundMaterial()
        {
            lock(_lock) {
                if( _jMaterialGround==null )
                {
                    joyce.Texture jGroundTexture = new joyce.Texture("gridlines1.png");
                    _jMaterialGround = new joyce.Material(jGroundTexture);
                    _jMaterialGround.AlbedoColor = engine.GlobalSettings.Get("debug.options.flatshading") != "true"
                        ? 0x00000000 : 0xff002222;
                }
                return _jMaterialGround;
            }
        }

        public engine.Engine Engine { get; }
        public world.Loader Loader { get; }

        private string _myKey;
        private int _id;
        public int NumericalId { get => _id; }

        public int LastIteration { get; set; }

        // private List<DefaultEcs.Entity> _listCharacters;
        // private _listStaticMolecules: List<engine.IMolecule>;

        private engine.RandomSource _rnd;

        public Vector3 Position { get; }

        public Index3 IdxFragment;

        /**
         * Our array of elevations.
         */
        private float[,] _elevations;

        private int _groundResolution;
        private int _groundNElevations;

        /**
         * The list of (static) molecules.
         */
        private List<Entity> _eStaticMolecules;

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
            in float[,] groundArray,
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
            engine.joyce.InstanceDesc jInstanceDesc = new();
            jInstanceDesc.Meshes.Add(jMeshTerrain);
            jInstanceDesc.MeshMaterials.Add(0);
            jInstanceDesc.Materials.Add(_getGroundMaterial());
            AddStaticMolecule("engine.world.ground", jInstanceDesc);
        }


        public string GetId()
        {
            return _myKey;
        }


        public void WorldFragmentUnload()
        {
            // _allEnv.pfGame.platformDestroyFragment(_platformFragment);
            // _molecules = null;
            // _listCharacters = null;
            // _allEnv = null;
        }


        /**
         * Load any ground that shall be applied to this terrain.
         * 
         * @return Int
         */
        public int WorldFragmentLoadGround()
        {
            // Re-seed to what we are.
            _rnd.clear();
            _createGround();

            return 0;
        }


        public void WorldFragmentRemove()
        {
            /*
             * Create an action of removing all entities with this fragment id.
             */
            // TXWTODO: As long we don't create after this step we're good.

            var enumDoomedEntities = Engine.GetEcsWorld().GetEntities()
                .With<engine.world.components.FragmentId>()
                .AsEnumerable();
            List<DefaultEcs.Entity> listDoomedEntities = new();
            foreach(var entity in enumDoomedEntities)
            {
                if (entity.Get<engine.world.components.FragmentId>().Id == _id)
                {
                    listDoomedEntities.Add(entity);
                }
            }
            Engine.AddDoomedEntities(listDoomedEntities);
        }


        /**
         * Actually do add the given world fragment to the game runtime.
         */
        public void WorldFragmentAdd( float newDetail )
        {
            // TXWTODO: Make all things visible that are local to us.

        }


        /**
            * Create an array capable of holding the elevation data
            * of the given resolution.
            */
        private void _createElevationArray()
        {
            var plusone = _groundNElevations+1;
            _elevations = new float[plusone, plusone];
        }




        /**
         * Add a geometry atom to this fragment.
         */
        public void AddStaticMolecule(string staticName, in engine.joyce.InstanceDesc jInstanceDesc)
        {
            AddStaticMolecule(staticName, jInstanceDesc, null);
        }


        public void AddStaticMolecule(
            string staticName,
            engine.joyce.InstanceDesc jInstanceDesc,
            IList<Func<IList<StaticHandle>, Action>> listCreatePhysics)
        {
            var worldRecord = Engine.GetEcsWorldRecord();

            /*
             * Schedule execution of entity setup in the logical thread.
             */
            Engine.QueueEntitySetupAction(staticName, (DefaultEcs.Entity entity) =>
            {
                entity.Set(new engine.joyce.components.Instance3(jInstanceDesc));
                engine.transform.components.Transform3 cTransform3 = new(
                    true, 0x00000001, new Quaternion(), Position);
                entity.Set(cTransform3);
                engine.transform.API.CreateTransform3ToParent(cTransform3, out var mat);
                entity.Set(new engine.transform.components.Transform3ToParent(cTransform3.IsVisible, cTransform3.CameraMask, mat));

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
            _eStaticMolecules = new();
            _myKey = strKey;
            _rnd = new engine.RandomSource(_myKey);
            Position = position0;
            IdxFragment = idxFragment0;

            // Create an initial elevation array that still is zeroed.
            _createElevationArray();
        }
    }
}

