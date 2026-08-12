using System;
using System.Globalization;
using System.IO;
using System.Numerics;
using engine;

namespace Splash;

/**
 * Per-frame content digest, for answering ONE question: does this game replay
 * identically between two runs of the same build?
 *
 * WHY THIS EXISTS BEFORE ANY GL TRACING
 *
 * GATE-F wants to prove that swapping the GL binding changes nothing observable. The
 * plan for that is to compare the stream of GL calls a real session issues, before and
 * after the swap. That comparison is worthless unless the SESSION itself is reproducible:
 * if two runs of the same unmodified build already differ, a diff after the swap says
 * nothing about the swap.
 *
 * Measurement said not to assume it. Mesh-batch counts were seen swinging 103 <-> 140
 * between adjacent samples of a single run - NPCs spawning, despawning and being culled -
 * so frame-for-frame reproducibility is a real question, not a formality.
 *
 * WHAT IS HASHED, AND WHY NOT GL CALLS
 *
 * The RenderFrame's draw-relevant CONTENT, not the GL calls it will produce. Content is
 * upstream of the binding, so it is identical across bindings by construction - which is
 * exactly right here, because this measures the SESSION's determinism and nothing else.
 * The GL-level comparison is a separate instrument, and building it before knowing this
 * answer would be building on an assumption.
 *
 * Deliberately excluded: StartCollectTime and EndCollectTime. They are wall-clock and
 * would differ on every run by construction, which would answer "is a clock a clock"
 * rather than the question asked.
 *
 * Enabled by the global setting "debug.option.frameDigest", whose value is the output
 * path. Off, it costs one string comparison at startup and nothing per frame.
 */
public static class FrameDigest
{
    private static readonly object _lo = new();
    private static StreamWriter? _writer;
    private static bool _isChecked;
    private static bool _isEnabled;

    public static bool IsEnabled
    {
        get
        {
            if (!_isChecked)
            {
                lock (_lo)
                {
                    if (!_isChecked)
                    {
                        string path = GlobalSettings.Get("debug.option.frameDigest");
                        _isEnabled = !string.IsNullOrEmpty(path);
                        if (_isEnabled)
                        {
                            _writer = new StreamWriter(path, append: false) { AutoFlush = true };
                            _writer.WriteLine("# frame digest: frameNumber hash parts meshes materials instances anims");
                        }

                        _isChecked = true;
                    }
                }
            }

            return _isEnabled;
        }
    }

    private const ulong FnvOffset = 14695981039346656037UL;
    private const ulong FnvPrime = 1099511628211UL;

    private static void _mix(ref ulong h, ulong v)
    {
        for (int i = 0; i < 8; ++i)
        {
            h ^= (v >> (i * 8)) & 0xFF;
            h *= FnvPrime;
        }
    }

    /** Bit pattern, not value: two floats that print the same must still differ if they are. */
    private static void _mix(ref ulong h, float f)
        => _mix(ref h, (ulong)(uint)BitConverter.SingleToInt32Bits(f));

    private static void _mix(ref ulong h, in Matrix4x4 m)
    {
        _mix(ref h, m.M11); _mix(ref h, m.M12); _mix(ref h, m.M13); _mix(ref h, m.M14);
        _mix(ref h, m.M21); _mix(ref h, m.M22); _mix(ref h, m.M23); _mix(ref h, m.M24);
        _mix(ref h, m.M31); _mix(ref h, m.M32); _mix(ref h, m.M33); _mix(ref h, m.M34);
        _mix(ref h, m.M41); _mix(ref h, m.M42); _mix(ref h, m.M43); _mix(ref h, m.M44);
    }

    public static void Record(RenderFrame renderFrame)
    {
        if (!IsEnabled || null == _writer) return;

        var fs = renderFrame.FrameStats;

        ulong h = FnvOffset;
        _mix(ref h, (ulong)renderFrame.RenderParts.Count);
        _mix(ref h, (ulong)fs.NMeshes);
        _mix(ref h, (ulong)fs.NMaterials);
        _mix(ref h, (ulong)fs.NInstances);
        _mix(ref h, (ulong)fs.NAnimations);
        _mix(ref h, (ulong)fs.NTriangles);
        _mix(ref h, (ulong)renderFrame.ListAmbientLights.Count);
        _mix(ref h, (ulong)renderFrame.ListDirectionalLights.Count);
        _mix(ref h, (ulong)renderFrame.ListPointLights.Count);

        foreach (var part in renderFrame.RenderParts)
        {
            var co = part.CameraOutput;
            if (null == co) continue;

            _mix(ref h, (ulong)co.CameraMask);
            _mix(ref h, co.TransformToWorld);
        }

        lock (_lo)
        {
            _writer.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "{0} {1:x16} {2} {3} {4} {5} {6}",
                renderFrame.FrameNumber, h, renderFrame.RenderParts.Count,
                fs.NMeshes, fs.NMaterials, fs.NInstances, fs.NAnimations));
        }
    }
}
