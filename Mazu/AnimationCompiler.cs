using engine.joyce;
using static engine.Logger;

namespace Mazu;

public class AnimationCompiler : IDisposable
{
    public void Dispose()
    {
        Trace($"Disposing {nameof(AnimationCompiler)}");
    }
    
    public void Compile(ModelAnimation ma)
    {
        Trace($"Compiling animation.");
    }
    
    public AnimationCompiler()
    {
        Trace($"Animation compiler initialized.");
    }
}