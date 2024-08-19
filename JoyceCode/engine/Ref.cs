using System;

namespace engine;

public class Ref<T> where T : IDisposable
{
    private class ObjectReference<T>
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
            lock (Lock)
            {
                return --NReferences == 0;
            }
        }
        
        public ObjectReference(T obj)
        {
            Obj = obj;
            NReferences = obj;
        }
    }

    private ObjectReference<T> _ref;
    
    public Ref(Ref<T> other)
    {
        other._ref.AddReference();
        _ref = other._ref;
    }
    
    public Ref(T obj)
    {
        _ref = obj;
    }
}