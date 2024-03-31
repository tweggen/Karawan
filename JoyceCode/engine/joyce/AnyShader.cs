using System;

namespace engine.joyce;

public interface AnyShader : IComparable
{
    public string Source { get; set; }
    
    public bool IsValid();
}