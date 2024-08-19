using System;

namespace engine;

public class Ref<T> where T : IDisposable
{
    private class ObjectReference<T> where T : IDisposable
    {
        public T Obj;
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
                Obj.Dispose();
                Obj = default;
            }

            return dispose;
        }
        
        
        public ObjectReference(T obj)
        {
            Obj = obj;
            NReferences = 1;
        }
    }

    private ObjectReference<T> _ref;


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
    
    public Ref(T obj)
    {
        _ref = new ObjectReference<T>(obj);
    }
}