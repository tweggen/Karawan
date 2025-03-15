using System.Runtime.InteropServices;

namespace engine.joyce;

[StructLayout(LayoutKind.Explicit, Size = 16)]
public struct Int4
{
    [FieldOffset(0)]
    public int B0; 

    [FieldOffset(4)]
    public int B1; 
        
    [FieldOffset(8)]
    public int B2; 
        
    [FieldOffset(12)]
    public int B3; 

        
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
    