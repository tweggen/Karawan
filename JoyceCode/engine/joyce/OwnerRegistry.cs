using System;
using System.Collections.Generic;
using static engine.Logger;

namespace engine.joyce;


internal class OwnerEntry
{
    public System.Type Type;
    public ushort Id;
    public IOwner Implementation;
}


public class OwnerRegistry : AModule
{
    private object _lo = new();
    private SortedDictionary<System.Type, OwnerEntry> _mapTypes = new();
    private SortedDictionary<ushort, OwnerEntry> _mapIds = new();
    private ushort _nextId = engine.world.components.Owner.OwnerId_HardcodeMax + 1;
    
    
    public ushort FindOwnerId(System.Type typeOwner)
    {
        lock (_lo)
        {
            if (_mapTypes.TryGetValue(typeOwner, out var oe))
            {
                return oe.Id;
            }
            else
            {
                ErrorThrow<ArgumentException>($"Unable to find owner for type {typeOwner}");
                return 0;
            }
        }
    }

    
    public void UnregisterOwner(System.Type typeOwner)
    {
        lock (_lo)
        {
            var oe = _mapTypes[typeOwner];
            _mapTypes.Remove(oe.Type);
            _mapIds.Remove(oe.Id);
        }
    }
    
    
    public void RegisterOwner(IOwner iowner)
    {
        lock (_lo)
        {
            var oe = new OwnerEntry()
            {
                Type = iowner.GetType(),
                Id = _nextId++,
                Implementation = iowner
            };
            _mapIds[oe.Id] = oe;
        }
    }
}