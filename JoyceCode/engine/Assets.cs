using System;
using static engine.Logger;

namespace engine;

public sealed class Assets
{
    private static object _staticlock = new();
    private static IAssetImplementation _implementation;

    public static System.IO.Stream Open(in string filename)
    {
        IAssetImplementation impl = null;
        lock (_staticlock)
        {
            impl = _implementation;
        }

        if (null == impl)
        {
            ErrorThrow("Platform Asset Implementation not setup.", m=>new InvalidOperationException(m));           
        }

        return impl.Open(filename);
    }

    public static void SetAssetImplementation(in IAssetImplementation impl)
    {
        lock (_staticlock)
        {
            _implementation = impl;
        }
    }
}