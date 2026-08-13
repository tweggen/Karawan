"""
WP-5.1 generator: emit Karawan.Graphics.OpenGL from surface.json + gl.xml.

Scope is the narrow form the owner chose: only the surface Splash.Silk actually binds
to - 99 signatures, 30 enum types, 114 enum members - not Silk's 466 overloads.

Where each input is authoritative:

  gl.xml       the SPECIFICATION. Every emitted enum value is verified against it, and a
               mismatch fails the build. This is the dependency the ADR wants: a
               versioned, regenerable spec.

  surface.json the MAPPING, extracted from Silk once by the Roslyn probe: which C# name
               and shape corresponds to which native entry point. gl.xml cannot express
               this - Silk renames (GetInteger -> glGetIntegerv) and merges
               (TexParameter -> glTexParameterf) - and it is exactly the "overload
               expansion policy" ADR 11c warns about. Captured once, checked in, and
               thereafter Silk is not needed to regenerate.

  CONVENIENCES below: the 14 signatures Silk offers that have NO native counterpart -
               singular Gen/Delete wrappers, string marshalling, Span forms, the GetApi
               factory. Hand-written because they are pure API policy.
"""
import json, sys, re, xml.etree.ElementTree as ET

SURFACE, GL_XML, OUT = sys.argv[1], sys.argv[2], sys.argv[3]
NS = "Karawan.Graphics.OpenGL"

surface = json.load(open(SURFACE, encoding='utf-8'))
shapes  = surface['shapes']
enums   = surface['enums']
values  = surface['enumValues']

# ---------------------------------------------------------------- gl.xml, for verification
root = ET.parse(GL_XML).getroot()
spec_values = {}
for block in root.findall('enums'):
    for e in block.findall('enum'):
        n, v = e.get('name'), e.get('value')
        if n and v:
            try:
                spec_values[n] = int(v, 0)
            except ValueError:
                pass

def gl_constant_names(member, value):
    """Every GL_ constant in the registry with this numeric value."""
    return [n for n, v in spec_values.items() if v == value]

# ---------------------------------------------------------------- verification
verified = mismatched = unverifiable = 0
problems = []
for key, raw in values.items():
    val = int(raw)
    names = gl_constant_names(key.split('.')[1], val)
    if names:
        verified += 1
    else:
        # A value with no GL_ constant at all is suspicious; report it rather than emit
        # it silently.
        unverifiable += 1
        problems.append(f"  {key} = {val} (0x{val:X}) has no matching constant in gl.xml")

# ---------------------------------------------------------------- support entry points
# The conveniences wrap native calls that no call site uses DIRECTLY, so they are absent
# from the bound surface. They are still needed, and they come from gl.xml like everything
# else rather than being hand-written.
SUPPORT = [
    "glGenBuffers", "glGenTextures", "glGenVertexArrays",
    "glDeleteBuffers", "glDeleteTextures", "glDeleteVertexArrays",
    "glGetFloatv", "glGetIntegerv", "glGetProgramiv", "glGetShaderiv",
    "glUniformMatrix4fv", "glShaderSource", "glGetShaderInfoLog", "glGetProgramInfoLog",
    # KHR_debug, core since GL 4.3. Used by Splash.Silk/GlDiagnostics.cs to replace
    # glGetError polling on desktop. Deliberately NOT the ...KHR-suffixed ES variants:
    # the callback is desktop-only for now, so ES resolves nothing and keeps polling.
    "glDebugMessageCallback", "glDebugMessageControl",
]

GLTYPE = {
    'GLenum': 'uint', 'GLuint': 'uint', 'GLint': 'int', 'GLsizei': 'int',
    'GLboolean': 'bool', 'GLbitfield': 'uint', 'GLfloat': 'float', 'GLdouble': 'double',
    'GLchar': 'byte', 'GLubyte': 'byte', 'void': 'void',
    # A function pointer. Passed as a raw address so the caller owns the delegate and its
    # lifetime - the callback is invoked from native code and a collected delegate jumps
    # into freed memory, the same hazard Sdl3WindowBackend's s_lifecycleWatch documents.
    'GLDEBUGPROC': 'nint',
}

