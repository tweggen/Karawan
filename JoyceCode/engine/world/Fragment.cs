using engine;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using DefaultEcs;

namespace engine.world
{
    public class Fragment
    {
        static private object _lock = new();
        static private engine.joyce.Material _jMaterialGround = null;

        static private engine.joyce.Material _getGroundMaterial()
        {
            lock(_lock) {
                if( _jMaterialGround==null )
                {
                    joyce.Texture jGroundTexture = new joyce.Texture("assets\\gridlines1.png");
                    _jMaterialGround = new joyce.Material(jGroundTexture);
                }
                return _jMaterialGround;
            }
        }

        public engine.Engine Engine { get; }
        public world.Loader Loader { get; }

        private string _myKey;

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
        private Nullable<Entity> _eGround;

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

            _eGround = Engine.CreateEntity();
            _eGround.Value.Set<engine.joyce.components.Instance3>(
                    new engine.joyce.components.Instance3(jInstanceDesc));
            Engine.AddInstance3(
                _eGround.Value, true, 0xffffffff,
                Position,
                new Quaternion());
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

        public void WorldFragmentLoadStatic()
        {
#if false
            _allEnv = envCurrent;

            _listCharacters = new List<ICharacter>();
#endif
        }



        public void WorldFragmentRemove()
        {
            {
                DefaultEcs.Entity eGround;
                List<DefaultEcs.Entity> eStaticMolecules;
                lock (_lock)
                {
                    eGround = _eGround.Value;
                    _eGround = null;
                    eStaticMolecules = new List<DefaultEcs.Entity>(_eStaticMolecules);
                    _eStaticMolecules.Clear();
                }
                eGround.Dispose();
                foreach (var eStatic in eStaticMolecules)
                {
                    eStatic.Dispose();
                }
                eStaticMolecules.Clear();
            }
#if false
            /*
             * Remove characters
             */
            if (WorldMetaGen.TRACE_CHARACTER_MIGRATION) trace('WorldFragment.worldFragmentRemove(): Removing characters from fragment $_myKey.');
            if (_listCharacters != null)
            {
                var dismissList = new List<ICharacter>();
                for (character in _listCharacters )
                {
                    if (WorldMetaGen.TRACE_CHARACTER_MIGRATION) trace('WorldFragment.worldFragmentRemove(): Scheduling character ${character.id} for removal from fragment $_myKey.');
                    dismissList.push(character);
                }
                for (dismissedCharacter in dismissList )
                {
                    worldFragmentRemoveCharacter(dismissedCharacter);
                }
                _listCharacters = new List<ICharacter>();
            }

            /*
             * Remove static molecules from fragment
             */
            if (_molecules != null)
            {
                var dismissList = new List<engine.IMolecule>();
                for (molecule in _molecules )
                {
                    if (WorldMetaGen.TRACE_CHARACTER_MIGRATION) trace('WorldFragment.worldFragmentRemove(): Scheduling molecule for removal from fragment $_myKey.');
                    dismissList.push(molecule);
                }
                for (dismissedMolecule in dismissList )
                {
                    dismissedMolecule.moleculeSetFragment(null);
                    _allEnv.pfGame.platformFragmentRemoveMolecule(_platformFragment, dismissedMolecule);
                }
                _molecules = new Array<engine.IMolecule>();
            }

            /*
             * Remove ground
             */
            if (_molTerrain != null)
            {
                _molTerrain.moleculeSetFragment(null);
                _allEnv.pfGame.platformFragmentRemoveMolecule(_platformFragment, _molTerrain);
            }

            /*
             * Remove 3d data from engine.
             */
            trace('WorldFragment.worldFragmentRemove(): Removing platform fragment.');
            _allEnv.pfGame.platformRemoveFragment(_platformFragment);

            return 0;
#endif
        }


