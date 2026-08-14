"""
Emit the body of one bound entry point, dispatching through an UNMANAGED FUNCTION POINTER.

WHY NOT Marshal.GetDelegateForFunctionPointer

Three reasons, in increasing order of how much they actually cost:

  1. A marshalling stub per call, for a surface issuing ~2,650 GL calls a frame. Real but
     small - well under 1% of a core, and NOT the reason this changed. The earlier claim
     that dispatch was "the performance question" was wrong: glGetError polling was about a
     third of all GL traffic, and that is fixed elsewhere.

  2. Default marshalling makes silent decisions. C# bool marshals as a 4-byte Win32 BOOL
     while GLboolean is one byte; that needed [MarshalAs(UnmanagedType.U1)] to correct.
     Function pointers have no marshaller, so every conversion is written down here instead
     of being inherited from a default nobody chose.

  3. THE ONE THAT FORCED IT: a delegate-based binding cannot be traced. .NET returns the
     ORIGINAL delegate from GetDelegateForFunctionPointer when the pointer came from
     GetFunctionPointerForDelegate, so the cast to the binding's own delegate type throws.
     The GATE-F tracer interposes exactly there. It worked against Silk - which dispatches
     through function pointers - and stopped working against this binding the moment WP-5.2
     swapped the renderer onto it.

CALLING CONVENTION

delegate* unmanaged<> takes the platform default. On x64 there is only one convention, and
on arm64 likewise, so this is correct for every target here (win-x64, linux-x64, osx-arm64,
android-arm64). A 32-bit x86 target would need unmanaged[Stdcall], because OpenGL's APIENTRY
is __stdcall there.

WHAT NEEDS CONVERTING

Of 85 bound shapes, 69 pass straight through: primitives, enums (blittable, uint-backed) and
pointers are all legal in a function-pointer signature. Only these are not:

  bool          -> byte      GLboolean is one byte; the conversion is explicit both ways
  string        -> byte*     UTF-8, null-terminated, pinned for the call
  in/ref/out T  -> T*        a local is taken by address; out is copied back afterwards
"""

import re


def _fp_type(type_name, ref_kind):
    """The type as it appears in the function-pointer signature."""
    if ref_kind:
        # bool by reference is still a GLboolean behind the pointer: byte*, not bool*.
        base = 'byte' if type_name == 'bool' else type_name
        return base + '*'
    if type_name == 'bool':
        return 'byte'
    if type_name == 'string':
        return 'byte*'
    return type_name


def _fp_ret(ret):
    if ret == 'bool':
        return 'byte'
    if ret == 'string':
        return 'byte*'
    return ret


def emit(L, indent, name, ret, params, entry, slug, ident):
    """
    Append the field and public method for one entry point.

    params: list of dicts with Name / Type / RefKind, as surface.json records them.
    ident:  the caller's C#-keyword escaper.
    """
    i = ' ' * indent

    fp_types = [_fp_type(p['Type'], p['RefKind']) for p in params]
    fp_sig = ', '.join(fp_types + [_fp_ret(ret)])
    fp_decl = f'delegate* unmanaged<{fp_sig}>'

    # Public signature is UNCHANGED - the whole point is that call sites do not move.
    sig = ', '.join(
        ((p['RefKind'] + ' ') if p['RefKind'] else '') + p['Type'] + ' ' + ident(p['Name'])
        for p in params)

    L.append(f'{i}private {fp_decl} f_{slug};')
    L.append(f'{i}public {ret} {name}({sig})')
    L.append(i + '{')
    L.append(f'{i}    if (f_{slug} == null) f_{slug} = ({fp_decl})_getProc("{entry}");')

    pre, post, args = [], [], []
    fixups = []          # (variable, byte[] expression) for pinned strings

    for n, p in enumerate(params):
        var = ident(p['Name'])
        t, rk = p['Type'], p['RefKind']

        if rk:
            # A local taken by address. `out` starts at default and is copied back; `in`
            # and `ref` start from the caller's value. bool needs the byte dance as well.
            tmp = f'_a{n}'
            if t == 'bool':
                init = '0' if rk == 'out' else f'(byte)({var} ? 1 : 0)'
                pre.append(f'byte {tmp} = {init};')
                if rk == 'out':
                    post.append(f'{var} = {tmp} != 0;')
                args.append(f'&{tmp}')
            else:
                init = 'default' if rk == 'out' else var
                pre.append(f'{t} {tmp} = {init};')
                if rk == 'out':
                    post.append(f'{var} = {tmp};')
                args.append(f'&{tmp}')
        elif t == 'bool':
            args.append(f'(byte)({var} ? 1 : 0)')
        elif t == 'string':
            buf, ptr = f'_s{n}', f'_p{n}'
            # Null-terminated: GL reads a C string, and Encoding.UTF8.GetBytes does not
            # add the terminator.
            pre.append(f'var {buf} = System.Text.Encoding.UTF8.GetBytes({var} + "\\0");')
            fixups.append((ptr, buf))
            args.append(ptr)
        else:
            args.append(var)

    call = f'f_{slug}({", ".join(args)})'
    if ret == 'bool':
        result = f'{call} != 0'
    elif ret == 'string':
        result = f'Marshal.PtrToStringUTF8((IntPtr){call}) ?? string.Empty'
    else:
        result = call

    body = []
    body.extend(pre)

    inner = []
    if ret == 'void':
        inner.append(f'{result};')
        inner.extend(post)
    elif post:
        # A value AND out-parameters: the result has to be held while they are copied back.
        inner.append(f'var _r = {result};')
        inner.extend(post)
        inner.append('return _r;')
    else:
        inner.append(f'return {result};')

    if fixups:
        opens = ' '.join(f'fixed (byte* {ptr} = {buf})' for ptr, buf in fixups)
        body.append(opens)
        body.append('{')
        body.extend('    ' + x for x in inner)
        body.append('}')
    else:
        body.extend(inner)

    for line in body:
        L.append(f'{i}    {line}')

    L.append(i + '}')
    L.append('')
