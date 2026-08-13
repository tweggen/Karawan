using System;
using System.Collections.Generic;
using System.IO;
using engine;
using static engine.Logger;

namespace Splash.Silk;

/**
 * Arms GL tracing for exactly the frames that render a nominated ANCHOR state, and writes
 * the resulting call list out.
 *
 * WHY AN ANCHOR AND NOT A FRAME NUMBER
 *
 * Measured over four runs of the same unmodified build, only 13% of frames matched
 * between two runs; the first divergence was at frame 3 and structure diverged by frame
 * 12. Fixed dt did not help, because scene sequencing depends on wall clock. So "frame
 * 400 of run A" and "frame 400 of run B" are not the same picture and comparing their GL
 * calls would prove nothing.
 *
 * The same measurement found the way round it: 74% of consecutive frames within a run are
 * byte-identical - the game settles into quiescent plateaus - and those plateaus RECUR
 * ACROSS RUNS. Two independent runs shared 70 distinct rendered states, 61 of them
 * drawing more than 50 mesh batches.
 *
 * So the comparison is anchored on CONTENT. When both builds reach the same rendered
 * state, whenever each happens to get there, their GL call streams must agree - and a
 * difference is attributable to the binding and nothing else.
 *
 * SETTINGS
 *
 *   debug.option.glTraceAnchor  the digest to trace, from scripts/find-gl-anchors.py.
 *                               "first" traces the first frame with at least
 *                               glTraceMinMeshes batches, which is how you get a starting
 *                               digest before you have one.
 *   debug.option.glTraceOut     output path. Default gl-trace.txt.
 *   debug.option.glTraceMinMeshes  for "first". Default 50, because an anchor drawing two
 *                               meshes exercises almost none of the GL surface.
 */
public static class GlTraceAnchor
{
    private static bool _isArmed;
    private static bool _hasWritten;
    private static string _anchor = "";
    private static string _outPath = "gl-trace.txt";
    private static int _minMeshes = 50;

    /** Call once, after GlobalSettings are loaded. No-op unless an anchor is configured. */
    public static void Install()
    {
        _anchor = GlobalSettings.Get("debug.option.glTraceAnchor") ?? "";
        if (string.IsNullOrEmpty(_anchor)) return;

        string outp = GlobalSettings.Get("debug.option.glTraceOut");
        if (!string.IsNullOrEmpty(outp)) _outPath = outp;

        string mm = GlobalSettings.Get("debug.option.glTraceMinMeshes");
        if (!string.IsNullOrEmpty(mm) && int.TryParse(mm, out int parsed)) _minMeshes = parsed;

        FrameDigest.OnFrameDigest = _onFrameDigest;
        _isArmed = true;
        Trace($"GlTraceAnchor: waiting for anchor '{_anchor}' -> {_outPath}");
    }

    /**
     * Runs immediately before the frame is drawn, so arming here means the very next GL
     * calls are the ones recorded. Disarming happens on the NEXT frame's callback, which
     * is the first moment the frame is known to be finished.
     */
    private static void _onFrameDigest(string digest, RenderFrame renderFrame)
    {
        if (!_isArmed) return;

        if (GLTrace.IsRecording)
        {
            // The traced frame has ended - this callback belongs to the next one.
            var calls = GLTrace.End();
            _write(calls, digest);
            _isArmed = false;
            FrameDigest.OnFrameDigest = null;
            return;
        }

        if (_hasWritten) return;

        bool match = _anchor == "first"
            ? renderFrame.FrameStats.NMeshes >= _minMeshes
            : digest == _anchor;

        if (!match) return;

        _tracedDigest = digest;
        _tracedFrame = renderFrame.FrameNumber;
        _tracedMeshes = renderFrame.FrameStats.NMeshes;
        GLTrace.Begin();
    }

    private static string _tracedDigest = "";
    private static uint _tracedFrame;
    private static int _tracedMeshes;

    private static void _write(IReadOnlyList<string> calls, string _)
    {
        _hasWritten = true;
        try
        {
            using var w = new StreamWriter(_outPath, append: false);
            /*
             * The header is what makes two traces comparable at a glance, and it is
             * deliberately the FIRST thing a diff shows: if the anchors differ, nothing
             * below is worth reading.
             */
            w.WriteLine($"# gl-trace anchor={_tracedDigest} meshes={_tracedMeshes} calls={calls.Count}");
            w.WriteLine($"# frame={_tracedFrame} traced={GLTrace.TracedCount} untraced={GLTrace.Untraced.Count}");
            if (GLTrace.Untraced.Count > 0)
            {
                // A hole in the coverage. Recorded in the artifact rather than only in a
                // log, because whoever diffs these needs to know what was NOT observed.
                w.WriteLine("# UNTRACED: " + string.Join(",", GLTrace.Untraced));
            }

            foreach (string c in calls) w.WriteLine(c);

            Trace($"GlTraceAnchor: wrote {calls.Count} calls for anchor {_tracedDigest} "
                  + $"(frame {_tracedFrame}, {_tracedMeshes} meshes) to {_outPath}");
        }
        catch (Exception e)
        {
            Error($"GlTraceAnchor: failed writing {_outPath}: {e}");
        }
    }
}
