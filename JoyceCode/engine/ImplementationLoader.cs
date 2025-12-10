using System;
using System.Text.Json.Nodes;
using static engine.Logger;

namespace engine;

public class ImplementationLoader
{
    private void _whenLoaded(string path, JsonNode? jnImplementations)
    {
        if (null == jnImplementations)
        {
            return;
        }

        if (engine.GlobalSettings.Get("joyce.CompileMode") == "true")
        {
            ErrorThrow<InvalidOperationException>("I should not have been called.");
            return;
        }

        try
        {
            /*
             * Register the listed class factories, possibly using the key as interface name.
             */
            if (jnImplementations is JsonObject obj)
            {
                foreach (var pair in obj)
                {
                    /*
                     * Skip internal primitives.
                     * TXWTODO:  How to handle this on mix level?
                     */
                    if (pair.Key.StartsWith("__")) continue;
                    
                    // pair.Value is already a JsonNode, so we can pass it directly
                    var factoryMethod = I.Get<engine.casette.Loader>()
                        .CreateFactoryMethod(pair.Key, pair.Value);

                    string interfaceName = pair.Key;

                    try
                    {
                        Type type = engine.rom.Loader.LoadType(interfaceName);
                        I.Instance.RegisterFactory(type, factoryMethod);
                    }
                    catch (Exception e)
                    {
                        Warning($"Unable to load implementation type {pair.Key}: {e}");
                    }
                }
            }
        }
        catch (Exception e)
        {
            // Consider logging or rethrowing instead of swallowing silently
            Warning($"Unexpected error while loading implementations: {e}");
        }

    }

    public ImplementationLoader()
    {
        if (engine.GlobalSettings.Get("joyce.CompileMode") != "true")
        {
            I.Get<engine.casette.Loader>().WhenLoaded("/implementations", _whenLoaded);
        }
    }
}