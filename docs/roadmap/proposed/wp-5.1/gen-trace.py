#!/usr/bin/env python3
"""
Emit Splash.OpenGL/Trace/GLTrace.g.cs - a tracing interposer for the GATE-F comparison.

WHY INTERPOSE AT GetProcAddress

Both GL bindings resolve every entry point through one function. The generated binding
does it at Splash.GL/generated/GL.g.cs ("GetApi(Func<string, IntPtr>)"), and Silk's
GL.GetApi(Func<string, nint>) takes the same shape. IWindowBackend.GetProcAddress is
that single Func.

Hand back a thunk instead of the real pointer and every GL call becomes observable
WITHOUT wrapping either binding - which is the point, because the same tracer then
observes the Silk build and the generated build identically. A difference between two
traces is then a statement about the bindings and nothing else.

WHY THE INPUT IS GL.g.cs AND NOT gl.xml

gl.xml is not checked in; it is fetched at generation time. GL.g.cs IS checked in, and it
already carries, for every entry point, both the native name and the exact signature -
having itself been generated from gl.xml and verified against it. Reading it here means
no external dependency and, more importantly, a tracer that cannot drift from the binding
it is meant to observe.

NORMALISATION

Several C# overloads share one native entry point (ActiveTexture(GLEnum) and
ActiveTexture(TextureUnit) are both glActiveTexture). At the ABI there is one signature,
so types are reduced to their native form - every generated enum is ": uint", pointers
and strings and "out" parameters are all pointer-width, GLboolean is one byte - and the
result is deduplicated per native name. If two overloads of the same entry point reduce
to DIFFERENT native signatures that is a real ambiguity, and this script fails rather
than silently picking one.

USAGE
    python3 gen-trace.py <GL.g.cs> <out GLTrace.g.cs>
"""

import re
import sys
import collections

if len(sys.argv) not in (3, 4):
    sys.exit(__doc__)

SRC, OUT = sys.argv[1], sys.argv[2]
NS = sys.argv[3] if len(sys.argv) > 3 else "Splash.OpenGL"
src = open(SRC, encoding="utf-8").read()

# Enums are emitted as ": uint", so every enum name reduces to uint.
ENUMS = set(re.findall(r'public enum (\w+)\s*:\s*uint', src))

PRIMS = {"void", "uint", "int", "float", "double", "byte", "nuint", "nint"}

# Where Silk offers overloads that reduce to DIFFERENT native signatures, the spec
# decides - not whichever the parser happened to see first. Each entry cites why.
#
# These differ only in the signedness of a 32-bit parameter, so the ABI is identical
# either way; the choice affects how the value is PRINTED in a trace, and a trace should
# print what the spec says the parameter is.
ABI_OVERRIDE = {
    # void glTexImage2D(GLenum target, GLint level, GLint internalformat, GLsizei width,
    #                   GLsizei height, GLint border, GLenum format, GLenum type,
    #                   const void *pixels)
    # internalformat is GLint even though its values are enum constants, and Silk exposes
    # both an InternalFormat overload and an int one.
    "glTexImage2D": ("void", ["uint", "int", "int", "uint", "uint", "int",
                              "uint", "uint", "IntPtr"]),

    # void glUniformMatrix4fv(GLint location, GLsizei count, GLboolean transpose,
    #                         const GLfloat *value)
    # GLsizei is GLint, so count is signed; Silk offers both a uint and an int overload.
    "glUniformMatrix4fv": ("void", ["int", "int", "byte", "IntPtr"]),

    # ---- these two correct a DEFECT in GL.g.cs, they do not merely disambiguate ----
    #
    # void glTexParameterIiv (GLenum target, GLenum pname, const GLint  *params)
    # void glTexParameterIuiv(GLenum target, GLenum pname, const GLuint *params)
    #
    # GL.g.cs declares the third parameter BY VALUE ("int @params"), so the generated
    # binding would hand the driver an integer where it dereferences a pointer. Silk gets
    # it right - its signature is "Int32 ByRef" - and building a thunk from GL.g.cs's
    # version segfaulted the moment Silk called it, inside ImGui's font-texture setup.
    #
    # Recorded as a WP-5.2 blocker rather than quietly worked around here: this is a live
    # defect in the binding WP-5.2 intends to swap onto, and it is exactly the class of
    # thing GATE-F exists to catch. The likely origin is the Roslyn probe flattening
    # Silk's "in int" to "int" when it captured surface.json.
    "glTexParameterIiv":  ("void", ["uint", "uint", "IntPtr"]),
    "glTexParameterIuiv": ("void", ["uint", "uint", "IntPtr"]),
}


