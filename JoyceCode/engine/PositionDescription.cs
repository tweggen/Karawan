using System.Numerics;
using System.Text.Json.Serialization;

namespace engine;

public class PositionDescription
{
    [JsonInclude] public string FragmentId;
    public world.Fragment Fragment;
    
    [JsonInclude] public string ClusterId;
    [JsonInclude] public string ClusterName;
    public engine.world.ClusterDesc ClusterDesc;

    [JsonInclude] public string QuarterName;
    public engine.streets.Quarter Quarter;
    
    [JsonInclude] public int StreetPointId;
    public engine.streets.StreetPoint StreetPoint;
    public engine.streets.QuarterDelim QuarterDelim;
    
    [JsonInclude] public int StrokeSid;
    public engine.streets.Stroke Stroke;
    
    [JsonInclude] public float RelativePos;
    [JsonInclude] public int QuarterDelimIndex;
    [JsonInclude] public float QuarterDelimPos;
    
    [JsonInclude] public Vector3 Position;
    [JsonInclude] public Quaternion Orientation;
}