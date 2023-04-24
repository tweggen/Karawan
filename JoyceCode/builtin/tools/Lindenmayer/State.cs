using System.Collections.Generic;
using System.ComponentModel;
using static engine.Logger;

namespace builtin.tools.Lindenmayer;

public class State
{
    public IList<Part> Parts;

    public string ToString()
    {
        if (Parts != null) {
            bool isFirst = true;
            string result = "";
            foreach( Part part in Parts ) {
                if( !isFirst ) {
                    result += ", ";
                } else {
                    isFirst = false;
                }
                result += $"{part}";
            }
            return $"State {{ Parts: [ {result} ] }}";
        } else {
            return "State { Parts: null }";
        }
    }

    public State Clone()
    {
        List<Part> newParts = null;
        if( Parts != null )
        {
            newParts = new();
            foreach (Part part in Parts) {
                if( null==part ) {
                    ErrorThrow($"null part detected.",m => new InvalidEnumArgumentException(m));
                }
                newParts.Add( part.Clone() );
            }
        } else {
            // newParts = null;
        }
        State dupState = new State( newParts );
        return dupState;
    }


    public State( IList<Part> parts ) {
        Parts = parts;
    }
}