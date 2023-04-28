using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using builtin.tools.Lindenmayer;
using static engine.Logger;

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
                _jMaterialTrees.AlbedoColor = 0xff448822;
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
        
        return new builtin.tools.Lindenmayer.System( new State( new List<Part>
            /*
             * Initial seed: One 10 up, 1m radius.
             */
            {
                /*
                 * Straight up.
                 */
                new Part( "rotate(d,x,y,z)", new SortedDictionary<string, float> {
                    ["d"] = 90f,["x"] = 0f, ["y"] = 0f, ["z"] =1f } ),
                /*
                 * Random orientation
                 */
                new Part( "rotate(d,x,y,z)", new SortedDictionary<string, float> {
                    ["d"] = _rnd.getFloat()*359f, ["x"] = 1f, ["y"] = 0f, ["z"] = 0f } ),
                new Part( "stem(r,l)", new SortedDictionary<string, float> {
                    ["r"] = 0.10f, ["l"] = (1f+2f*_rnd.getFloat()) } )
            } ),
            /*
             * Transformmation: Split and grow the main tree, add too branches left
             * and right.
             */            
            new List<Rule> {
                new Rule("stem(r,l)", 1.0f, 
                    (Params p) => (p["r"] > 0.02 && p["l"] > 0.1),
                    (Params p) => new List<Part> {
                        new Part( "stem(r,l)", new SortedDictionary<string, float> {
                            ["r"] = p["r"]*1.05f, ["l"] = p["l"]*0.8f } ),
                        new Part( "push()", null ),
                            new Part( "rotate(d,x,y,z)", new SortedDictionary<string, float> {
                                ["d"] = 30f+_rnd.getFloat()*30f, ["x"] = 0f, ["y"] = 0f, ["z"] = 1f } ),
                            new Part( "stem(r,l)", new SortedDictionary<string, float> {
                                ["r"] = p["r"]*0.6f, ["l"] = p["l"]*0.8f } ),
                        new Part( "pop()", null ),
                        new Part( "push()", null ),
                            new Part( "rotate(d,x,y,z)", new SortedDictionary<string, float> {
                                ["d"] = -30f+_rnd.getFloat()*30f, ["x"] = 0f, ["y"] = 0f, ["z"] = 1f } ),
                            new Part( "stem(r,l)", new SortedDictionary<string, float> {
                                ["r"] = p["r"]*0.6f, ["l"] = p["l"]*0.8f }),
                        new Part( "pop()", null ),
                        new Part( "rotate(d,x,y,z)", new SortedDictionary<string, float> {
                            ["d"] = 90f+_rnd.getFloat()*20f, ["x"] = 1f, ["y"] = 0f, ["z"] = 0f } ),
                        new Part( "stem(r,l)", new SortedDictionary<string, float> {
                            ["r"] = p["r"]*0.8f, ["l"] = p["l"]*0.5f } )
                    }
                )
            },
            /*
             * Macros: Break down the specific operation "stem" to a standard
             * turtle operation.
             */
            new List<Rule> {
                new Rule("stem(r,l)", 1.0f, null,
                    (Params p) => new List<Part> {
                        new Part( "fillrgb(r,g,b)", new SortedDictionary<string, float> {
                            ["r"] = 0.2f, ["g"] = 0.7f, ["b"] = 0.1f } ),
                        new Part( "cyl(r,l)", new SortedDictionary<string, float> {
                            ["r"] = p["r"], ["l"] = p["l"] } )
                    }
                )
            }
        );

    }


    private builtin.tools.Lindenmayer.System _createTree2System()
    {
        return new builtin.tools.Lindenmayer.System( new State( new List<Part>  
            /*
             * Initial seed: One 10 up, 1m radius.
             */
            {
                /*
                 * Straight up.
                 */
                new Part( "rotate(d,x,y,z)", new SortedDictionary<string, float> {
                    ["d"] = 90f, ["x"] = 0f, ["y"] = 0f, ["z"] = 1f } ),
                /*
                 * Random orientation
                 */
                new Part( "rotate(d,x,y,z)", new SortedDictionary<string, float> {
                    ["d"] = _rnd.getFloat()*359f, ["x"] = 1f, ["y"] = 0f, ["z"] = 0f } ),
                new Part( "stem(r,l)", new SortedDictionary<string, float> {
                    ["r"] = 0.1f, ["l"] = (1f+2f*_rnd.getFloat()) } )
            } ),
            /*
             * Transformmation: Split and grow the main tree, add too branches left
             * and right.
             */            
            new List<Rule> {
                new Rule("stem(r,l)", 1.0f, 
                    (Params p) => (p["r"] > 0.02f && p["l"] > 0.1f),
                    (Params p) => new List<Part> {
                        new Part( "stem(r,l)", new SortedDictionary<string, float> {
                            ["r"] = p["r"]*1.05f, ["l"] = p["l"]*0.8f } ),
                        new Part( "push()", null ),
                            new Part( "rotate(d,x,y,z)", new SortedDictionary<string, float> {
                                ["d"] = 30f+_rnd.getFloat()*30f, ["x"] = 0f, ["y"] = 0f, ["z"] = 1f } ),
                            new Part( "stem(r,l)", new SortedDictionary<string, float> {
                                ["r"] = p["r"]*0.6f, ["l"] = p["l"]*0.8f } ),
                        new Part( "pop()", null ),
                        new Part( "push()", null ),
                            new Part( "rotate(d,x,y,z)", new SortedDictionary<string, float> {
                                ["d"] = -30f+_rnd.getFloat()*30f, ["x"] = 0f, ["y"] = 0f, ["z"] = 1f} ),
                            new Part( "stem(r,l)", new SortedDictionary<string, float> {
                                ["r"] = p["r"]*0.6f, ["l"] = p["l"]*0.8f} ),
                        new Part( "pop()", null ),
                        new Part( "rotate(d,x,y,z)", new SortedDictionary<string, float> {
                            ["d"] = 90f+_rnd.getFloat()*20f, ["x"] = 1f, ["y"] = 0f, ["z"] = 0f } )
                    }
                )
            },
            /*
             * Macros: Break down the specific operation "stem" to a standard
             * turtle operation.
             */
            new List<Rule> {
                new Rule("stem(r,l)", 1.0f, 
                    (Params p) => (p["r"] > 0.06f),
                    (Params p) => new List<Part> {
                        new Part( "fillrgb(r,g,b)", new SortedDictionary<string, float> {
                            ["r"] = 0.4f, ["g"] = 0.2f, ["b"] = 0.1f } ),
                        new Part( "cyl(r,l)", new SortedDictionary<string, float> {
                            ["r"] = p["r"], ["l"] = p["l"] } )
                    }
                ),
                new Rule("stem(r,l)", 1.0f,  
                    (Params p) => (p["r"] <= 0.06 && p["r"] > 0.04),
                    (Params p) => new List<Part> {
                        new Part( "fillrgb(r,g,b)", new SortedDictionary<string, float> {
                            ["r"] = 0.3f, ["g"] = 0.5f, ["b"] = 0.1f } ),
                        new Part( "flat(r,l)", new SortedDictionary<string, float> {
                            ["r"] = p["r"], ["l"] = p["l"] } )
                    }
                ),
                new Rule("stem(r,l)", 1.0f, 
                    (Params p) => (p["r"] <= 0.04f),
                    (Params p) => new List<Part> {
                        new Part( "fillrgb(r,g,b)", new SortedDictionary<string, float> {
                            ["r"] = 0.2f, ["g"] = 0.4f, ["b"] = 0.8f } ),
                        new Part( "flat(r,l)", new SortedDictionary<string, float> {
                            ["r"] = p["r"], ["l"] = p["l"] } )
                    }
                ),
            }
        );

    }


    private Instance _createLInstance()
    {
        var whichtree = _rnd.getFloat();

        builtin.tools.Lindenmayer.System lSystem = null;
        if( whichtree<0.5 ) {
            lSystem = _createTree1System();
        } else {
            lSystem = _createTree2System();
        }
        // TXWTODO: Create some sort of function encapsulating this?
        var lGenerator = new LGenerator( lSystem );
        var lInstance = lGenerator.Instantiate();
        var prevInstance = lInstance;
        for( int i=0; i<(int)(_rnd.getFloat()*2.5f+1f); ++i ) 
        {
            var nextInstance = lGenerator.Iterate( prevInstance );
            if( null==nextInstance ) {
                break;
            }
            prevInstance = nextInstance;
        }
        return lGenerator.Finalize( prevInstance );
    }


    public void FragmentOperatorApply(
        in engine.world.Fragment worldFragment
    )
    {
        float cx = _clusterDesc.Pos.X - worldFragment.Position.X;
        float cz = _clusterDesc.Pos.Z - worldFragment.Position.Z;

        float fsh = engine.world.MetaGen.FragmentSize / 2.0f;            

        /*
         * We don't apply the operator if the fragment completely is
         * outside our boundary box (the cluster)
         */
        {
            float csh = _clusterDesc.Size / 2.0f;
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

        var atomsMap = new SortedDictionary<string, engine.joyce.Mesh>();

        foreach( var quarter in quarterStore.GetQuarters() ) {
            if( quarter.IsInvalid() ) {
                Trace( "Skipping invalid quarter." );
                continue;
            }

            var xmiddle = 0.0;
            var ymiddle = 0.0;
            var n = 0;
            var delims = quarter.GetDelims();
            foreach( var delim in delims ) {
                xmiddle += delim.StreetPoint.Pos.X;
                ymiddle += delim.StreetPoint.Pos.Y;
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
            foreach( var estate in quarter.GetEstates() ) {

                /*
                 * Only consider this estate, if the center coordinate 
                 * is within this fragment.
                 */
                var center = estate.GetCenter();
                center.X += cx;
                center.Z += cz;
                if(!worldFragment.IsInsideLocal( center.X, center.Z )) {
                    continue;
                }
                
                // TXWTODO: The 2.15 is copied from GenerateClusterQuartersOperator
                var inFragmentY = _clusterDesc.AverageHeight + 2.15f;

                /*
                 * Ready to add a tree if there is no house.
                 */ 
                var buildings = estate.GetBuildings();
                if( buildings.Count>0 ) {
                    continue;
                }

                /*
                 * But don't use every estate, just some.
                 */
                if( _rnd.getFloat() > 0.7f ) {

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
                var poly = estate.GetIntPoly();      
                if( 0==poly.Count ) {
                    continue;
                }
                var area = estate.GetArea();
                float areaPerTree = 1.6f;
                if( area<areaPerTree ) {
                    /*
                     * If the area of the estate is less than 4m2, we just plant a 
                     * single tree.
                     */
                    var instance = _createLInstance();
                    var alpha = new AlphaInterpreter( instance );
                    var treePos = center with { Y = inFragmentY };
                    alpha.Run( worldFragment, treePos, atomsMap );
                    trace( $"GenerateTreesOperator.fragmentOperatorApply(): Adding single tree at {treePos}." );
                } else if( area >= (2f*areaPerTree) ) {
                    /*
                     * Just one other algo: random.
                     * We guess that one tree takes up 1x1m, i.e. 1m2
                     */

                    var nTrees = (int)((area+1f)/areaPerTree);
                    if( nTrees > 80 ) nTrees = 80;
                    var nPlanted = 0;
                    var iterations = 0;
                    var extent = estate.GetMaxExtent();
                    var min = estate.getMin();
                    while(nPlanted < nTrees && iterations < 4*nTrees) {
                        iterations++;
                        var treePos = new Vector3( 
                            min.X + _rnd.getFloat()*extent.X, 
                            inFragmentY, 
                            min.Z + _rnd.getFloat()*extent.Z 
                        );
                        // TXWTODO: Check, if it is inside.
                        if( !estate.IsInside( treePos ) ) continue;
                        treePos.X += cx; treePos.Z += cz;
                        var instance = _createLInstance();
                        var alpha = new AlphaInterpreter( instance );
                        alpha.Run( worldFragment, treePos, atomsMap );
                        nPlanted++;
                        trace( $"GenerateTreesOperator.fragmentOperatorApply(): Adding tree at {treePos}." );
                    }
                }
            }

        }


        try
        {
            foreach (var mesh in atomsMap.Values)
            {
                {
                    engine.joyce.InstanceDesc instanceDesc = new();
                    instanceDesc.Meshes.Add(mesh);
                    instanceDesc.MeshMaterials.Add(0);
                    instanceDesc.Materials.Add(_getTreesMaterial());
                    worldFragment.AddStaticMolecule(instanceDesc, null);
                }
            }

        }
        catch (Exception e)
        {
            trace($"Unknown exception: {e}");
        }
        
    }
    

    public GenerateTreesOperator(
        engine.world.ClusterDesc clusterDesc,
        string strKey
    ) {
        _clusterDesc = clusterDesc;
        _myKey = strKey;
        _rnd = new engine.RandomSource(strKey);
   }
}
