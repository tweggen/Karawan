using BepuPhysics.Constraints;

namespace engine.behave;


public class SpawnStatus
{
    private static int _nextId = 0;
 
    /**
     * For debugging: Identify this SpawnStatus
     */
    public int Id;

    private ushort _minCharacters;
    /**
     * The minimum of characters per fragment.
     */
    public ushort MinCharacters
    { 
        get { lock (this) return _minCharacters; }
        set { lock (this) _minCharacters = value; }
    }

    private ushort _maxCharacters;
    /**
     * The maximum of characters per fragment.
     */
    public ushort MaxCharacters
    { 
        get { lock (this) return _maxCharacters; }
        set { lock (this) _maxCharacters = value; }
    }

    private ushort _inCreation;
    /**
     * The number of characters in creation.
     */
    public ushort InCreation
    { 
        get { lock (this) return _inCreation; }
        set { lock (this) _inCreation = value; }
    }

    private ushort _isDying;
    /**
     * The number of characters currently being killed.
     */
    public ushort IsDying
    { 
        get { lock (this) return _isDying; }
        set { lock (this) _isDying = value; }
    }

    private ushort _dead;
    /**
     * The number of deads that won't be recreated
     */
    public ushort Dead
    { 
        get { lock (this) return _dead; }
        set { lock (this) _dead = value; }
    }
    

    public ushort ResidentCharacters
    {
        get => (ushort)(InCreation + Dead);
    }


    public bool IsValid()
    {
        return InCreation != 0xffff;
    }

    public SpawnStatus()
    {
        _inCreation = 0xffff;
        Id = ++_nextId;
    }
}