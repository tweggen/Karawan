using System;
using System.Collections.Generic;
using static engine.Logger;

namespace engine;


public class ObjectRegistry<T> : ObjectFactory<string, T> where T : class 
{
    private object _lo = new();
    
    public T FindLike(in T referenceObject)
    {
        return FindAdd(referenceObject.ToString(), referenceObject);
    }
}