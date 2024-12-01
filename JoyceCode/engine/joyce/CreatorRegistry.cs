using System;
using System.Collections.Generic;
using static engine.Logger;

namespace engine.joyce;


internal class CreatorEntry
{
    public ICreator Creator;
    public ushort Id;
}


public class CreatorRegistry : AModule
{
    private object _lo = new();
    private SortedDictionary<string, CreatorEntry> _mapNames = new();
    private Dictionary<ICreator, CreatorEntry> _mapCreators = new();
    private SortedDictionary<ushort, CreatorEntry> _mapIds = new();
    private ushort _nextId = engine.world.components.Creator.CreatorId_HardcodeMax + 1;
    
    
    public ushort FindCreatorId(ICreator iCreator)
    {
        lock (_lo)
        {
            if (_mapCreators.TryGetValue(iCreator, out var ce))
            {
                return ce.Id;
            }
            else
            {
                ErrorThrow<ArgumentException>($"Unable to find owner for type {iCreator.GetType()}");
                return 0;
            }
        }
    }


    public ICreator GetCreator(ushort creatorId)
    {
        lock (_lo)
        {
            return _mapIds[creatorId].Creator;
        }
    }
    
    
    public ICreator GetCreator(string strCreator)
    {
        lock (_lo)
        {
            return _mapNames[strCreator].Creator;
        }
    }
    
    
    public void UnregisterCreator(ICreator iCreator)
    {
        lock (_lo)
        {
            var ce = _mapCreators[iCreator];
            _mapCreators.Remove(ce.Creator);
            _mapIds.Remove(ce.Id);
        }
    }
    
    
    public void RegisterCreator(ICreator iCreator)
    {
        lock (_lo)
        {
            var ce = new CreatorEntry()
            {
                Id = _nextId++,
                Creator = iCreator
            };
            _mapIds[ce.Id] = ce;
            _mapCreators[iCreator] = ce;
            _mapNames[iCreator.GetType().ToString()] = ce;
        }
    }
}
