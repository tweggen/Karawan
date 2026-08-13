"""
Verify that surface.json's parameter shapes agree with gl.xml.

Extracted from gen.py so it can be TESTED. gl.xml is not checked in - it is fetched at
generation time - so the guard inside gen.py could not be exercised here, and an unrun
guard is a guard nobody knows works. test-shapecheck.py drives this with a synthetic
registry instead.

WHAT IT CATCHES, AND WHY IT EXISTS

surface.json is the MAPPING, captured once from Silk by a Roslyn probe; gl.xml is the
SPECIFICATION. Where they disagree about whether a parameter is a pointer, the
specification wins - and they did disagree. The probe recorded Silk's "in int" as a plain
"int" for glTexParameterIiv and glTexParameterIuiv, so the generator emitted a delegate
passing an integer BY VALUE where the driver dereferences a pointer.

Nothing failed at build time. It segfaulted on first use, inside ImGui's font-texture
setup, and was found only because the GATE-F tracer built thunks from those signatures.

gen.py already verified enum VALUES against gl.xml. It did not verify parameter SHAPE, and
shape is the half that corrupts memory rather than merely drawing the wrong thing.
"""


def pointer_flags(root):
    """gl.xml -> {entry point: [is_pointer per parameter]}."""
    out = {}
    for cmd in root.find('commands').findall('command'):
        name = cmd.find('proto').find('name').text
        out[name] = ['*' in ''.join(p.itertext()) for p in cmd.findall('param')]
    return out


def passes_pointer(param):
    """Does this surface.json parameter reach the driver as a pointer?"""
    # "in"/"ref"/"out" all marshal as a pointer, as does an explicit "*". A string is
    # marshalled as a pointer to its first character.
    return bool(param.get('RefKind')) or param['Type'].endswith('*') or param['Type'] == 'string'


def check(shapes, flags):
    """
    Compare every entry-point-backed shape against the registry.

    Returns a list of human-readable problems; empty means agreement. Shapes whose entry
    point is absent from the registry are SKIPPED rather than reported - gl.xml carries
    only what it carries, and a missing command is a different complaint from a wrong one.
    """
    problems = []
    for key in sorted(shapes):
        shape = shapes[key]
        entry = shape.get('EntryPoint')
        if not entry or entry not in flags:
            continue

        spec = flags[entry]
        got = shape['Parameters']
        if len(spec) != len(got):
            problems.append(
                f"  {entry}: gl.xml has {len(spec)} parameters, surface.json has {len(got)}")
            continue

        for i, (is_ptr, param) in enumerate(zip(spec, got)):
            if is_ptr and not passes_pointer(param):
                problems.append(
                    f"  {entry} param {i} ({param['Name']}): gl.xml says POINTER, "
                    f"surface.json says by-value {param['Type']!r}")
            elif not is_ptr and param['Type'].endswith('*'):
                problems.append(
                    f"  {entry} param {i} ({param['Name']}): gl.xml says by-value, "
                    f"surface.json says pointer {param['Type']!r}")

    return problems
