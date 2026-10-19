#!/usr/bin/env python3
"""Fold docs/main/docs/status-log.d/*.md fragments into STATUS-LOG.md (KAR-462).

Each PR adds its status-log entry as one fragment file instead of editing
STATUS-LOG.md, so parallel PRs never conflict. This script, run in its own
roll-up PR, inserts the fragments at the top of the first "## " section of
STATUS-LOG.md, newest first (date, then ticket number), and deletes them.

Usage: scripts/rollup-status-log.py [--dry-run]
"""
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
LOG = ROOT / "docs/main/docs/STATUS-LOG.md"
FRAGMENTS = ROOT / "docs/main/docs/status-log.d"
NAME = re.compile(r"^(\d{4}-\d{2}-\d{2})-([A-Z]+-(\d+)|[^-]+)-.*\.md$")


def sort_key(path):
    m = NAME.match(path.name)
    if not m:
        sys.exit(f"bad fragment name: {path.name} (want YYYY-MM-DD-<TICKET>-<slug>.md)")
    return (m.group(1), int(m.group(3) or 0), path.name)


def main():
    dry_run = "--dry-run" in sys.argv[1:]
    fragments = sorted((p for p in FRAGMENTS.glob("*.md") if p.name != "README.md"),
                       key=sort_key, reverse=True)
    if not fragments:
        print("no fragments")
        return

    entries = []
    for p in fragments:
        text = p.read_text(encoding="utf-8").strip("\n")
        if not text.startswith("- "):
            sys.exit(f"{p.name}: an entry must be one top-level bullet starting with '- '")
        entries.append(text)

    lines = LOG.read_text(encoding="utf-8").split("\n")
    at = next((i for i, l in enumerate(lines) if l.startswith("## ")), None)
    if at is None:
        sys.exit("STATUS-LOG.md has no '## ' section to insert into")
    block = "\n".join(entries).split("\n")
    lines[at + 1:at + 1] = block

    for p in fragments:
        print(("would fold " if dry_run else "folded ") + p.name)
    if dry_run:
        return
    LOG.write_text("\n".join(lines), encoding="utf-8")
    for p in fragments:
        p.unlink()


if __name__ == "__main__":
    main()
