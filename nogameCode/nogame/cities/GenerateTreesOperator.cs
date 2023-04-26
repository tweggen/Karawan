using System;
using System.Collections.Generic;
using System.Numerics;

namespace nogame.cities;

public class GenerateTreesOperator : engine.world.IFragmentOperator 
{
    
    private void trace(string message)
    {
        Console.WriteLine(message);
    }

    static private object _lock = new();

    static private engine.joyce.Material _jMaterialTrees = null;
    
    static private engine.joyce.Material _getTreesMaterial()
    {
        lock (_lock)
        {
            if (_jMaterialTrees == null)
            {
                _jMaterialTrees = new engine.joyce.Material(); 
                _jMaterialTrees.AlbedoColor = 0x44ff4444;
                // _jMaterialTrees.Texture = new engine.joyce.Texture("buildingdiffuse.png");
            }
            return _jMaterialTrees;
        }
    }
    
    private engine.world.ClusterDesc _clusterDesc;
    private engine.RandomSource _rnd;
    private string _myKey;


    public string FragmentOperatorGetPath()
    {
        return $"8001/GenerateTreesOperator/{_myKey}/";
    }


    private builtin.tools.Lindenmayer.System _createTree1System() 
    { 
        using Part = builtin.tools.Lindenmayer.Part;
        return new builtin.tools.Lindenmayer.System( new builtin.tools.Lindenmayer.State( 
            /*
             * Initial seed: One 10 up, 1m radius.
             */
            [
                /*
                 * Straight up.
                 */
                new Part( "rotate(d,x,y,z)",
                    ["d"] = 90f,["x"] = 0f, "y"=>0., "z"=>1.] ),
                /*
                 * Random orientation
                 */
                new LPart( "rotate(d,x,y,z)",
                    ["d" => _rnd.getFloat()*359., "x"=>1., "y"=>0., "z"=>0.] ),
                new LPart( "stem(r,l)",
                    ["r"=>0.10, "l"=>(1.+2.*_rnd.getFloat())] )
            ] ),
            /*
             * Transformmation: Split and grow the main tree, add too branches left
             * and right.
             */            
            [
                new LRule("stem(r,l)", 1.0, 
                    (p: LParams) -> (p["r"] > 0.02 && p["l"] > 0.1),
                    (p: LParams) -> [
                        new LPart( "stem(r,l)",
                            ["r" => p["r"]*1.05, "l" => p["l"]*0.8]
                        ),
                        new LPart( "push()", null ),
                            new LPart( "rotate(d,x,y,z)", 
                                ["d" => 30.+_rnd.getFloat()*30., "x" => 0., "y" => 0., "z" => 1.] ),
                            new LPart( "stem(r,l)",
                                ["r" => p["r"]*0.6, "l" => p["l"]*0.8] ),
                        new LPart( "pop()", null ),
                        new LPart( "push()", null ),
                            new LPart( "rotate(d,x,y,z)", 
                                ["d" => -30.+_rnd.getFloat()*30., "x" => 0., "y" => 0., "z" => 1.] ),
                            new LPart( "stem(r,l)",
                                ["r" => p["r"]*0.6, "l" => p["l"]*0.8] ),
                        new LPart( "pop()", null ),
                        new LPart( "rotate(d,x,y,z)",
                            ["d" => 90.+_rnd.getFloat()*20., "x"=>1., "y"=>0., "z"=>0.] ),
                        new LPart( "stem(r,l)",
                            ["r" => p["r"]*0.8, "l" => p["l"]*0.5] )
                    ]
                )
            ],
            /*
             * Macros: Break down the specific operation "stem" to a standard
             * turtle operation.
             */
            [
                new LRule("stem(r,l)", 1.0, null,
                    (p: LParams) -> [
                        new LPart( "fillrgb(r,g,b)",
                            ["r" => 0.2, "g" => 0.7, "b" => 0.1] ),
                        new LPart( "cyl(r,l)",
                            ["r" => p["r"], "l" => p["l"]] )
                    ]
                )
            ]
        );

    }


    private function createTree2System(): LSystem {

        return new LSystem( new LState( 
            /*
             * Initial seed: One 10 up, 1m radius.
             */
            [
                /*
                 * Straight up.
                 */
                new LPart( "rotate(d,x,y,z)",
                    ["d"=>90.,"x"=>0., "y"=>0., "z"=>1.] ),
                /*
                 * Random orientation
                 */
                new LPart( "rotate(d,x,y,z)",
                    ["d" => _rnd.getFloat()*359., "x"=>1., "y"=>0., "z"=>0.] ),
                new LPart( "stem(r,l)",
                    ["r"=>0.1, "l"=>(1.+2.*_rnd.getFloat())] )
            ] ),
            /*
             * Transformmation: Split and grow the main tree, add too branches left
             * and right.
             */            
            [
                new LRule("stem(r,l)", 1.0, 
                    (p: LParams) -> (p["r"] > 0.02 && p["l"] > 0.1),
                    (p: LParams) -> [
                        new LPart( "stem(r,l)",
                            ["r" => p["r"]*1.05, "l" => p["l"]*0.8]
                        ),
                        new LPart( "push()", null ),
                            new LPart( "rotate(d,x,y,z)", 
                                ["d" => 30.+_rnd.getFloat()*30., "x" => 0., "y" => 0., "z" => 1.] ),
                            new LPart( "stem(r,l)",
                                ["r" => p["r"]*0.6, "l" => p["l"]*0.8] ),
                        new LPart( "pop()", null ),
                        new LPart( "push()", null ),
                            new LPart( "rotate(d,x,y,z)", 
                                ["d" => -30.+_rnd.getFloat()*30., "x" => 0., "y" => 0., "z" => 1.] ),
                            new LPart( "stem(r,l)",
                                ["r" => p["r"]*0.6, "l" => p["l"]*0.8] ),
                        new LPart( "pop()", null ),
                        new LPart( "rotate(d,x,y,z)",
                            ["d" => 90.+_rnd.getFloat()*20., "x"=>1., "y"=>0., "z"=>0.] )
                    ]
                )
            ],
            /*
             * Macros: Break down the specific operation "stem" to a standard
             * turtle operation.
             */
            [
                new LRule("stem(r,l)", 1.0, 
                    (p: LParams) -> (p["r"] > 0.06),
                    (p: LParams) -> [
                        new LPart( "fillrgb(r,g,b)",
                            ["r" => 0.4, "g" => 0.2, "b" => 0.1] ),
                        new LPart( "cyl(r,l)",
                            ["r" => p["r"], "l" => p["l"]] )
                    ]
                ),
                new LRule("stem(r,l)", 1.0, 
                    (p: LParams) -> (p["r"] <= 0.06 && p["r"] > 0.04),
                    (p: LParams) -> [
                        new LPart( "fillrgb(r,g,b)",
                            ["r" => 0.3, "g" => 0.5, "b" => 0.1] ),
                        new LPart( "flat(r,l)",
                            ["r" => p["r"], "l" => p["l"]] )
                    ]
                ),
                new LRule("stem(r,l)", 1.0, 
                    (p: LParams) -> (p["r"] <= 0.04),
                    (p: LParams) -> [
                        new LPart( "fillrgb(r,g,b)",
                            ["r" => 0.2, "g" => 0.4, "b" => 0.8] ),
                        new LPart( "flat(r,l)",
                            ["r" => p["r"], "l" => p["l"]] )
                    ]
                ),
            ]
        );

    }


    private function createLInstance(): LInstance {
        var whichtree = _rnd.getFloat();

        var lSystem: LSystem = null;
        if( whichtree<0.5 ) {
            lSystem = createTree1System();
        } else {
            lSystem = createTree2System();
        }
        // TXWTODO: Create some sort of function encapsulating this?
        var lGenerator = new LGenerator( lSystem );
        var lInstance = lGenerator.instantiate();
        var prevInstance = lInstance;
        for( i in 0...Std.int(_rnd.getFloat()*2.5+1) ) {
            var nextInstance = lGenerator.iterate( prevInstance );
            if( null==nextInstance ) {
                break;
            }
            prevInstance = nextInstance;
        }
        return lGenerator.finalize( prevInstance );
    }


    public function fragmentOperatorApply(
        allEnv: AllEnv,
        worldFragment: WorldFragment
    ) : Void {
        var cx:Float = _clusterDesc.x - worldFragment.x;
        var cz:Float = _clusterDesc.z - worldFragment.z;

        var fsh: Float = WorldMetaGen.fragmentSize / 2.0;            

        /*
         * We don't apply the operator if the fragment completely is
         * outside our boundary box (the cluster)
         */
        {
            var csh: Float = _clusterDesc.size / 2.0;
            if (
                (cx-csh)>(fsh)
                || (cx+csh)<(-fsh)
                || (cz-csh)>(fsh)
                || (cz+csh)<(-fsh)
            ) {
                return;
            }
        }

        // trace( 'GenerateHousesOperator(): cluster "${_clusterDesc.name}" (${_clusterDesc.id}) in range');
        _rnd.clear();

        /*
         * Iterate through all quarters in the clusters and generate lots and houses.
         */
        var quarterStore = _clusterDesc.quarterStore();

        var atomsMap = new Map<String, engine.IGeomAtom>();

        for( quarter in quarterStore.getQuarters() ) {
            if( quarter.isInvalid() ) {
                trace( 'GenerateHousesOperator.fragmentOperatorApply(): Skipping invalid quarter.' );
                continue;
            }

            var xmiddle = 0.0;
            var ymiddle = 0.0;
            var n = 0;
            var delims = quarter.getDelims();
            for( delim in delims ) {
                xmiddle += delim.streetPoint.pos.x;
                ymiddle += delim.streetPoint.pos.y;
                ++n;
            }
            if( 3>n ) {
                continue;
            }
            xmiddle /= n;
            ymiddle /= n;

            /*
             * Compute some properties of this quarter.
             * - is it convex?
             * - what is it extend?
             * - what is the largest side?
             */
            for( estate in quarter.getEstates() ) {

                /*
                 * Only consider this estate, if the center coordinate 
                 * is within this fragment.
                 */
                var center = estate.getCenter();
                center.x += cx;
                center.z += cz;
                if(!worldFragment.isInsideLocal( center.x, center.z )) {
                    continue;
                }
                
                // TXWTODO: The 2.15 is copied from GenerateClusterQuartersOperator
                var inFragmentY = _clusterDesc.averageHeight + 2.15;

                /*
                 * Ready to add a tree if there is no house.
                 */ 
                var buildings = estate.getBuildings();
                if( buildings.length>0 ) {
                    continue;
                }

                /*
                 * But don't use every estate, just some.
                 */
                if( _rnd.getFloat() > 0.7 ) {

                } else {
                    continue;
                }

                /*
                 * OK, let's generate a number of trees at random positions within this estate.
                 * 
                 * There's a couple of different techniques: 
                 * - one in the center
                 * - some grid of trees
                 * - random placement
                 * - along the edges.
                 * 
                 * Base the decision on the quarter's size.
                 */
                var poly = estate.getPoly();      
                if( 0==poly.length ) {
                    continue;
                }
                var area = estate.getArea();
                var areaPerTree = 1.6;
                if( area<areaPerTree ) {
                    /*
                     * If the area of the estate is less than 4m2, we just plant a 
                     * single tree.
                     */
                    var instance = createLInstance();
                    var alpha = new engine.LAlphaInterpreter( instance );
                    var treePos = new geom.Vector3D( center.x, inFragmentY, center.z );
                    alpha.run( worldFragment, treePos, atomsMap );
//                    trace( 'GenerateTreesOperator.fragmentOperatorApply(): Adding single tree at $treePos.' );
                } else if( area >= (2.*areaPerTree) ) {
                    /*
                     * Just one other algo: random.
                     * We guess that one tree takes up 1x1m, i.e. 1m2
                     */

                    var nTrees = Std.int((area+1.)/areaPerTree);
                    if( nTrees > 80 ) nTrees = 80;
                    var nPlanted = 0;
                    var iterations = 0;
                    var extent = estate.getMaxExtent();
                    var min = estate.getMin();
                    while(nPlanted < nTrees && iterations < 4*nTrees) {
                        iterations++;
                        var treePos = new geom.Vector3D( 
                            min.x + _rnd.getFloat()*extent.x, 
                            inFragmentY, 
                            min.z + _rnd.getFloat()*extent.z 
                        );
                        // TXWTODO: Check, if it is inside.
                        if( !estate.isInside( treePos ) ) continue;
                        treePos.x += cx; treePos.z += cz;
                        var instance = createLInstance();
                        var alpha = new engine.LAlphaInterpreter( instance );
                        alpha.run( worldFragment, treePos, atomsMap );
                        nPlanted++;
                        //trace( 'GenerateTreesOperator.fragmentOperatorApply(): Adding tree at $treePos.' );
                    }
                }
            }

        }


        var mol = new engine.SimpleMolecule( null );
        var isEmpty = true;
        for( atom in atomsMap ) {
            mol.moleculeAddGeomAtom( atom );
            isEmpty = false;
        }
        if( !isEmpty ) {
            worldFragment.addStaticMolecule( mol );
        }

    }
    

    public function new (
        clusterDesc: ClusterDesc,
        strKey: String
    ) {
        _clusterDesc = clusterDesc;
        _myKey = strKey;
        _rnd = new engine.RandomSource(strKey);

        /**
         * Create a material for the trees.
         */
        WorldMetaGen.cat.catGetSingleton( "LAlphaInterpreter._matAlpha", function () {
            var matAlpha = new engine.Material("");
            matAlpha.ambientColor = 0x448822;
            matAlpha.specular = 0.4;
            matAlpha.ambient = 0.5;
            return matAlpha;
        });
    }
}

}