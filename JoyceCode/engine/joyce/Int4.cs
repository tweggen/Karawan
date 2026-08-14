using System.Runtime.InteropServices;
using MessagePack;

namespace engine.joyce;

/**
 * Per-vertex bone indices.
 *
 * MessagePack-annotated for WP-4.1: unlike the System.Numerics vector, matrix and
 * quaternion types, which MessagePack 3.x resolves out of the box, a plain struct
 * of ours is not registered in StandardResolver and throws
 * FormatterNotRegisteredException the moment a Mesh carrying bone weights is
 * serialised.
 */
[MessagePackObject]
[StructLayout(LayoutKind.Explicit, Size = 16)]
public struct Int4
{
    [Key(0)]
    [FieldOffset(0)]
    public int B0;

    [Key(1)]
    [FieldOffset(4)]
    public int B1;

    [Key(2)]
    [FieldOffset(8)]
    public int B2;

    [Key(3)]
    [FieldOffset(12)]
    public int B3;


    [IgnoreMember]
    public int this[int idx]
    {
        get => (idx < 2) ? ((idx == 0) ? B0 : B1) : ((idx == 2) ? B2 : B3);

        set
        {
            switch (idx)
            {
                default:
                case 0: B0 = value; break;
                case 1: B1 = value; break;
                case 2: B2 = value; break;
                case 3: B3 = value; break;
            }
        }
    }
    
    
    public override string ToString()
    {
        return $"B0: {B0}, B1: {B1}, B2: {B2}, B3: {B3}";
    }

    public Int4()
    {
        B0 = B1 = B2 = B3 = 0;
    }

    public Int4(int value)
    {
        B0 = B1 = B2 = B3 = value;
    }
}
    