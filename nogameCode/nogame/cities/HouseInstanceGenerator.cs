
using System.Collections.Generic;
using System.Numerics;
using System.Text.Json.Nodes;
using builtin.tools.Lindenmayer;
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
        // TXWTODO: Register materials
    }

    
    private JsonObject _extrudeAh(IEnumerable<Vector3> pA, float ph) => new ()
    {
        ["A"] = From(pA), ["h"] = ph
    };
    
    
    private builtin.tools.Lindenmayer.System _createHouse1System(
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
                
                new Part( "segment(A,h)", new JsonObject {
                    ["A"] = ini["A"], ["h"] = (float)ini["h"] } ),
            }),
            
            /*
             * Transformation
             */
            new List<Rule>
            {
                /*
                 * We are testing and not using any rules at all.
                 */
            },
            
            /*
             * Macros: Break down the specific operation stem to a standard turtle operation.
             */
            new List<Rule>
            {
                new Rule("segment(A,h)",
                    (Params p) => new List<Part>
                    {
                        new Part("extrudePoly(A,h,mat)", new JsonObject
                        {
                            ["A"] = p["A"], ["h"] = p["h"], ["mat"] = "nogame.cities.houses.materials.houses.win3"
                        })
                    })
            });
    }
}
