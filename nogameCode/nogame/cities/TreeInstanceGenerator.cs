using System;
using System.Collections.Generic;
using System.Numerics;
using builtin.tools.Lindenmayer;
using engine;
using engine.joyce;
using engine.world;
using static engine.Logger;

namespace nogame.cities;

/**
 * Create instances of trees based on the parameters.
 */
public class TreeInstanceGenerator
{
    static private object _lo = new();

    private readonly int _nTemplates = 30;
    private InstanceDesc[] _arrInstanceDescs = null;
    
    private engine.joyce.Material _jMaterialTrees = null;
    private engine.joyce.Material _getTreesMaterial()
    {
        lock (_lo)
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
    
    private builtin.tools.Lindenmayer.System _createTree1System(RandomSource rnd) 
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
                    ["d"] = rnd.getFloat()*359f, ["x"] = 1f, ["y"] = 0f, ["z"] = 0f } ),
                new Part( "stem(r,l)", new SortedDictionary<string, float> {
                    ["r"] = 0.10f, ["l"] = (1f+3f*rnd.getFloat()) } )
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
                                ["d"] = 30f+rnd.getFloat()*30f, ["x"] = 0f, ["y"] = 0f, ["z"] = 1f } ),
                            new Part( "stem(r,l)", new SortedDictionary<string, float> {
                                ["r"] = p["r"]*0.6f, ["l"] = p["l"]*0.8f } ),
                        new Part( "pop()", null ),
                        new Part( "push()", null ),
                            new Part( "rotate(d,x,y,z)", new SortedDictionary<string, float> {
                                ["d"] = -30f+rnd.getFloat()*30f, ["x"] = 0f, ["y"] = 0f, ["z"] = 1f } ),
                            new Part( "stem(r,l)", new SortedDictionary<string, float> {
                                ["r"] = p["r"]*0.6f, ["l"] = p["l"]*0.8f }),
                        new Part( "pop()", null ),
                        new Part( "rotate(d,x,y,z)", new SortedDictionary<string, float> {
                            ["d"] = 90f+rnd.getFloat()*20f, ["x"] = 1f, ["y"] = 0f, ["z"] = 0f } ),
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


    private builtin.tools.Lindenmayer.System _createTree2System(RandomSource rnd)
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
                    ["d"] = rnd.getFloat()*359f, ["x"] = 1f, ["y"] = 0f, ["z"] = 0f } ),
                new Part( "stem(r,l)", new SortedDictionary<string, float> {
                    ["r"] = 0.1f, ["l"] = (1f+3f*rnd.getFloat()) } )
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
                                ["d"] = 30f+rnd.getFloat()*30f, ["x"] = 0f, ["y"] = 0f, ["z"] = 1f } ),
                            new Part( "stem(r,l)", new SortedDictionary<string, float> {
                                ["r"] = p["r"]*0.6f, ["l"] = p["l"]*0.8f } ),
                        new Part( "pop()", null ),
                        new Part( "push()", null ),
                            new Part( "rotate(d,x,y,z)", new SortedDictionary<string, float> {
                                ["d"] = -30f+rnd.getFloat()*30f, ["x"] = 0f, ["y"] = 0f, ["z"] = 1f} ),
                            new Part( "stem(r,l)", new SortedDictionary<string, float> {
                                ["r"] = p["r"]*0.6f, ["l"] = p["l"]*0.8f} ),
                        new Part( "pop()", null ),
                        new Part( "rotate(d,x,y,z)", new SortedDictionary<string, float> {
                            ["d"] = 90f+rnd.getFloat()*20f, ["x"] = 1f, ["y"] = 0f, ["z"] = 0f } )
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


    private Instance _createLInstance(RandomSource rnd)
    {
        var whichtree = rnd.getFloat();

        builtin.tools.Lindenmayer.System lSystem = null;
        if( whichtree<0.5 ) {
            lSystem = _createTree1System(rnd);
        } else {
            lSystem = _createTree2System(rnd);
        }
        // TXWTODO: Create some sort of function encapsulating this?
        var lGenerator = new LGenerator( lSystem );
        var lInstance = lGenerator.Instantiate();
        var prevInstance = lInstance;
        int iMax = (int)(rnd.getFloat() * 1.5f + 1f);
        for( int i=0; i<iMax; ++i ) 
        {
            var nextInstance = lGenerator.Iterate( prevInstance );
            if( null==nextInstance ) {
                break;
            }
            prevInstance = nextInstance;
        }
        return lGenerator.Finalize( prevInstance );
    }


    private InstanceDesc _buildTreeInstance(int idx, in RandomSource rnd)
    {
        InstanceDesc instanceDesc = new();
        
        /*
         * This takes some extra effort (cause the lindenmayer generator
         * is meant for use with larger things): We build the trees one by
         * one using their material maps...
         */ 
        var atomsMap = new SortedDictionary<string, engine.joyce.Mesh>();
        var instance = _createLInstance(rnd);
        var alpha = new AlphaInterpreter( instance );
        alpha.Run( null, Vector3.Zero, atomsMap );
        
        /*
         * ... then we create a standard instanceDesc from it.
         */
        try
        {
            foreach (var mesh in atomsMap.Values)
            {
                {
                    instanceDesc.Meshes.Add(mesh);
                    instanceDesc.MeshMaterials.Add(0);
                    instanceDesc.Materials.Add(_getTreesMaterial());
                }
            }

        }
        catch (Exception e)
        {
            Trace($"Unknown exception: {e}");
        }

        return instanceDesc;
    }


    public InstanceDesc CreateInstance(Fragment targetFragment, Vector3 vTargetPosition, int param)
    {
        lock (_lo)
        {
            if (null == _arrInstanceDescs)
            {
                _arrInstanceDescs = new InstanceDesc[_nTemplates];
                for (int i = 0; i < _nTemplates; ++i)
                {
                    RandomSource rnd = new($"TreeGen{i}");
                    _arrInstanceDescs[i] = _buildTreeInstance(i, rnd);
                }
            }

            Matrix4x4 mPos = Matrix4x4.CreateTranslation(vTargetPosition);
            return _arrInstanceDescs[param % _nTemplates].TransformedCopy(mPos);
        }
    }
}