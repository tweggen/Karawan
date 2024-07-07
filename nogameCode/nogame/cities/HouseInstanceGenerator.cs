
using System.Collections.Generic;
using System.Text.Json.Nodes;
using builtin.tools.Lindenmayer;
using static engine.Logger;

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

    
    private builtin.tools.Lindenmayer.System _createHouse1System(builtin.tools.RandomSource rnd)
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
            }),
            /*
             * Transformation
             */
            new List<Rule>
            {

            },
            /*
             * Mactos: Break down the specifric operation stem to a standard turtle operation.
             */
            new List<Rule>
            {

            });
    }
}
