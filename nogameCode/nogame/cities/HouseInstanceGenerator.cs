
using System.Collections.Generic;
using System.Linq;
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
                _createSeed1(ini, rnd)
            }),
            
            /*
             * Transformation
             */
            new List<Rule>
            {
                new Rule("buildable(A,h)",
                (p) => new List<Part>
                {
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
                    })
            });
    }
}
