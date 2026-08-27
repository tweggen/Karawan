using System;
using engine;

namespace JoyceCode.Tests.engine;


/**
 * Shared, idempotent registration into the process-global I container.
 *
 * I.Register throws on a second registration of the same type, and the container is
 * static, so two test classes that both need the same service race: whichever runs
 * second fails with "Already registered", reporting nothing about what it was actually
 * testing. That is not hypothetical - the street geometry harness and the Assimp
 * fixture both need ObjectRegistry&lt;Material&gt;, and adding the former broke the
 * latter until this existed.
 *
 * Registering through here instead means the first caller wins and the rest simply use
 * what is there, which is the correct outcome for services that carry no test-specific
 * state.
 */
internal static class TestContainer
{
    private static readonly object _lo = new();


    internal static void EnsureRegistered<T>(Func<object> factory) where T : class
    {
        lock (_lo)
        {
            if (null == I.TryGet<T>())
            {
                I.Register<T>(factory);
            }
        }
    }
}
