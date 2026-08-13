#!/usr/bin/env python3
"""
Exercise shapecheck against a synthetic registry.

WHY THIS EXISTS

gl.xml is not checked in - it is fetched at generation time - so the guard inside gen.py
cannot be run here, and an unrun guard is a guard nobody knows works. This drives the same
code with a hand-written registry small enough to reason about.

The case it reproduces is the real one: glTexParameterIiv's third parameter is a pointer in
the specification, and surface.json recorded it as a plain by-value int because the Roslyn
probe flattened Silk's "in int". That mismatch emitted a binding which passed an integer
where the driver dereferences a pointer, and it segfaulted on first use rather than failing
to build.

Run:  python3 test-shapecheck.py     (exit 0 = all cases pass)
"""

import os
import sys
import xml.etree.ElementTree as ET

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import shapecheck

REGISTRY = """<?xml version="1.0"?>
<registry>
  <commands>
    <command>
      <proto>void <name>glTexParameterIiv</name></proto>
      <param><ptype>GLenum</ptype> <name>target</name></param>
      <param><ptype>GLenum</ptype> <name>pname</name></param>
      <param>const <ptype>GLint</ptype> *<name>params</name></param>
    </command>
    <command>
      <proto>void <name>glBindBuffer</name></proto>
      <param><ptype>GLenum</ptype> <name>target</name></param>
      <param><ptype>GLuint</ptype> <name>buffer</name></param>
    </command>
  </commands>
</registry>
"""


def shape(entry, params):
    return {"EntryPoint": entry, "Parameters": params}


def p(name, typ, refkind=""):
    return {"Name": name, "Type": typ, "RefKind": refkind}


FLAGS = shapecheck.pointer_flags(ET.fromstring(REGISTRY))

failures = []


def case(label, shapes, expect_problem):
    problems = shapecheck.check(shapes, FLAGS)
    got = len(problems) > 0
    ok = got == expect_problem
    print(f"  {'PASS' if ok else 'FAIL'}  {label}")
    if not ok:
        failures.append(label)
        for pr in problems:
            print(f"          {pr}")
    elif problems:
        for pr in problems:
            print(f"          reported: {pr.strip()}")


print("shapecheck against a synthetic registry:")

# THE REAL DEFECT: spec says pointer, surface says by-value.
case("by-value where gl.xml says pointer is REPORTED",
     {"k": shape("glTexParameterIiv",
                 [p("target", "GLEnum"), p("pname", "GLEnum"), p("params", "int")])},
     expect_problem=True)

# The fix that was applied to surface.json: RefKind "in" marshals as a pointer.
case("RefKind 'in' counts as a pointer",
     {"k": shape("glTexParameterIiv",
                 [p("target", "GLEnum"), p("pname", "GLEnum"), p("params", "int", "in")])},
     expect_problem=False)

# An explicit star is equally acceptable.
case("explicit 'int*' counts as a pointer",
     {"k": shape("glTexParameterIiv",
                 [p("target", "GLEnum"), p("pname", "GLEnum"), p("params", "int*")])},
     expect_problem=False)

# The inverse mistake matters too: a pointer where the spec says by value.
case("pointer where gl.xml says by-value is REPORTED",
     {"k": shape("glBindBuffer", [p("target", "GLEnum"), p("buffer", "uint*")])},
     expect_problem=True)

case("matching by-value shape is accepted",
     {"k": shape("glBindBuffer", [p("target", "GLEnum"), p("buffer", "uint")])},
     expect_problem=False)

case("parameter COUNT mismatch is REPORTED",
     {"k": shape("glBindBuffer", [p("target", "GLEnum")])},
     expect_problem=True)

# A convenience has no entry point and nothing to check against.
case("shape with no entry point is skipped",
     {"k": shape("", [p("x", "int")])},
     expect_problem=False)

# gl.xml carries only what it carries; an unknown command is not a complaint.
case("entry point absent from the registry is skipped",
     {"k": shape("glSomethingElse", [p("x", "int")])},
     expect_problem=False)

print()
if failures:
    print(f"FAIL: {len(failures)} case(s): {', '.join(failures)}")
    sys.exit(1)

print("OK: all cases behave as specified.")
