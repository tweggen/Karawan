using System.Text.Json.Nodes;

namespace builtin.tools.Lindenmayer;

public class Part
{
    public string Name;

    public Params Parameters;

    public override string ToString()
    {
        var strName = "null";
        if( null != Name ) {
            strName = $"{Name}";
        }
        string strParameters;
        if( null != Parameters ) {
            strParameters = Parameters.Map.ToString();
        }
        else
        {
            strParameters = "(null)";
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

    public Part(
        in string name,
        JsonObject map
    )
    {
        Name = name;
        Parameters = new Params(map);
    }
    
}