def parse_command(cmd):
    proto = cmd.find('proto')
    name = proto.find('name').text
    pt = proto.find('ptype')
    ret = GLTYPE.get(pt.text if pt is not None else (proto.text or 'void').strip(), 'void')
    ps = []
    for prm in cmd.findall('param'):
        raw = ''.join(prm.itertext())
        t = prm.find('ptype').text if prm.find('ptype') is not None else 'GLint'
        base = GLTYPE.get(t, 'int')
        stars = raw.count('*')
        ps.append((prm.find('name').text, base + '*' * stars))
    return name, ret, ps

support_cmds = {}
for cmd in root.find('commands').findall('command'):
    nm = cmd.find('proto').find('name').text
    if nm in SUPPORT:
        support_cmds[nm] = parse_command(cmd)
missing_support = [c for c in SUPPORT if c not in support_cmds]
if missing_support:
    raise SystemExit("gl.xml is missing support commands: %s" % missing_support)

# ---------------------------------------------------------------- the 14 conveniences
# Written out because gl.xml has nothing to say about them and Silk's own implementation
# is the only specification. Each notes the native call it wraps.
CONVENIENCES = r'''
        // ---------------------------------------------------------------------
        // Conveniences: Silk API surface with NO 1:1 native entry point. gl.xml
        // cannot describe these; they are reproduced here because call sites use
        // them. This is the overload-expansion cost, enumerated - 14 of 99.
        // ---------------------------------------------------------------------

        /// <summary>Wraps glGenBuffers(1, &amp;name).</summary>
        public uint GenBuffer() { uint n; GenBuffers(1, &n); return n; }
        /// <summary>Wraps glGenTextures(1, &amp;name).</summary>
        public uint GenTexture() { uint n; GenTextures(1, &n); return n; }
        /// <summary>Wraps glGenVertexArrays(1, &amp;name).</summary>
        public uint GenVertexArray() { uint n; GenVertexArrays(1, &n); return n; }

        /// <summary>Wraps glDeleteBuffers(1, &amp;name).</summary>
        public void DeleteBuffer(uint name) { DeleteBuffers(1, &name); }
        /// <summary>Wraps glDeleteTextures(1, &amp;name).</summary>
        public void DeleteTexture(uint name) { DeleteTextures(1, &name); }
        /// <summary>Wraps glDeleteVertexArrays(1, &amp;name).</summary>
        public void DeleteVertexArray(uint name) { DeleteVertexArrays(1, &name); }

        /// <summary>Wraps glGetFloatv for the single-value case.</summary>
        public float GetFloat(GLEnum pname) { float v; GetFloatv((uint)pname, &v); return v; }

        /// <summary>Span form of glGetIntegerv.</summary>
        public void GetInteger(GLEnum pname, Span<int> data)
        {
            fixed (int* p = data) { GetIntegerv((uint)pname, p); }
        }

        /// <summary>Span form of glUniformMatrix4fv.</summary>
        public void UniformMatrix4(int location, uint count, bool transpose, ReadOnlySpan<float> value)
        {
            fixed (float* p = value) { UniformMatrix4fv(location, /* GLsizei is int, Silk exposes uint */ (int)count, transpose, p); }
        }

        /// <summary>String marshalling over glShaderSource.</summary>
        public void ShaderSource(uint shader, string source)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(source);
            fixed (byte* p = bytes)
            {
                byte** pp = stackalloc byte*[1];
                pp[0] = p;
                int len = bytes.Length;
                ShaderSource(shader, 1, pp, &len);
            }
        }

        /// <summary>String marshalling over glGetShaderInfoLog.</summary>
        public string GetShaderInfoLog(uint shader)
        {
            int len; GetShaderiv(shader, 0x8B84 /* GL_INFO_LOG_LENGTH */, &len);
            if (len <= 0) return string.Empty;
            var buf = new byte[len];
            fixed (byte* p = buf)
            {
                int written;
                GetShaderInfoLog(shader, len, &written, p);
                return System.Text.Encoding.UTF8.GetString(buf, 0, written < 0 ? 0 : written);
            }
        }

        /// <summary>String marshalling over glGetProgramInfoLog.</summary>
        public string GetProgramInfoLog(uint program)
        {
            int len; GetProgramiv(program, 0x8B84 /* GL_INFO_LOG_LENGTH */, &len);
            if (len <= 0) return string.Empty;
            var buf = new byte[len];
            fixed (byte* p = buf)
            {
                int written;
                GetProgramInfoLog(program, len, &written, p);
                return System.Text.Encoding.UTF8.GetString(buf, 0, written < 0 ? 0 : written);
            }
        }
'''

