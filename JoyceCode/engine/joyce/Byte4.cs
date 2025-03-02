using System.Runtime.InteropServices;

namespace engine.joyce;

[StructLayout(LayoutKind.Explicit, Size = 4)]
public struct Byte4
{
    [FieldOffset(0)]
    public byte B0; 

    [FieldOffset(1)]
    public byte B1; 
        
    [FieldOffset(2)]
    public byte B2; 
        
    [FieldOffset(3)]
    public byte B3; 

        
    public Byte4()
    {
    }

    
    public byte this[int idx]
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
}
    