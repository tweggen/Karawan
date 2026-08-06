"""
Survey the actual OpenGL surface Splash.Silk depends on.

WP-5.0 needs real numbers, not the plan's estimate of "80 entry points and ~10 enum
types". Everything downstream - generator size, OpenTK rename churn - is a function of
this surface, so measure it first.
"""
import re, sys, os, json, collections

root = sys.argv[1]
src = os.path.join(root, 'Splash.Silk')

files = [os.path.join(src, f) for f in sorted(os.listdir(src)) if f.endswith('.cs')]

# Calls made on a GL instance. The codebase reaches GL through several receivers:
#   _gl.Foo(...)   gl.Foo(...)   _getGL().Foo(...)   GetGL().Foo(...)
call_re = re.compile(r'\b(?:_gl|gl|_getGL\(\)|GetGL\(\))\s*\.\s*([A-Z][A-Za-z0-9_]*)\s*[(<]')

# Silk.NET.OpenGL types named explicitly, e.g. GLEnum.Foo, BufferTargetARB.ArrayBuffer.
# Collected as <Type>.<Member> so we can separate enum types from their members.
type_re = re.compile(r'\b([A-Z][A-Za-z0-9_]*)\s*\.\s*([A-Z][A-Za-z0-9_]*)\b')

# Types that are ours or BCL, not Silk GL. Filtering these out keeps the enum count honest.
NOT_GL = {
    'Console','Math','MathF','String','Convert','Array','Buffer','File','Path','Encoding',
    'Vector2','Vector3','Vector4','Matrix4x4','Quaternion','Marshal','MemoryMarshal','Debug',
    'Trace','Logger','I','Engine','Platform','SilkThreeD','SilkRenderer','GLCheck','Flags',
    'GLAnimBuffers','TimeSpan','DateTime','Task','Thread','Interlocked','Monitor','Environment',
    'Type','Object','Enum','Guid','Stopwatch','ImGui','ImGuiNET','SkTexture','SkTextureEntry',
    'SkMeshEntry','SkMaterialEntry','SkProgramEntry','SkAnimationsEntry','SkRenderbufferEntry',
    'SkSingleShaderEntry','InstanceDesc','Mesh','Material','Texture','RenderFrame','MeshBatch',
    'MaterialBatch','CameraOutput','LogicalRenderer','TextureGenerator','TextureManager',
    'ShaderManager','ModuleFactory','GlobalSettings','Vector2D','Vector3D','Vector4D','Silk',
    'System','Splash','Joyce','SilkRenderState','GlStateSaver','BufferObject','VertexArrayObject',
    'LightShaderUseCase','MeshGenerator','SilkFrame','PreviewHelper','ArgumentNullException',
    'InvalidOperationException','Exception','List','Dictionary','Nullable','Span','ReadOnlySpan',
    'IntPtr','UIntPtr','GC','Assembly','Activator','StringComparison','SortedDictionary',
    'SilkTextureChannelState','DelegateProcContext','StringName',
}

calls = collections.Counter()
call_sites = collections.Counter()
types = collections.defaultdict(set)
per_file = {}

for f in files:
    text = open(f, encoding='utf-8', errors='replace').read()
    # strip // line comments (crude but adequate: no // inside our string literals)
    stripped = '\n'.join(re.sub(r'//.*$', '', ln) for ln in text.split('\n'))

    fc = call_re.findall(stripped)
    for c in fc:
        calls[c] += 1
    call_sites[os.path.basename(f)] = len(fc)

    for t, m in type_re.findall(stripped):
        if t in NOT_GL or t.startswith('Sk') or t.endswith('Exception'):
            continue
        types[t].add(m)
    per_file[os.path.basename(f)] = len(fc)

# An enum type is one we reference by several members, or one with a GL-ish name.
GLISH = re.compile(r'(ARB|EXT|GLEnum|Target|Cap|Mode|Format|Type|Mask|Usage|Parameter|Name|Flags|Func|Filter|Wrap|Attachment|Buffer|Shader|Program|Texture|Face|Direction|Op|Factor|Hint)')
enum_types = {t: ms for t, ms in types.items() if GLISH.search(t) or len(ms) > 1}

print('=' * 72)
print('GL SURFACE ACTUALLY USED BY Splash.Silk')
print('=' * 72)
print(f'files scanned              : {len(files)}')
print(f'distinct GL entry points   : {len(calls)}')
print(f'total GL call sites        : {sum(calls.values())}')
print(f'distinct GL-ish enum types : {len(enum_types)}')
print(f'distinct enum members used : {sum(len(v) for v in enum_types.values())}')
print()
print('--- entry points by call count ---')
for name, n in calls.most_common():
    print(f'  {n:4d}  {name}')
print()
print('--- enum types and the members actually referenced ---')
for t in sorted(enum_types):
    ms = sorted(enum_types[t])
    print(f'  {t}  ({len(ms)})')
    print(f'      {", ".join(ms)}')
print()
print('--- call sites per file ---')
for f, n in sorted(per_file.items(), key=lambda kv: -kv[1]):
    if n:
        print(f'  {n:4d}  {f}')

json.dump(
    {'entry_points': dict(calls), 'enum_types': {k: sorted(v) for k, v in enum_types.items()},
     'per_file': per_file},
    open(os.path.join(os.path.dirname(__file__), 'gl-survey.json'), 'w'), indent=2)
print()
print('written: gl-survey.json')
