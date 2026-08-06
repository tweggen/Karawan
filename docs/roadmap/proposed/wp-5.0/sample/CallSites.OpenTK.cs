// WP-5.0b: the SAME call sites, ported to OpenTK 5.
//
// Every change below was driven by reflection over OpenTK.Graphics 5.0.0-pre.15, not by
// memory. Diff this against sample/CallSites.cs to get the S2b churn number.

using System;
using OpenTK.Graphics.OpenGL;

namespace Karawan.Wp50Sample
{
    public unsafe class CallSites
    {
        // GL is `abstract sealed` in OpenTK - a static class. There is no instance to hold,
        // so the field and its injection point disappear entirely.
        private BufferTarget _bufferType;     // was BufferTargetARB
        private int _handle;                  // was uint: OpenTK's BindBuffer takes Int32

        private int _lastActiveTexture, _lastProgram, _lastArrayBuffer;
        private int[] _lastViewport = new int[4];

        public void SaveState()
        {
            GL.GetInteger(GetPName.ActiveTexture, out _lastActiveTexture);
            GL.GetInteger(GetPName.CurrentProgram, out _lastProgram);
            GL.GetInteger(GetPName.ArrayBufferBinding, out _lastArrayBuffer);

            Span<int> viewport = _lastViewport.AsSpan();
            GL.GetInteger(GetPName.Viewport, viewport);
        }

        public void BindTyped()
        {
            GL.BindBuffer(_bufferType, _handle);
        }

        public void BindViaGlEnum()
        {
            // No GLEnum in OpenTK: the catch-all has to be resolved to a specific type,
            // and which type is a per-call-site judgement.
            GL.BindBuffer(BufferTarget.ArrayBuffer, _lastArrayBuffer);
        }

        public void ConfigureState()
        {
            GL.Enable(EnableCap.CullFace);
            GL.Enable(EnableCap.DepthTest);
            GL.CullFace(TriangleFace.Back);
        }

        public int CreateProgram() => GL.CreateProgram();   // returns int, not uint
    }
}
