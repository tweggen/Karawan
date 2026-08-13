#!/usr/bin/env python3
"""
Find ANCHOR FRAMES shared by two runs, for the GATE-F GL-binding comparison.

THE PROBLEM THIS SOLVES

GATE-F wants to prove that swapping the GL binding changes nothing observable, by
diffing the GL calls a real session issues before and after the swap. The obvious
method - run both builds, compare frame N to frame N - does not work here. Measured
over four runs of the SAME unmodified build, only 13% of frames matched, the first
divergence was at frame 3, and structure diverged by frame 12 with mesh-count deltas
reaching 71. Fixed dt (engine.Turbo) did not help. Scene sequencing depends on wall
clock, so the two runs are executing different content at the same frame number.

THE WAY ROUND IT

Do not index by frame. Index by CONTENT.

The same measurement showed 74% of consecutive frames within a run are byte-identical
- the game settles into quiescent plateaus, the longest observed being 269 frames. And
those plateaus RECUR ACROSS RUNS: two independent runs shared 70 distinct digests, 61
of them drawing more than 50 mesh batches, the richest at 153 meshes / 14 materials /
358 instances.

So: whenever both runs reach the same rendered state - however long each took to get
there - their GL call streams must be identical. That is a comparison nondeterminism
cannot disturb, because the anchor is defined by what was drawn rather than by when.

USAGE

    scripts/find-gl-anchors.py digestA.txt digestB.txt [--min-meshes 50] [--top 20]

Digests come from Splash/FrameDigest.cs, enabled by setting
"debug.option.frameDigest" to an output path.

Anchors are ranked by mesh count, because an anchor that draws two meshes exercises
almost none of the GL surface and would pass whatever the bindings did.
"""

import argparse
import collections
import sys


def load(path):
    """-> list of (frameNumber, digest, structure-tuple)"""
    rows = []
    with open(path, encoding="utf-8") as fh:
        for line in fh:
            if line.startswith("#"):
                continue
            parts = line.split()
            if len(parts) >= 7:
                rows.append((int(parts[0]), parts[1], tuple(parts[2:])))
    if not rows:
        sys.exit(f"{path}: no digest lines. Was debug.option.frameDigest set?")
    return rows


def main():
    ap = argparse.ArgumentParser(description="Find shared anchor frames between two runs.")
    ap.add_argument("digest_a")
    ap.add_argument("digest_b")
    ap.add_argument("--min-meshes", type=int, default=50,
                    help="ignore anchors drawing fewer mesh batches than this (default 50)")
    ap.add_argument("--top", type=int, default=20, help="how many anchors to list")
    args = ap.parse_args()

    a, b = load(args.digest_a), load(args.digest_b)
    count_a = collections.Counter(r[1] for r in a)
    count_b = collections.Counter(r[1] for r in b)
    struct = {r[1]: r[2] for r in a}
    first_a = {}
    first_b = {}
    for f, d, _ in a:
        first_a.setdefault(d, f)
    for f, d, _ in b:
        first_b.setdefault(d, f)

    shared = set(count_a) & set(count_b)
    rich = [d for d in shared if int(struct[d][1]) >= args.min_meshes]
    rich.sort(key=lambda d: -int(struct[d][1]))

    print(f"run A : {len(a):6} frames, {len(count_a):5} distinct states   {args.digest_a}")
    print(f"run B : {len(b):6} frames, {len(count_b):5} distinct states   {args.digest_b}")
    print(f"shared states: {len(shared)}   of which >= {args.min_meshes} meshes: {len(rich)}")

    if not rich:
        print()
        print("NO USABLE ANCHOR.")
        print("  Either the runs never reached a common rendered state, or every shared")
        print("  state is too trivial to exercise the GL surface. Re-run for longer, or")
        print("  lower --min-meshes and accept weaker coverage - but a 2-mesh anchor")
        print("  proves almost nothing about a binding swap.")
        return 1

    print()
    print(f"{'anchor digest':18} {'#A':>4} {'#B':>4} {'firstA':>7} {'firstB':>7}  "
          f"{'parts':>5} {'meshes':>6} {'mats':>4} {'insts':>5}")
    for d in rich[:args.top]:
        s = struct[d]
        print(f"{d:18} {count_a[d]:4} {count_b[d]:4} {first_a[d]:7} {first_b[d]:7}  "
              f"{s[0]:>5} {s[1]:>6} {s[2]:>4} {s[3]:>5}")

    best = rich[0]
    print()
    print("RECOMMENDED ANCHOR: " + best)
    print(f"  {struct[best][1]} mesh batches, {struct[best][3]} instances, "
          f"{struct[best][2]} materials")
    print(f"  present {count_a[best]}x in A (first at frame {first_a[best]}) and "
          f"{count_b[best]}x in B (first at frame {first_b[best]})")
    print()
    print("  Capture the GL trace at this digest in BOTH builds and diff. Frames carrying")
    print("  it are the same rendered content by construction, so any difference in the")
    print("  call stream is attributable to the binding and nothing else.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
