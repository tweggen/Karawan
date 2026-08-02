#!/usr/bin/env python
"""Prepend a 3-field status block to each plan. Run AFTER move.sh.

Idempotent: re-running skips files that already carry a status block.
Runnable from any directory -- it locates the repo root from its own path.
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))  # docs/_migration -> docs -> repo root
os.chdir(ROOT)

with open(os.path.join(HERE, "status-headers.json"), encoding="utf-8") as fh:
    headers = json.load(fh)

applied = skipped = missing = 0
for path, hdr in sorted(headers.items()):
    if not os.path.exists(path):
        print("MISSING:", path)
        missing += 1
        continue

    with open(path, encoding="utf-8") as fh:
        text = fh.read()

    # Skip only if OUR header is already there. Keying this on "**Status:**"
    # instead would suppress exactly the docs that need correcting most -- the
    # ones carrying their own (stale) status line. "**Verified:**" appears only
    # in a generated header, so it is the safe idempotency marker.
    #
    # A doc's own status line is deliberately left in place: replacing it is a
    # rewrite, not a filing change. The contradiction stays visible, and every
    # such doc is already listed under "suspicious" in REVIEW.md.
    if "**Verified:**" in "\n".join(text.split("\n")[:8]):
        skipped += 1
        continue

    lines = text.split("\n")
    insert_at = 1 if lines and lines[0].startswith("# ") else 0  # keep the H1 first
    out = lines[:insert_at]
    if insert_at:
        out.append("")
    out += [hdr, ""]
    out += lines[insert_at:]

    with open(path, "w", encoding="utf-8", newline="\n") as fh:
        fh.write("\n".join(out))
    applied += 1

print(f"headers applied : {applied}")
print(f"already present : {skipped}")
print(f"missing files   : {missing}")
if missing:
    print("\nMissing files usually mean move.sh has not run yet.", file=sys.stderr)
    sys.exit(1)