        /**
         * Actually do add the given world fragment to the game runtime.
         */
        public void WorldFragmentAdd( float newDetail )
        {
            // TXWTODO: Make all things visible that are local to us.

#if false
            /*
             * Pass on 3d stuff to heart (first for visibility)
             */
            try
            {
                _allEnv.pfGame.platformAddFragment(_platformFragment, new geom.Vector3D(x, y, z));
            }
            catch (unknown: Dynamic ) {
                trace("WorldLoader.worldLoaderProvideFragments(): Unknown exception calling platformAddFragment(): "
                    + Std.string(unknown) + "\n"
                    + haxe.CallStack.toString(haxe.CallStack.callStack()));
            }

            var physics = allEnv.worldLoader.getPhysics();

            /*
             * First add all molecules to the engine in one bulk operation.
             */
            for (molecule in _molecules )
            {
                try
                {
                    molecule.moleculeSetFragment(this);
                    _allEnv.pfGame.platformFragmentAddMolecule(_platformFragment, molecule);
                }
                catch (unknown: Dynamic ) {
                trace("WorldLoader.worldLoaderProvideFragments(): Unknown exception calling platformFragmentAddMolecule(): "
                    + Std.string(unknown) + "\n"
                    + haxe.CallStack.toString(haxe.CallStack.callStack()));
            }
            }

            /*
             * Trigger post pro for the static molecules (naking textures etc).
             */
            for (molecule in _molecules )
            {
                try
                {
                    _allEnv.pfGame.platformFragmentSetMoleculePositionVisible(
                        _platformFragment, molecule, null, null, true);
                }
                catch (unknown: Dynamic ) {
                trace("WorldLoader.worldLoaderProvideFragments(): Unknown exception calling platformFragmentSetMoleculePositionVisible(): "
                    + Std.string(unknown) + "\n"
                    + haxe.CallStack.toString(haxe.CallStack.callStack()));
            }
        }

        return 0;
#endif
            }

#if false
    private function _characterIsVisible(character: ICharacter ): Bool
        {
            var shallBeVisible: Bool = false;
            var pos = character.characterGetWorldPos();
            var dist: Float =
                (pos.x - WorldMetaGen.mex) * (pos.x - WorldMetaGen.mex)
                + (pos.y - WorldMetaGen.mey) * (pos.y - WorldMetaGen.mey)
                + (pos.z - WorldMetaGen.mez) * (pos.z - WorldMetaGen.mez);

            var maxvisib = character.characterGetMaxVisibility();
            if (dist < maxvisib * maxvisib)
            {
                shallBeVisible = true;
            }
            return shallBeVisible;
        }


        private function _characterIsAudible(character: ICharacter ): Bool
        {
            var shallBeAudible: Bool = false;
            var pos = character.characterGetWorldPos();
            var dist: Float =
                (pos.x - WorldMetaGen.mex) * (pos.x - WorldMetaGen.mex)
                + (pos.y - WorldMetaGen.mey) * (pos.y - WorldMetaGen.mey)
                + (pos.z - WorldMetaGen.mez) * (pos.z - WorldMetaGen.mez);

            var maxaudib = character.characterGetMaxAudibility();
            if (dist < maxaudib * maxaudib)
            {
                shallBeAudible = true;
            }
            return shallBeAudible;
        }
#endif