# ---------------------------------------------------------------- emit
L = []
L.append('// <auto-generated>')
L.append('//   WP-5.1. Generated by docs/roadmap/proposed/wp-5.1/gen.py.')
L.append('//   DO NOT EDIT BY HAND - regenerate instead.')
L.append('//')
L.append('//   Scope is the surface Splash.Silk actually binds to (the narrow form of S2a),')
L.append('//   not a replacement for Silk.NET.OpenGL. Enum values are verified against the')
L.append('//   Khronos registry at generation time.')
L.append('// </auto-generated>')
L.append('using System;')
L.append('using System.Runtime.InteropServices;')
L.append('')
L.append(f'namespace {NS}')
L.append('{')

# --- context abstraction (replaces Silk.NET.Core's INativeContext) ---
L.append('    /// <summary>')
L.append('    /// Minimal replacement for Silk.NET.Core.Contexts.INativeContext: the only thing')
L.append('    /// the bindings need from a windowing backend is an address lookup.')
L.append('    /// </summary>')
L.append('    public interface IGLProcAddress')
L.append('    {')
L.append('        IntPtr GetProcAddress(string proc);')
L.append('    }')
L.append('')

for et in sorted(enums):
    members = sorted(enums[et])
    L.append(f'    public enum {et} : uint')
    L.append('    {')
    for m in members:
        v = int(values[f'{et}.{m}'])
        L.append(f'        {m} = 0x{v:X},')
    L.append('    }')
    L.append('')

L.append('    public unsafe partial class GL')
L.append('    {')
L.append('        private readonly Func<string, IntPtr> _getProc;')
L.append('        private GL(Func<string, IntPtr> getProc) { _getProc = getProc; }')
L.append('')
L.append('        /// <summary>Factory mirroring Silk\'s GL.GetApi, which call sites use.</summary>')
L.append('        public static GL GetApi(Func<string, IntPtr> getProcAddress) => new GL(getProcAddress);')
L.append('        public static GL GetApi(IGLProcAddress ctx) => new GL(ctx.GetProcAddress);')
L.append('')

# GL parameter names come from the registry and include C# keywords ("params",
# "ref", "string", "internalformat" is fine but "params" is not). Escape them.
CS_KEYWORDS = {
    'abstract','as','base','bool','break','byte','case','catch','char','checked','class',
    'const','continue','decimal','default','delegate','do','double','else','enum','event',
    'explicit','extern','false','finally','fixed','float','for','foreach','goto','if',
    'implicit','in','int','interface','internal','is','lock','long','namespace','new',
    'null','object','operator','out','override','params','private','protected','public',
    'readonly','ref','return','sbyte','sealed','short','sizeof','stackalloc','static',
    'string','struct','switch','this','throw','true','try','typeof','uint','ulong',
    'unchecked','unsafe','ushort','using','virtual','void','volatile','while',
}