def native_type(t):
    """Function-pointer type -> the type the thunk declares."""
    t = t.strip()
    if t.endswith("*"):
        return "IntPtr"
    if t in ENUMS:
        return "uint"            # every generated enum is ": uint"
    if t in PRIMS:
        return t
    # An unknown type is a generator bug, not something to guess at.
    raise SystemExit(f"gen-trace: unknown type {t!r} - teach native_type() about it")


# The function-pointer type IS the ABI signature - bool is already byte, string already
# byte*, byref already T* - because the binding writes those conversions out rather than
# leaving them to a marshaller. Parsing it is therefore both simpler and more faithful than
# reading C# delegate declarations was.
blocks = re.findall(r'\(delegate\* unmanaged<([^>]*)>\)_getProc\("(\w+)"\)', src)

sigs = {}      # native name -> (ret, [param types])
conflicts = {}
overloads = collections.Counter()
for typelist, native in blocks:
    parts = [t.strip() for t in typelist.split(",")]
    ps = [native_type(t) for t in parts[:-1]]
    r = native_type(parts[-1])
    overloads[native] += 1
    if native in ABI_OVERRIDE:
        sigs[native] = ABI_OVERRIDE[native]
        continue
    if native in sigs and sigs[native] != (r, ps):
        conflicts.setdefault(native, [sigs[native]]).append((r, ps))
    sigs[native] = (r, ps)

if conflicts:
    # All of them at once: fixing these one generator run at a time is miserable.
    msg = ["gen-trace: overloads reduce to different native signatures.",
           "Add a cited ABI_OVERRIDE entry for each, or teach native_type().", ""]
    for n, variants in sorted(conflicts.items()):
        msg.append(f"  {n}")
        for v in variants:
            msg.append(f"      {v}")
    sys.exit(chr(10).join(msg))

if not sigs:
    sys.exit("gen-trace: parsed no entry points - has GL.g.cs changed shape?")


# ------------------------------------------------------------- object-name canonicalising
#
# GL object names are assigned by the DRIVER in allocation order, so the same logical
# buffer is 24 in one run and 31 in the next. Measured: of ~6500 calls in a shared-anchor
# frame, 595 differed only in numeric arguments, and object names were most of them.
# Recording the raw number guarantees a diff that says nothing.
#
# Each entry below names the parameter positions that carry an object name, and the KIND
# it belongs to - a buffer 24 and a texture 24 are different objects and must not share a
# numbering space. Positions are indices into the native parameter list.
CANON = {
    "glBindBuffer":            {1: "buffer"},
    "glBindBufferBase":        {2: "buffer"},
    "glBindVertexArray":       {0: "vao"},
    "glBindTexture":           {1: "texture"},
    "glBindFramebuffer":       {1: "framebuffer"},
    "glBindRenderbuffer":      {1: "renderbuffer"},
    "glUseProgram":            {0: "program"},
    "glAttachShader":          {0: "program", 1: "shader"},
    "glCompileShader":         {0: "shader"},
    "glLinkProgram":           {0: "program"},
    "glDeleteProgram":         {0: "program"},
    "glDeleteShader":          {0: "shader"},
    "glShaderSource":          {0: "shader"},
    "glGetShaderiv":           {0: "shader"},
    "glGetProgramiv":          {0: "program"},
    "glGetShaderInfoLog":      {0: "shader"},
    "glGetProgramInfoLog":     {0: "program"},
    "glGetUniformLocation":    {0: "program"},
    "glGetAttribLocation":     {0: "program"},
    "glGetUniformBlockIndex":  {0: "program"},
    "glUniformBlockBinding":   {0: "program"},
    "glFramebufferTexture2D":  {3: "texture"},
    "glFramebufferRenderbuffer": {3: "renderbuffer"},
}