        public void WorldFragmentBehave()
        {
#if false
        var dismissList: List < ICharacter > = new List<ICharacter>();

            /*
             * First care about all characters.
             */
            if (null == _listCharacters)
            {
                throw "WorldFragment.worldFragmentBehave(): _listCharacters is null";
            }

            // trace( 'Behave called for fragment $_myKey');
            for (character in _listCharacters )
            {

                try
                {
                    character.characterBehave();
                }
                catch (unknown: Dynamic ) {
                trace("WorldLoader.worldLoaderProvideFragments(): Unknown exception calling characterBehave(): "
                    + Std.string(unknown) + "\n"
                    + haxe.CallStack.toString(haxe.CallStack.callStack()));
            }

            var localCharacter: FragmentCharacterData =
                cast(WorldMetaGen.cat.catGetEntity(character.id + '-frag-local-' + _myKey), FragmentCharacterData);

            if (localCharacter == null)
            {
                trace('WorldFragment.worldFragmentBehave(): character ${character.id}: No local character data found.');
                continue;
            }

            // TXWTODO: Ask for removeMe
            var pos = character.characterGetWorldPos();
            var rot = character.characterGetOrientation();
            var dir = character.characterGetDirection();

            /*
             * We only display charactesr reasonable close to the player.
             * Objects are restricted to the visibility trunk by the engine.
             * The same goes for audibility;
             */
            var shallBeVisible: Bool = _characterIsVisible(character);
            var shallBeAudible: Bool = _characterIsAudible(character);

            /*
             * Now check visibility and audibility.
             */

            /* 
             *  Check visibility first.
             */
            var molecule = character.characterGetMolecule();
            if (molecule != null)
            {
                if (null == molecule.moleculeGetFragment())
                {
                    _onCharacterMoleculeAvailable(character, molecule);
                }
                // trace( 'WorldFragment.worldFragmentBehave(): Calling ...PositionVisible( visble==${localCharacter.wasVisible})' );
                _allEnv.pfGame.platformFragmentSetMoleculePositionVisible(
                    _platformFragment, molecule,
                    new geom.Vector3D(pos.x - x, pos.y - y, pos.z - z),
                    new geom.Vector3D(rot.x, rot.y, rot.z),
                    shallBeVisible);
            }
            else
            {
                // trace( 'WorldFragment.worldFragmentBehave(): character ${character.id}: no molecule available yet.' );
            }

            /*
             * Consider sound.
             * If the character now is visible, ensure we have a sound source.
             * Setup velocity and position.
             * If it has not been playing, start playback.
             */
            if (shallBeAudible)
            {

                /*
                 * If there was no audio source yet, create it.
                 */
                if (null == localCharacter.soundSource)
                {
                    // trace( 'WorldFragment.worldFragmentBehave(): character ${character.id}: Creating audio source.' );
                    localCharacter.soundSource = character.characterCreateAudioSource();
                }

                /*
                 * If we have a sound source, update it.
                 */
                if (null != localCharacter.soundSource)
                {
                    var soundX = pos.x - WorldMetaGen.mex;
                    var soundY = pos.y - WorldMetaGen.mey;
                    var soundZ = pos.z - WorldMetaGen.mez;
#if false
                                trace( 'character ${character.id} '
                                +'$soundX, '
                                +'$soundY, '
                                +'$soundZ} v '
                                +'${dir.x-WorldMetaGen.vmex}, '
                                +'${dir.y-WorldMetaGen.vmey}, '
                                +'${dir.z-WorldMetaGen.vmez} '
                                );
#endif
                            localCharacter.soundSource.audioSourceSetPosition( 
                                new geom.Vector3D( soundX, soundY, soundZ ) );
                            localCharacter.soundSource.audioSourceSetVelocity( 
                                new geom.Vector3D(
                                    dir.x-WorldMetaGen.vmex,
                                    dir.y-WorldMetaGen.vmey,
                                    dir.z-WorldMetaGen.vmez ) );

                    
                            if( !localCharacter.soundIsPlaying ) {
                                // trace( 'WorldFragment.worldFragmentBehave(): character ${character.id}: Starting playback.' );
                                localCharacter.soundSource.audioSourcePlay();
                                localCharacter.soundIsPlaying = true;
                            }
                        } else /* null sound source */ {
                            trace('WorldFragment.worldFragmentBehave(): Error creating sound source.');
                        }
                    } else /* shallBeAudible */ {
                        if( localCharacter.soundSource != null ) {
                            if( localCharacter.soundIsPlaying ) {
                                // trace( 'WorldFragment.worldFragmentBehave(): character ${character.id}: Stopping playback.' );
                                localCharacter.soundSource.audioSourceStop();
                                localCharacter.soundIsPlaying = false;

                                var dist: Float = 
                                    (pos.x-WorldMetaGen.mex)*(pos.x-WorldMetaGen.mex)
                                    +(pos.y-WorldMetaGen.mey)*(pos.y-WorldMetaGen.mey)
                                    +(pos.z-WorldMetaGen.mez)*(pos.z-WorldMetaGen.mez);
                        
                            }
                        }
                    }

                    /*
                     * Finally, update visibility and audibility for local character data.
                     */
                    localCharacter.wasVisible = shallBeVisible;
                    localCharacter.wasAudible = shallBeAudible;


                    /*
                     * Now we got the position check if the character still is in my
                     * fragment. If not, remove it from my fragment and ask the world
                     * loader to redistribute the character.
                     */
                    if( !isInside( pos.x, pos.z ) ) {
#if false
                        if( pos.x<-200. && pos.x>-220. && pos.z < -280.0 && pos.z > -320.0 ) {
                            trace( 'My border character ${character.id}.' );
                        }
#endif
                        if( WorldMetaGen.TRACE_CHARACTER_MIGRATION ) trace( 'WorldFragment.worldFragmentBehave(): character ${character.id}: not inside, dismissing character at ${pos.x} ${pos.z}.' );
                        dismissList.push( character );
                        continue;
                    }
                }


                /*
                 * Now care about the dismissed characters. First remove them from 
                 * this fragment, then ask the world manager to care about them.
                 */
                for( dismissedCharacter in dismissList ) {
                    /*
                     * Read the local character data to pass it on to the next owner.
                     */
                    var localCharacter: FragmentCharacterData = 
                        cast(WorldMetaGen.cat.catGetEntity( dismissedCharacter.id+'-frag-local-'+_myKey ), FragmentCharacterData);
                    if( isBuggyCharacter( dismissedCharacter )) {
                        trace( 'Buggy character.');
                    }
                    worldFragmentRemoveCharacter( dismissedCharacter );
                    allEnv.worldLoader.worldLoaderSortinCharacter( dismissedCharacter, localCharacter );
                    // Warning: localCharacter may be invalid here.
                }

                return 0;
#endif
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


#if false
        /**
         * This function is called as soon the platform is available.
         * Either, at the time of adding it or later, when it is available
         * first during the behave call.
         */
        private function _onCharacterMoleculeAvailable(
                character: ICharacter,
                molecule: engine.IMolecule
            ): Void {
                molecule.moleculeSetFragment( this );
                _allEnv.pfGame.platformFragmentAddMolecule( _platformFragment, molecule );

            }

#endif

#if false
        /**
         * Add a character to the fragment. It may happen that a character's molecule is
         * not available at the time of adding. It then will be added later.
         */
        public function worldFragmentAddCharacter( 
                character: ICharacter,
                localCharacter: FragmentCharacterData
            ): Void {
                if( WorldMetaGen.TRACE_CHARACTER_MIGRATION ) trace( 'Fragment: ${_myKey} adding character ${character.id}');

                if( null==localCharacter) {
                    trace( 'WorldFragment.worldFragmentAddCharacter(): fragment ${_myKey} chracter ${character.id}: No localCharacter.' );
                    throw 'WorldFragment.worldFragmentAddCharacter(): fragment ${_myKey} chracter ${character.id}: No localCharacter.';
                }

                _listCharacters.add( character );
                WorldMetaGen.cat.catAddGlobalEntity( character.id+'-frag-local-'+_myKey, localCharacter );

                // First add the character.

                {
                    // TXWTODO: Ask for removeMe
                    var pos = character.characterGetWorldPos();
                    var rot = character.characterGetOrientation();

                    var molecule = character.characterGetMolecule();
                    if( molecule != null ) {
                        var moleculeFragment = molecule.moleculeGetFragment();
                        if( null != moleculeFragment ) {
                            trace( 'WorldFragment.worldFragmentAddCharacter(): Warning: Character was added to a fragment before.' );
                            if( moleculeFragment != this ) {
                                trace( 'WorldFragment.worldFragmentAddCharacter(): Warning: Character was added to a different fragment.' );
                            }
                        } else {
                            _onCharacterMoleculeAvailable( character, molecule );
                        }
                
                        // TXWTODO: Why hide it here?
                        // Wouldn't we want to apply the usual visibility checks?
                        // Or just use the last visibility information?
                        // trace( 'WorldFragment.worldFragmentAddCharacter(): Calling ...PositionVisible( visble==${localCharacter.wasVisible})' );
                        _allEnv.pfGame.platformFragmentSetMoleculePositionVisible(
                            _platformFragment,
                            molecule,
                            new geom.Vector3D( pos.x-x, pos.y-y, pos.z-z ),
                            new geom.Vector3D( rot.x, rot.y, rot.z ),
                            localCharacter.wasVisible );
                    } else {

                    }
                }
            }
#endif

#if false
        public function worldFragmentRemoveCharacter( character: ICharacter ): Void {

                if( WorldMetaGen.TRACE_CHARACTER_MIGRATION ) trace( 'Fragment: ${_myKey} removing character ${character.id}');

                if( false ==_listCharacters.remove( character ) ) {
                    throw 'WorldFragment: Trying to remove unknown character from fragment.';
                }

                var molecule = character.characterGetMolecule();
                if( null!=molecule) {
                    try {
                        molecule.moleculeSetFragment( null );
                        _allEnv.pfGame.platformFragmentRemoveMolecule( _platformFragment, molecule );
                    } catch( unknown: Dynamic ) {
                        trace( "WorldLoader.worldFragmentRemoveCharacter(): Unknown exception calling platformFragmentRemoveMolecule(): "
                            + Std.string( unknown )+ "\n"
                            + haxe.CallStack.toString( haxe.CallStack.callStack() ) );
                    }
                }
                var localCharacter: FragmentCharacterData = 
                    cast(WorldMetaGen.cat.catGetEntity( character.id+'-frag-local-'+_myKey ), FragmentCharacterData);
                WorldMetaGen.cat.catRemoveEntity( character.id+'-frag-local-'+_myKey );

                /*
                 * we must not dismiss the character here, we might need it in the next fragment.
                 */
#if false
                if( localCharacter != null ) {
                    localCharacter.dismiss();
                } else {
                    throw "WorldFragment.worldFragmentRemoveCharacter(): No localCharacter found to remove.";
                }
#endif
            }
#endif


        /**
         * Add a material factory to this fragment.
         */
            public void AddMaterialFactory( string keyMaterial, Action<engine.joyce.Material> factoryMaterial )
            {
                // world.MetaGen.cat.catGetSingleton( materialKey, factory );
            }


        /**
         * Add a geometry atom to this fragment.
         */
        public void AddStaticMolecule(in engine.joyce.InstanceDesc jInstanceDesc)
        {
            AddStaticMolecule(jInstanceDesc, null, null);
        }


        public void AddStaticMolecule(
            in engine.joyce.InstanceDesc jInstanceDesc,
            in IList<BepuPhysics.StaticDescription> listStaticDescriptions,
            in IList<BepuPhysics.Collidables.TypedIndex> listShapes)
        {
            /**
                * We create an entity for this particular mesh.
                * This entity is child to our fragment's entity.
                */
            Entity entity = Engine.CreateEntity();
            entity.Set<engine.joyce.components.Instance3>(
                new engine.joyce.components.Instance3(jInstanceDesc));
            Engine.AddInstance3(
                entity, true, 0xffffffff,
                Position,
                new Quaternion());
            if( listStaticDescriptions!=null 
                || listShapes !=null)
            {
                List<BepuPhysics.StaticHandle> handles = null;
                List<BepuPhysics.Collidables.TypedIndex> shapes = null;
                // TXWTODO: We assume that the shapes already are added to the engine at this point.
                if (listStaticDescriptions != null)
                {
                    handles = new();
                    lock (Engine.Simulation)
                    {
                        foreach (var staticDescription in listStaticDescriptions)
                        {
                            handles.Add(Engine.Simulation.Statics.Add(staticDescription));
                        }
                    }
                }
                entity.Set(new engine.physics.components.Statics(handles, shapes));
            }

            /*
             * Remember the molecule to be able to remove its contents later again.
             */
            _eStaticMolecules.Add(entity);

            /*
             * As soon we show this fragment, we make all the static molecules visible.
             * Only then all the actual resources will be created by the engine.
             */
        }

        public Fragment(
            in engine.Engine engine0,
            in world.Loader loader,
            in string strKey,
            in Index3 idxFragment0,
            in Vector3 position0)
        {
            Engine = engine0;
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

