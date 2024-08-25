using System;

namespace engine;


internal class ObjectReference<T>
{
    public T Obj;
    public Action<T> Disposer;
    internal object Lock = new();
    internal int NReferences;

    public void AddReference()
    {
        lock (Lock)
        {
            ++NReferences;
        }
    }


    public bool RemoveReference()
    {
        bool dispose;
        lock (Lock)
        {
            dispose = --NReferences == 0;
        }

        if (dispose)
        {
            if (null != Disposer)
            {
                Disposer(Obj);
            }
            else
            {
                if (Obj is IDisposable)
                {
                    (Obj as IDisposable).Dispose();
                }
            }

            Obj = default;
        }

        return dispose;
    }

        
    public ObjectReference(T obj, Action<T> disposer)
    {
        Obj = obj;
        Disposer = disposer;
        NReferences = 1;
    }
}


public struct Ref<T>
{
    private ObjectReference<T> _ref;


    public T Value
    {
        get => _ref.Obj;
    }
    
    public bool IsNil()
    {
        return null == _ref;
    }
    

    public void Dispose()
    {
        if (_ref.RemoveReference())
        {
            _ref = null;
        }
    }
    
    
    public Ref(Ref<T> other)
    {
        other._ref.AddReference();
        _ref = other._ref;
    }
    
    
    public Ref(T obj, Action<T> Disposer = null )
    {
        _ref = new ObjectReference<T>(obj, null);
    }


    public Ref()
    {
        _ref = null;
    }
}



/**
 * The same reference pointer implementation, however, this time as a class type.
 * This basically doesn't make so much sense at all, however, it is required for
 * some types.
 */
public class RRef<T>
{
    private ObjectReference<T> _ref;


    public T Value
    {
        get => _ref.Obj;
    }
    
    public bool IsNil()
    {
        return null == _ref;
    }
    

    public void Dispose()
    {
        if (_ref.RemoveReference())
        {
            _ref = null;
        }
    }
    
    
    public RRef(RRef<T> other)
    {
        other._ref.AddReference();
        _ref = other._ref;
    }
    
    
    public RRef(T obj, Action<T> Disposer = null )
    {
        _ref = new ObjectReference<T>(obj, null);
    }


    public RRef()
    {
        _ref = null;
    }
}