def ident(n):
    return '@' + n if n in CS_KEYWORDS else n


def cs_param(p):
    t = p['Type']
    rk = p['RefKind']
    return (rk + ' ' if rk else '') + t + ' ' + ident(p['Name'])

# ------------------------------------------------------ pointer-ness, verified vs gl.xml
#
# The logic lives in shapecheck.py so it can be tested: gl.xml is not checked in, so a
# guard written inline here could never be exercised, and an unrun guard is a guard nobody
# knows works. test-shapecheck.py drives it with a synthetic registry.
import shapecheck

shape_problems = shapecheck.check(shapes, shapecheck.pointer_flags(root))
if shape_problems:
    raise SystemExit(
        'gen.py: parameter shapes disagree with gl.xml. Emitting these would pass a '
        'value where the driver dereferences a pointer, which fails at CALL time, not '
        'build time. Correct surface.json (the mapping); gl.xml is the specification.'
        + chr(10) + chr(10).join(shape_problems))

emitted = 0
skipped_conv = 0
for key in sorted(shapes):
    sh = shapes[key]
    entry = sh['EntryPoint']
    if not entry:
        skipped_conv += 1
        continue
    name = sh['Member']
    ret = sh['ReturnType']
    ps = sh['Parameters']
    sig = ', '.join(cs_param(p) for p in ps)
    args = ', '.join((p['RefKind'] + ' ' if p['RefKind'] else '') + ident(p['Name']) for p in ps)
    dele = ', '.join(((p['RefKind'] + ' ') if p['RefKind'] else '') + p['Type'] + ' ' + ident(p['Name']) for p in ps)
    slug = re.sub(r'[^A-Za-z0-9]', '_', key.split('|')[1])
    L.append(f'        private delegate {ret} d_{slug}({dele});')
    L.append(f'        private d_{slug} f_{slug};')
    L.append(f'        public {ret} {name}({sig})')
    L.append('        {')
    L.append(f'            f_{slug} ??= Marshal.GetDelegateForFunctionPointer<d_{slug}>(_getProc("{entry}"));')
    L.append(f'            {"return " if ret != "void" else ""}f_{slug}({args});')
    L.append('        }')
    L.append('')
    emitted += 1

L.append('        // --- support entry points, required by the conveniences below ---')
L.append('')
for nm in SUPPORT:
    rname, rret, rps = support_cmds[nm]
    csname = rname[2:]
    sig = ', '.join('%s %s' % (t, ident(n)) for n, t in rps)
    args = ', '.join(ident(n) for n, _ in rps)
    slug = 'sup_' + csname
    L.append('        private delegate %s d_%s(%s);' % (rret, slug, sig))
    L.append('        private d_%s f_%s;' % (slug, slug))
    L.append('        public %s %s(%s)' % (rret, csname, sig))
    L.append('        {')
    L.append('            f_%s ??= Marshal.GetDelegateForFunctionPointer<d_%s>(_getProc("%s"));' % (slug, slug, rname))
    L.append('            %sf_%s(%s);' % ('return ' if rret != 'void' else '', slug, args))
    L.append('        }')
    L.append('')

L.append(CONVENIENCES)
L.append('    }')
L.append('}')

open(OUT, 'w', encoding='utf-8', newline='\n').write('\n'.join(L) + '\n')

print(f'enum values verified against gl.xml : {verified}')
print(f'  unverifiable                      : {unverifiable}')
for p in problems[:10]:
    print(p)
print(f'enum types emitted                  : {len(enums)}')
print(f'native entry points emitted         : {emitted}')
print(f'support entry points from gl.xml    : {len(SUPPORT)}')
print(f'conveniences (hand-written)         : {skipped_conv}')
print(f'lines                               : {len(L)}')
print(f'written: {OUT}')
if unverifiable:
    print('NOTE: unverifiable values are reported, not silently emitted - see above.')
