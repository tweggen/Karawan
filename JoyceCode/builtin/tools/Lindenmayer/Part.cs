using System.Collections.Generic;

namespace builtin.tools.Lindenmayer;

public class Part
{
    public string Name;

    public Params Parameters;

    public string ToString()
    {
        var strName = "null";
        if( null != Name ) {
            strName = $"{Name}";
        }
        var strParameters = "null";
        if( null != Parameters ) {
            var result = "";
            var isFirst = true;
            foreach (string key in Parameters.Map.Keys) 
            {
                if( !isFirst ) {
                    result += ", ";
                } else {
                    isFirst = false;
                }
                result += $"{key} -> {Parameters.Map[key]}";
            }
            strParameters = $"{result}";
        }
        return $"Part {{ Name => \"{strName}\", Parameters => {strParameters} }}";
    }

    public Part Clone()
    {
        /*
         * Note: Copying the map works because there are no data structures inside.
         */
        Params dupParams = null;
        if( null != Parameters ) {
            dupParams = Parameters.Clone();
        }
        return new Part( Name, dupParams );
    }

    public Part(
        string name,
        Params parameters
    ) {
        Name = name;
        Parameters = parameters;
    }
   
}
