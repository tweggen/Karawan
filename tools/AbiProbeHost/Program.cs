using System;
/*
 * Runs engine.physics.AbiProbe on the HOST architecture, with nothing else in the process.
 *
 * The question: is the trailing-struct corruption a JIT codegen fault, or is the C# compiler
 * emitting wrong IL? Same source, same Roslyn, same IL shape - if x64 passes every case while
 * arm64 fails M and N, the difference is below IL by elimination.
 */
Console.WriteLine($"arch={System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture} "
                  + $"runtime={System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
engine.physics.AbiProbe.RunOnce();
Console.WriteLine("done");

/*
 * IL evidence. Dumps the raw method body of the two failing constructors and decodes every
 * argument access.
 *
 * The point: IL addresses arguments by ORDINAL (ldarg.N), never by address or byte offset.
 * "Read 8 bytes early" is not expressible in IL at all - physical placement of arguments,
 * registers versus stack slots and at what offsets, is decided when the JIT lowers IL to
 * machine code, per the platform ABI.
 */
static void DumpIl(Type t)
{
    foreach (var ctor in t.GetConstructors(System.Reflection.BindingFlags.Public
                                           | System.Reflection.BindingFlags.NonPublic
                                           | System.Reflection.BindingFlags.Instance))
    {
        byte[] il = ctor.GetMethodBody()?.GetILAsByteArray();
        if (il == null) continue;

        var args = new System.Collections.Generic.List<string>();
        for (int i = 0; i < il.Length; i++)
        {
            switch (il[i])
            {
                case 0x02: args.Add("ldarg.0"); break;
                case 0x03: args.Add("ldarg.1"); break;
                case 0x04: args.Add("ldarg.2"); break;
                case 0x05: args.Add("ldarg.3"); break;
                case 0x0E: args.Add($"ldarg.s {il[i + 1]}"); break;
                case 0x0F: args.Add($"ldarga.s {il[i + 1]}"); break;
                case 0xFE when i + 1 < il.Length && il[i + 1] == 0x09:
                    args.Add($"ldarg {il[i + 2] | (il[i + 3] << 8)}"); break;
            }
        }

        Console.WriteLine($"  {t.Name}..ctor  ilBytes={il.Length}  argAccesses=[{string.Join(", ", args)}]");
    }
}

Console.WriteLine("IL argument accesses in the two failing probe constructors:");
foreach (var nested in typeof(engine.physics.AbiProbe).GetNestedTypes(
             System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public))
{
    if (nested.Name.Contains("Prologue")) DumpIl(nested);
}

Console.WriteLine("Android-compiled assembly (metadata only):");
AndroidIl.Dump("C:/Users/timow/coding/github/Karawan/Wuka/bin/Debug/net9.0-android36.0/android-arm64/Joyce.dll");