def fmt_arg(t, name, native=None, idx=None):
    """How an argument is rendered into the trace line."""
    if native is not None and native in CANON and idx in CANON[native] and t == "uint":
        return f'_canon("{CANON[native][idx]}", {name})'
    if t == "IntPtr":
        # NEVER the address. It is allocation-dependent, so recording it would guarantee
        # a diff between any two runs and prove nothing. Presence is what is stable.
        return f'({name} == IntPtr.Zero ? "null" : "ptr")'
    if t == "float":
        return f'{name}.ToString("G9", CultureInfo.InvariantCulture)'
    if t == "double":
        return f'{name}.ToString("G17", CultureInfo.InvariantCulture)'
    return f"{name}"


L = []
w = L.append
w("// <auto-generated>")
w("//   Generated by docs/roadmap/proposed/wp-5.1/gen-trace.py from")
w("//   Splash.GL/generated/GL.g.cs. DO NOT EDIT BY HAND - regenerate instead.")
w("// </auto-generated>")
w("using System;")
w("using System.Collections.Generic;")
w("using System.Globalization;")
w("using System.Runtime.InteropServices;")
w("")
# NOT a "Splash.OpenGL.Trace" sub-namespace, however natural that reads: Splash.OpenGL files
# do "using static engine.Logger;", and a namespace segment named Trace shadows the
# Logger.Trace() method throughout the assembly. The file still lives in Trace/.
w(f"namespace {NS};")
w("")
w("/// <summary>")
w("/// Records every GL call, by interposing on the one function both bindings use to")
w("/// resolve entry points. See gen-trace.py for why this seam and not a wrapper.")
w("/// </summary>")
w("public static class GLTrace")
w("{")
w("    private static readonly object _lo = new();")
w("    private static List<string>? _sink;")
w("")
w("    /// <summary>True while calls are being recorded. Checked on every GL call, so it")
w("    /// is a plain field read and nothing more.</summary>")
w("    public static bool IsRecording;")
w("")
w("    /// <summary>Entry points the binding asked for that this tracer does not cover.")
w("    /// They still work - the real pointer is handed through - but they are invisible,")
w("    /// so a non-empty list means the comparison has a hole in it.</summary>")
w("    public static readonly List<string> Untraced = new();")
w("")
w("    public static int TracedCount => _thunks.Count;")
w("")
w("    private static Func<string, IntPtr>? _real;")
w("    private static readonly Dictionary<string, IntPtr> _thunks = new();")
w("    private static readonly List<Delegate> _keepAlive = new();")
w("")
w("    public static void Begin()")
w("    {")
w("        // Ordinals restart per capture, so each trace is self-contained. Otherwise the")
w("        // numbering of the second anchor would depend on what the first one saw, and two")
w("        // runs that captured different anchor sets could never be compared.")
w("        lock (_lo) { _sink = new List<string>(65536); _canonMaps.Clear(); }")
w("        IsRecording = true;")
w("    }")
w("")
w("    public static IReadOnlyList<string> End()")
w("    {")
w("        IsRecording = false;")
w("        lock (_lo) { var s = _sink ?? new List<string>(); _sink = null; return s; }")
w("    }")
w("")
w("    /// <summary>")
w("    /// Driver-assigned object name -> a stable ordinal, per KIND.")
w("    /// </summary>")
w("    /// <remarks>")
w("    /// GL hands out object names in allocation order, so the same logical buffer is 24")
w("    /// in one run and 31 in the next. Recording the raw value guarantees a diff that")
w("    /// says nothing about the binding. Kinds are separate numbering spaces because a")
w("    /// buffer 24 and a texture 24 are unrelated objects.")
w("    /// </remarks>")
w("    private static readonly Dictionary<string, Dictionary<uint, int>> _canonMaps = new();")
w("")
w("    private static string _canon(string kind, uint raw)")
w("    {")
w("        // 0 is not an object, it is 'unbind'. It must stay 0 or every unbind reads as")
w("        // a distinct object and the traces diverge on nothing.")
w("        if (raw == 0) return kind + \"#none\";")
w("")
w("        if (!_canonMaps.TryGetValue(kind, out var m))")
w("        {")
w("            m = new Dictionary<uint, int>();")
w("            _canonMaps[kind] = m;")
w("        }")
w("")
w("        if (!m.TryGetValue(raw, out int ordinal))")
w("        {")
w("            ordinal = m.Count;")
w("            m[raw] = ordinal;")
w("        }")
w("")
w("        return kind + \"#\" + ordinal;")
w("    }")
w("")
w("    private static void _rec(string line)")
w("    {")
w("        lock (_lo) { _sink?.Add(line); }")
w("    }")
w("")
w("    /// <summary>")
w("    /// Wrap a backend's GetProcAddress. Entry points this tracer knows are answered")
w("    /// with a thunk; anything else is passed straight through and noted in")
w("    /// <see cref=\"Untraced\"/>.")
w("    /// </summary>")
w("    public static Func<string, IntPtr> Wrap(Func<string, IntPtr> real)")
w("    {")
w("        _real = real;")
w("        _register();")
w("        return _lookup;")
w("    }")
w("")
w("    /// <summary>")
w("    /// Trace with NO driver behind it: every entry point gets a thunk that records and")
w("    /// returns default without forwarding.")
w("    /// </summary>")
w("    /// <remarks>")
w("    /// This is what lets the two bindings be compared without a GL context, without a")
w("    /// window, and without arguments that would be valid to execute - the question")
w("    /// being asked is which NATIVE call a given C# call produces, which is answered")
w("    /// before the driver is ever involved.")
w("    /// </remarks>")
w("    public static Func<string, IntPtr> WrapRecordOnly() => Wrap(_ => IntPtr.Zero);")
w("")
w("    private static IntPtr _lookup(string name)")
w("    {")
w("        if (_thunks.TryGetValue(name, out IntPtr thunk)) return thunk;")
w("        lock (_lo) { if (!Untraced.Contains(name)) Untraced.Add(name); }")
w("        return _real!(name);")
w("    }")
w("")

