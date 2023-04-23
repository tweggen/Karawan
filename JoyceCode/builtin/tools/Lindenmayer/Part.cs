
#if false
using System.Collections.Generic;

namespace builtin.tools.Lindenmayer;

public class Part
{
    public string Name;
    public SortedDictionary<string, > parameters: LParams;

    public function toString(): String {
        var strName = "null";
        if( null != name ) {
            strName = '"$name"';
        }
        var strParameters = "null";
        if( null != parameters ) {
            var out = "";
            var isFirst = true;
            for( key in parameters.keys() ) {
                if( !isFirst ) {
                    out += ", ";
                } else {
                    isFirst = false;
                }
                out += '"$key" -> ${parameters[key]}';
            }
            strParameters = '{ $out }';
        }
        return 'LPart { name => "$strName", parameters => $strParameters }';
    }

    public function clone(): LPart {
        /*
         * Note: Copying the map works because there are no data structures inside.
         */
        var dupParams = null;
        if( null != parameters ) {
            dupParams = parameters.copy();
        }
        return new LPart( name, dupParams );
    }

    public function new(
    name0: String,
    parameters0: LParams
    ) {
        name = name0;
        parameters = parameters0;
    }
   
}
#endif