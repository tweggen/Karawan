using System;
using System.Collections.Generic;
using System.IO;
using engine;
using static engine.Logger;

namespace Splash.Silk;

/**
 * Records GL call traces for frames that render ANCHOR states, one file per anchor.
 *
 * WHY AN ANCHOR AND NOT A FRAME NUMBER
 *
 * Measured over four runs of the same unmodified build, only 13% of frames matched
 * between two runs; the first divergence was at frame 3 and structure diverged by frame
 * 12. Fixed dt did not help, because scene sequencing depends on wall clock. So "frame
 * 400 of run A" and "frame 400 of run B" are not the same picture, and comparing their GL
 * calls would prove nothing.
 *
 * The same measurement found the way round it: 74% of consecutive frames within a run are
 * byte-identical - the game settles into quiescent plateaus - and those plateaus RECUR
 * ACROSS RUNS. Two independent runs shared 70 distinct rendered states, 61 of them
 * drawing more than 50 mesh batches.
 *
 * So the comparison is anchored on CONTENT: when both builds reach the same rendered
 * state, whenever each happens to get there, their GL call streams must agree, and a
 * difference is attributable to the binding and nothing else.
 *
 * WHY SEVERAL ANCHORS AND NOT ONE
 *
 * The first version captured a single nominated digest, and that is a bet. Tried in
 * practice: run 1 captured its anchor at frame 553, and run 2 never rendered that exact
 * state before the timeout, so there was nothing to compare. Anchors recur, but not
 * promptly and not on demand.
 *
 * Capturing the first N distinct qualifying states removes the bet. Each run comes back
 * with a spread of anchors, and the comparison is over whatever the two runs have in
 * common - which is what scripts/find-gl-anchors.py already reports from the digests.
 *
 * SETTINGS
 *
 *   debug.option.glTraceAnchor      "auto" (default when tracing is on) captures the
 *                                   first N distinct qualifying states. A digest captures
 *                                   only that state. A comma-separated list captures each.
 *   debug.option.glTraceOut         output path; the anchor digest is inserted before the
 *                                   extension, so one run yields gl-trace.<digest>.txt.
 *   debug.option.glTraceMinMeshes   qualifying threshold, default 50. An anchor drawing
 *                                   two meshes exercises almost none of the GL surface.
 *   debug.option.glTraceMaxAnchors  how many to capture, default 8.
 */
public static class GlTraceAnchor
{
    private static bool _isInstalled;
    private static string _outPath = "gl-trace.txt";
    private static int _minMeshes = 50;
    private static int _maxAnchors = 8;
    private static int _minPlateau = 4;

    /* Run-length of the current state, for the plateau test below. */
    private static string _prevDigest = "";
    private static int _plateauLength;

    /** Empty means "auto": take whatever qualifies. Otherwise only these digests. */
    private static readonly HashSet<string> _wanted = new();

    private static readonly HashSet<string> _captured = new();

    /* State of the capture in flight, if any. */
    private static bool _isCapturing;
    private static string _capturingDigest = "";
    private static uint _capturingFrame;
    private static int _capturingMeshes;

    /** Call once, after GlobalSettings are loaded. No-op unless an anchor is configured. */
    public static void Install()
    {
        if (_isInstalled) return;

        string anchor = GlobalSettings.Get("debug.option.glTraceAnchor") ?? "";
        if (string.IsNullOrEmpty(anchor)) return;

        if (anchor != "auto" && anchor != "first")
        {
            foreach (string a in anchor.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                _wanted.Add(a.Trim());
            }
        }

        string outp = GlobalSettings.Get("debug.option.glTraceOut");
        if (!string.IsNullOrEmpty(outp)) _outPath = outp;

        string mm = GlobalSettings.Get("debug.option.glTraceMinMeshes");
        if (!string.IsNullOrEmpty(mm) && int.TryParse(mm, out int parsedMin)) _minMeshes = parsedMin;

        string ma = GlobalSettings.Get("debug.option.glTraceMaxAnchors");
        if (!string.IsNullOrEmpty(ma) && int.TryParse(ma, out int parsedMax)) _maxAnchors = parsedMax;

        string mp = GlobalSettings.Get("debug.option.glTracePlateau");
        if (!string.IsNullOrEmpty(mp) && int.TryParse(mp, out int parsedPl)) _minPlateau = parsedPl;

        FrameDigest.OnFrameDigest = _onFrameDigest;
        _isInstalled = true;

        Trace(_wanted.Count > 0
            ? $"GlTraceAnchor: capturing {_wanted.Count} nominated anchor(s) -> {_outPath}"
            : $"GlTraceAnchor: capturing up to {_maxAnchors} anchors with >= {_minMeshes} meshes -> {_outPath}");
    }

