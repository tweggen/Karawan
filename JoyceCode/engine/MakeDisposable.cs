using System;

namespace engine;


public struct MakeDisposable<T> : IDisposable
{
    public T Content;

    
    public void Dispose()
    {
        Content = default;
    }

    
    public MakeDisposable(T obj)
    {
        Content = obj;
    }
    
    public MakeDisposable()
    {
        Content = default;
    }

}
