#!/usr/bin/env python
"""Rewrite cross-references broken by the move. Run AFTER move.sh.

Two passes:
  relative -- markdown links between docs that both moved
  absolute -- 'docs/...' references from outside docs/ (CLAUDE.md, PROCESS.md, ...)

Idempotent: a rewrite that finds no match is simply skipped.
Runnable from any directory -- it locates the repo root from its own path.
"""
import json
import os

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))  # docs/_migration -> docs -> repo root
os.chdir(ROOT)

with open(os.path.join(HERE, "link-fixes.json"), encoding="utf-8") as fh:
    fixes = json.load(fh)


def rewrite(path, old, new):
    """Replace old->new in path. Returns True if the file changed."""
    if not os.path.exists(path):
        print("MISSING:", path)
        return False
    with open(path, encoding="utf-8") as fh:
        text = fh.read()
    if old not in text:
        return False
    with open(path, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(text.replace(old, new))
    return True


rel = sum(rewrite(p, "](" + old + ")", "](" + new + ")")
          for p, old, new in fixes["relative"])
absolute = sum(rewrite(p, old, new) for p, old, new in fixes["absolute"])

print(f"relative link rewrites : {rel}/{len(fixes['relative'])}")
print(f"absolute ref rewrites  : {absolute}/{len(fixes['absolute'])}")
print(f"NOT fixed (pre-existing rot, see REVIEW.md): {len(fixes['unresolved'])}")
