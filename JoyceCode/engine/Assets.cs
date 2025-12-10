using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using static engine.Logger;

namespace engine;

public sealed class Assets
{
    private static object _staticlock = new();
    private static IAssetImplementation _implementation;


    public static void AddAssociation(string tag, string uri)
    {
        Trace($"Added tag \"{tag}\" for \"{uri}\"");

        IAssetImplementation impl = null;
        lock (_staticlock)
        {
            impl = _implementation;
        }

        if (null == impl)
        {
            ErrorThrow("Platform Asset Implementation not setup.", m=>new InvalidOperationException(m));           
        }

        impl.AddAssociation(tag, uri);
    }
    
    public static System.IO.Stream Open(in string filename)
    {
        Trace($"Asked to open \"{filename}\"");
        
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


    public static bool Exists(in string filename)
    {
        Trace($"Checked for \"{filename}\"");

        IAssetImplementation impl = null;
        lock (_staticlock)
        {
            impl = _implementation;
        }

        if (null == impl)
        {
            ErrorThrow("Platform Asset Implementation not setup.", m=>new InvalidOperationException(m));           
        }

        return impl.Exists(filename);
    }
    
    public static void SetAssetImplementation(in IAssetImplementation impl)
    {
        lock (_staticlock)
        {
            _implementation = impl;
        }
    }


    public static IReadOnlyDictionary<string, string> GetAssets()
    {
        lock (_staticlock)
        {
            return _implementation.GetAssets();           
        }
    }
}