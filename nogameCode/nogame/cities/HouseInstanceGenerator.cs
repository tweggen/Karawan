
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json.Nodes;
using builtin.tools.Lindenmayer;
using engine;
using engine.geom;
using engine.joyce;
using static engine.Logger;
using static builtin.extensions.JsonObjectNumerics;

namespace nogame.cities;

/**
 * Create an individual house.
 * Unfortunately, instead of re-using houses, we need to re-create houses one
 * by one to fit the surrounding. However, this is less of an effort when compared
 * to the trees.
 */
public class HouseInstanceGenerator
{
    static private object _lo = new();

    public HouseInstanceGenerator()
    {
        I.Get<ObjectRegistry<Material>>().RegisterFactory("nogame.characters.house.materials.powerlines",
            name => new Material()
            {
                EmissiveTexture = I.Get<TextureCatalogue>().FindColorTexture(0xff33ffff)
            });
        // TXWTODO: Register materials
    }

    
    #if false
    private JsonObject _extrudeAh(IEnumerable<Vector3> pA, float ph) => new ()
    {
        ["A"] = From(pA), ["h"] = ph
    };
    #endif


    private Part _createSeed1(Params ini, builtin.tools.RandomSource rnd)
    {
        var jo = new JsonObject
        {
            ["A"] = ini["A"].DeepClone(), ["h"] = (float)ini["h"]
        };
        return new("buildable(A,h)", jo);
    }
    
    
    public builtin.tools.Lindenmayer.System CreateHouse1System(
        Params ini,
        builtin.tools.RandomSource rnd
        )
    {
        return new builtin.tools.Lindenmayer.System(new State(new List<Part>
            /*
             * Initial seed
             *
             * Expected context parameters:
             *    "basearea" : Poly
             *        The area we may build our building on
             *    "maxheight" : float
             *        The maximal height of the building.
             */
            {
                /*
                 * Straight up.
                 */
                new Part( "rotate(d,x,y,z)", new JsonObject {
                    ["d"] = 90f,["x"] = 0f, ["y"] = 0f, ["z"] =1f } ),
                _createSeed1(ini, rnd)
            }),
            
            /*
             * Transformation
             */
            new List<Rule>
            {
                /*
                 * A buildable with available space more than 4 stories may become
                 * segmented into a lower buildableBaseSegment and an upper buildableSegment. 
                 */
                new Rule("buildable(A,h)",
                    0.8f, (Params p) => (float)p["h"] > 4f*3f,
                    (p) =>
                    {
                        int availableStories = (int)Single.Ceiling((float)p["h"]) / 3;
                        /*
                         * The base is at least on storey.
                         */
                        int baseStories = 1 + (int)(((float)availableStories - 1f) * rnd.GetFloat());
                        
                        /*
                         * Well, all that remains is the reaminder.
                         */
                        int remainingStories = availableStories - baseStories;

                        var v3Edges = ToVector3List(p["A"]);

                        var v3SmallerEdges = new PolyTool(v3Edges, Vector3.UnitY).Extend(-2f);
                        
                        if (null == v3SmallerEdges)
                        {
                            /*
                             * We can't shrink it any more, keep the entire stem.
                             * TXWTODO: It would be kind of nice ta allow the grammer to do further
                             * trials.
                             */
                            return new List<Part>
                            {
                                new("buildableBaseSegment(A,h)", new JsonObject
                                {
                                    ["A"] = From(v3Edges), ["h"] = (float)(availableStories * 3f)
                                })
                            };
                        }
                        else
                        {
                            return new List<Part>
                            {
                                new("buildableBaseSegment(A,h)", new JsonObject
                                {
                                    ["A"] = From(v3Edges), ["h"] = (float)(baseStories * 3f)
                                }),
                                new("buildableAnySegment(A,h)", new JsonObject
                                {
                                    ["A"] = From(v3SmallerEdges), ["h"] = (float)(remainingStories * 3f)
                                }),
                            };
                        }
                    }),

                /*
                 * A buildable with available space more than 4 stories may become
                 * segmented into a lower buildableBaseSegment and an upper buildableSegment.
                 */
                new Rule("buildableAnySegment(A,h)",
                    0.9f, (Params p) => (float)p["h"] > 4f*3f,
                    (p) =>
                    {
                        int availableStories = (int)Single.Ceiling((float)p["h"]) / 3;

                        /*
                         * The base is at least on storey.
                         */
                        int lowerStories = 1 + (int)(((float)availableStories - 1f) * rnd.GetFloat());
                        
                        /*
                         * Well, all that remains is the reaminder.
                         */
                        int upperStories = availableStories - lowerStories;

                        var v3Edges = ToVector3List(p["A"]);

                        var v3SmallerEdges = new PolyTool(v3Edges, Vector3.UnitY).Extend(-2f);
                        if (null == v3SmallerEdges)
                        {
                            /*
                             * We can't shrink it any more, keep the entire stem.
                             * TXWTODO: It would be kind of nice ta allow the grammer to do further
                             * trials.
                             */
                            return new List<Part>
                            {
                                new("segment(A,h)", new JsonObject
                                {
                                    ["A"] = From(v3Edges), ["h"] = (float)(availableStories * 3f)
                                })
                            };
                        }
                        else
                        {
                            return new List<Part>
                            {
                                new("buildableAnySegment(A,h)", new JsonObject
                                {
                                    ["A"] = From(v3Edges), ["h"] = (float)(lowerStories * 3f)
                                }),
                                new("buildableAnySegment(A,h)", new JsonObject
                                {
                                    ["A"] = From(v3SmallerEdges), ["h"] = (float)(upperStories * 3f)
                                }),
                            };
                        }
                    }),

                /*
                 * A buildable may straightforward become a single buildable base part.
                 * That's what we had in the original game all the time.
                 */
                new Rule("buildable(A,h)",
                    0.1f, (Params p) => (float)p["h"] > 4f*3f,
                    (p) => new List<Part>
                    {
                        new ("buildableBaseSegment(A,h)", new JsonObject
                        {
                            ["A"] = p["A"].DeepClone(), ["h"] = (float)p["h"]
                        }),
                    }),
                
                new Rule("buildable(A,h)",
                    1f, (Params p) => (float)p["h"] < 4f*3f,
                    (p) => new List<Part>
                    {
                        new ("buildableBaseSegment(A,h)", new JsonObject
                        {
                            ["A"] = p["A"].DeepClone(), ["h"] = (float)p["h"]
                        }),
                    }),
                
                /*
                 * A buildable base segment has neon signs etc. .
                 */
                new Rule("buildableBaseSegment(A,h)",
                    (p) => new List<Part>
                    {
                        new ("powerline(P,h)", new JsonObject
                        {
                            ["P"] = From(AnyOf(rnd, ToVector3List(p["A"].DeepClone()))),
                            ["h"] = (float)p["h"],
                            ["mat"] = "nogame.characters.house.materials.powerlines"
                        }),
                        new ("segment(A,h)", new JsonObject
                        {
                            ["A"] = p["A"].DeepClone(), ["h"] = (float)p["h"]
                        }),
                        new ("neon(P,h,n)", new JsonObject
                        {
                            ["P"] = From(ToVector3List(p["A"].DeepClone()).First()),
                            ["h"] = ((float)p["h"])*(rnd.GetFloat()*0.7f+0.1f),
                            ["n"] = (rnd.Get8()&3)+2
                        })
                    }),
                
                /*
                 * Any other segment does not have neon signs.
                 */
                new Rule("buildableAnySegment(A,h)",
                    0.1f, Rule.Always,
                    (p) => new List<Part>
                    {
                        new ("powerline(P,h)", new JsonObject
                        {
                            ["P"] = From(AnyOf(rnd, ToVector3List(p["A"].DeepClone()))),
                            ["h"] = (float)p["h"],
                            ["mat"] = "nogame.characters.house.materials.powerlines"
                        }),
                        new ("segment(A,h)", new JsonObject
                        {
                            ["A"] = p["A"].DeepClone(), ["h"] = (float)p["h"]
                        }),
                    })
            },
            
            /*
             * Macros: Break down the specific operation stem to a standard turtle operation.
             */
            new List<Rule>
            {
                new Rule("segment(A,h)",
                    (p) => new List<Part>
                    {
                        new ("extrudePoly(A,h,mat)", new JsonObject
                        {
                            ["A"] = p["A"].DeepClone(), ["h"] = (float)p["h"], ["mat"] = "nogame.cities.houses.materials.houses.win3"
                        })
                    }),
                new Rule("neon(P,h,n)",
                    (p) => new List<Part>
                    {
                        /* TXWTODO: Write me */
                    })
            });
    }
}