    /**
     * Runs immediately before the frame is drawn, so arming here means the very next GL
     * calls are the ones recorded. A capture is closed on the NEXT callback, which is the
     * first moment the traced frame is known to have finished.
     */
    private static void _onFrameDigest(string digest, RenderFrame renderFrame)
    {
        if (_isCapturing)
        {
            _isCapturing = false;
            _write(GLTrace.End());
        }

        /*
         * Plateau tracking. Capturing the FIRST states that qualify was tried and did not
         * work: two runs each captured six anchors and shared none, because early frames
         * are exactly where the runs diverge - scene transitions land on different frames.
         *
         * The states that DO recur across runs are the quiescent plateaus the digest
         * measurement found: 74% of consecutive frames within a run are byte-identical,
         * the longest stretch being 269 frames. Requiring a state to have persisted for
         * several frames before capturing it selects those, and skips the transient
         * one-frame states that no second run will reproduce.
         */
        if (digest == _prevDigest)
        {
            ++_plateauLength;
        }
        else
        {
            _prevDigest = digest;
            _plateauLength = 1;
        }

        if (_captured.Count >= _maxAnchors) return;
        if (_captured.Contains(digest)) return;

        bool wanted = _wanted.Count > 0
            ? _wanted.Contains(digest)
            : renderFrame.FrameStats.NMeshes >= _minMeshes && _plateauLength >= _minPlateau;
        if (!wanted) return;

        _capturingDigest = digest;
        _capturingFrame = renderFrame.FrameNumber;
        _capturingMeshes = renderFrame.FrameStats.NMeshes;
        _isCapturing = true;
        GLTrace.Begin();
    }

    /** gl-trace.txt -> gl-trace.<digest>.txt, so one run's anchors do not overwrite. */
    private static string _pathFor(string digest)
    {
        string dir = Path.GetDirectoryName(_outPath) ?? "";
        string stem = Path.GetFileNameWithoutExtension(_outPath);
        string ext = Path.GetExtension(_outPath);
        if (string.IsNullOrEmpty(ext)) ext = ".txt";
        return Path.Combine(dir, $"{stem}.{digest}{ext}");
    }

    private static void _write(IReadOnlyList<string> calls)
    {
        _captured.Add(_capturingDigest);
        string path = _pathFor(_capturingDigest);
        try
        {
            using var w = new StreamWriter(path, append: false);
            /*
             * The anchor is the FIRST thing a diff shows on purpose: if two files disagree
             * about which state they captured, nothing below them is worth reading.
             *
             * The frame number is deliberately NOT in the header. It differs between runs
             * by construction - that is the whole reason for anchoring on content - and
             * putting it here would make every comparison differ on line 2.
             */
            w.WriteLine($"# gl-trace anchor={_capturingDigest} meshes={_capturingMeshes} calls={calls.Count}");
            w.WriteLine($"# traced={GLTrace.TracedCount} untraced={GLTrace.Untraced.Count}");
            if (GLTrace.Untraced.Count > 0)
            {
                // A hole in the coverage, recorded in the artifact rather than only a log:
                // whoever diffs these needs to know what was NOT observed.
                w.WriteLine("# UNTRACED: " + string.Join(",", GLTrace.Untraced));
            }

            foreach (string c in calls) w.WriteLine(c);

            Trace($"GlTraceAnchor: anchor {_capturingDigest} ({_capturingMeshes} meshes, "
                  + $"frame {_capturingFrame}) -> {calls.Count} calls, {path} "
                  + $"[{_captured.Count}/{_maxAnchors}]");
        }
        catch (Exception e)
        {
            Error($"GlTraceAnchor: failed writing {path}: {e}");
        }
    }
}