for native in sorted(sigs):
    ret, ps = sigs[native]
    names = [f"p{i}" for i in range(len(ps))]
    decl = ", ".join(f"{t} {n}" for t, n in zip(ps, names))
    call = ", ".join(names)
    parts = " + \",\" + ".join(
        fmt_arg(t, n, native, i) for i, (t, n) in enumerate(zip(ps, names))) or '""'
    w(f"    [UnmanagedFunctionPointer(CallingConvention.Winapi)]")
    w(f"    private delegate {ret} D_{native}({decl});")
    w(f"    private static D_{native}? _r_{native};")
    w(f"    private static {ret} _t_{native}({decl})")
    w("    {")
    w(f'        if (IsRecording) _rec("{native}(" + {parts} + ")");')
    # Tolerating a null real pointer is what makes RECORD-ONLY mode possible: the
    # differential harness needs no GL context, no driver and no valid arguments,
    # because nothing is forwarded - only observed.
    if ret == "void":
        w(f"        if (null != _r_{native}) _r_{native}({call});")
    else:
        w(f"        return null != _r_{native} ? _r_{native}({call}) : default;")
    w("    }")
    w("")

w("    private static void _register()")
w("    {")
w("        _thunks.Clear();")
w("        _keepAlive.Clear();")
for native in sorted(sigs):
    w(f"        _bind(\"{native}\", p => _r_{native} = "
      f"Marshal.GetDelegateForFunctionPointer<D_{native}>(p), new D_{native}(_t_{native}));")
w("    }")
w("")
w("    private static void _bind(string name, Action<IntPtr> setReal, Delegate thunk)")
w("    {")
w("        IntPtr real = _real!(name);")
w("        // A null real pointer is legitimate in two different situations, and the thunk")
w("        // is registered either way:")
w("        //   - RECORD-ONLY mode, where there is deliberately no driver behind this;")
w("        //   - a driver that does not export the call (a GLES context missing a")
w("        //     desktop-only entry point), where recording the attempt is still useful.")
w("        // The thunk checks for null before forwarding, so neither case jumps to 0.")
w("        if (real != IntPtr.Zero) setReal(real);")
w("        _keepAlive.Add(thunk);")
w("        _thunks[name] = Marshal.GetFunctionPointerForDelegate(thunk);")
w("    }")
w("}")

open(OUT, "w", encoding="utf-8", newline="\n").write("\n".join(L) + "\n")

multi = {n: c for n, c in overloads.items() if c > 1}
print(f"entry points traced          : {len(sigs)}")
print(f"  of which multi-overload    : {len(multi)} (reduced to one native signature each)")
print(f"written                      : {OUT}")
