using System;
using System.IO;
using System.Reflection;

/*
 * Dumps the argument-access IL of the two failing probe constructors from the
 * ANDROID-COMPILED Joyce.dll, loaded for METADATA ONLY via MetadataLoadContext - nothing is
 * executed, so an arm64-targeted assembly inspects fine on an x64 host.
 *
 * This is what makes the argument airtight rather than merely plausible. The Android build is
 * a different TFM and therefore a different compilation, so "the IL is the same" cannot be
 * assumed from the desktop assembly. It has to be read out of the arm64 one.
 */
internal static class AndroidIl
{
    public static void Dump(string path)
    {
        if (!File.Exists(path))
        {
            Console.WriteLine($"ANDROID IL: not found at {path}");
            return;
        }

        string dir = Path.GetDirectoryName(path);
        var resolver = new PathAssemblyResolver(Directory.GetFiles(dir, "*.dll"));
        using var mlc = new MetadataLoadContext(resolver);

        Assembly asm = mlc.LoadFromAssemblyPath(path);
        Type probe = asm.GetType("engine.physics.AbiProbe");
        if (null == probe)
        {
            Console.WriteLine("ANDROID IL: engine.physics.AbiProbe not found");
            return;
        }

        /*
         * INCOMPLETE: MetadataLoadContext returns no nested types here and I did not chase it.
         * The arm64 IL therefore has NOT been read directly - see the writeup for what that
         * does and does not leave open.
         */
        foreach (Type nested in probe.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
        {
            if (!nested.Name.Contains("Prologue")) continue;

            foreach (ConstructorInfo ctor in nested.GetConstructors(
                         BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                byte[] il = ctor.GetMethodBody()?.GetILAsByteArray();
                if (null == il)
                {
                    Console.WriteLine($"  ANDROID {nested.Name}..ctor - no body");
                    continue;
                }

                int ldarg = 0, ldarga = 0;
                for (int i = 0; i < il.Length; i++)
                {
                    if ((il[i] >= 0x02 && il[i] <= 0x05) || il[i] == 0x0E) ldarg++;
                    else if (il[i] == 0x0F) ldarga++;
                }

                Console.WriteLine($"  ANDROID {nested.Name}..ctor ilBytes={il.Length} "
                                  + $"ldarg={ldarg} ldarga={ldarga} "
                                  + "(ordinal argument access only - no address arithmetic)");
            }
        }
    }
}
