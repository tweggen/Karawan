// WP-5.0 call-site sample.
//
// Every statement below is copied VERBATIM from Splash.Silk - GlStateSaver.cs,
// BufferObject.cs, SilkThreeD.cs, SkProgramEntry.cs - narrowed to the entry points the
// prototype generator emits. Only the surrounding scaffolding (class, fields) is written
// for the harness.
//
// The one line that differs between the baseline and candidate builds is the single
// `using <namespace>;` injected at the top by the harness - exactly the line a real
// migration would touch. Everything below is byte-identical, so a diff measures call-site
// churn and nothing else.
//
// Two harness lessons already paid for, recorded so they are not re-learned:
//   - `using GLNS = <ns>;` plus `using GL = GLNS.GL;` is NOT legal C#: a using-alias
//     cannot reference another using-alias.
//   - the generated namespace must not be `Karawan.GL`, because it shadows the `GL`
//     class name. Silk avoids this with namespace Silk.NET.OpenGL + class GL.

using System;

namespace Karawan.Wp50Sample
{
    public unsafe class CallSites
    {
        private GL _gl;
        private BufferTargetARB _bufferType;
        private uint _handle;

        private int _lastActiveTexture, _lastProgram, _lastArrayBuffer;
        private int[] _lastViewport = new int[4];

        // GlStateSaver.cs:43-57 - GLEnum plus BOTH out-int and Span<int> forms of GetInteger
        public void SaveState()
        {
            _gl.GetInteger(GLEnum.ActiveTexture, out _lastActiveTexture);
            _gl.GetInteger(GLEnum.CurrentProgram, out _lastProgram);
            _gl.GetInteger(GLEnum.ArrayBufferBinding, out _lastArrayBuffer);

            Span<int> viewport = _lastViewport.AsSpan();
            _gl.GetInteger(GLEnum.Viewport, viewport);
        }

        // BufferObject.cs:28 - the TYPED enum form of BindBuffer
        public void BindTyped()
        {
            _gl.BindBuffer(_bufferType, _handle);
        }

        // GlStateSaver.cs:84 - the GLEnum form of the SAME entry point.
        // This is ADR 11c's "dual typed-enum / GLEnum surface", in our own code.
        public void BindViaGlEnum()
        {
            _gl.BindBuffer(GLEnum.ArrayBuffer, (uint)_lastArrayBuffer);
        }

        // SilkThreeD.cs:1099-1105
        public void ConfigureState()
        {
            _gl.Enable(EnableCap.CullFace);
            _gl.Enable(EnableCap.DepthTest);
            _gl.CullFace(TriangleFace.Back);
        }

        // SkProgramEntry - uint-returning entry point
        public uint CreateProgram() => _gl.CreateProgram();
    }
}
