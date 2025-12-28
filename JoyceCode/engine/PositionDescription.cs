using System;
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


    public PositionDescription()
    {
    }

    public PositionDescription(PositionDescription other)
    {
        FragmentId = other.FragmentId;
        Fragment = other.Fragment;
        ClusterId = other.ClusterId;
        ClusterName = other.ClusterName;
        ClusterDesc = other.ClusterDesc;
        QuarterName = other.QuarterName;
        Quarter = other.Quarter;
        StreetPointId = other.StreetPointId;
        StreetPoint = other.StreetPoint;
        QuarterDelim = other.QuarterDelim;
        StrokeSid = other.StrokeSid;
        Stroke = other.Stroke;
        RelativePos = other.RelativePos;
        QuarterDelimIndex = other.QuarterDelimIndex;
        QuarterDelimPos = other.QuarterDelimPos;
        Position = other.Position;
        Orientation = other.Orientation;
    }
}