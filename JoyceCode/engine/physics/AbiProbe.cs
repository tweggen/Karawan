using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BepuPhysics;
using BepuPhysics.Collidables;
using static engine.Logger;

namespace engine.physics;

/**
 * Self-contained on-target falsification test for the struct-argument corruption observed on
 * Android arm64.
 *
 * THE OBSERVATION IT IS TESTING. In CreateDynamic.Execute, a BodyInertia that is provably correct
 * at the call site arrives corrupt in the callee: the correct floats shifted right by two slots,
 * prefixed with the last two floats of the Quaternion argument that precedes it. Measured
 * repeatedly on device, bit-identical across processes.
 *
 * WHY A DEDICATED TEST. Two candidate explanations were proposed and BOTH are already refuted by
 * measurement:
 *   - "declared StructLayout Size exceeds the natural field extent (28 -> 32)" - refuted: removing
 *     Size = 32 in favour of an explicit padding field preserved the layout (verified
 *     sizeof == 32/64) and the corruption persisted unchanged.
 *   - "RigidPose has the same 28 -> 32 shape so it must be affected too" - refuted: the stored pose
 *     came back exact.
 * No upstream issue supports the theory either: dotnet/runtime#88220 is COM source generation,
 * dotnet/runtime#91432 is Options source gen, dotnet/runtime#71833 is ServicePointManager
 * obsoletion, dotnet/aspnetcore#55817 is Blazor JS interop. None concern ARM64 struct ABI.
 *
 * The surviving hypothesis is ARGUMENT POSITION: a non-HFA struct larger than 16 bytes, passed by
 * value AFTER preceding homogeneous float aggregates have consumed most of the SIMD argument
 * registers (Vector3 = 3, Quaternion = 4, of 8 available). RigidPose survives because it is the
 * FIRST parameter of BodyDescription.CreateDynamic.
 *
 * This runs in-process, in the same assembly and the same build, so it is immune to the "stale or
 * incremental compilation" doubt: if the probe fails, the defect is real and reproducible in
 * isolation; if the probe passes while the real call still corrupts, the trigger is something
 * about that specific call site and this file narrows it down rather than guessing.
 *
 * Every callee is NoInlining on purpose. Inlined, there is no call boundary and therefore no ABI
 * to get wrong - an inlined probe would pass vacuously and prove nothing.
 *
 * The `in`/byref variant is deliberately NOT probed: it SIGSEGVs inside libmonosgen-2.0.so
 * (reproduced twice), which would kill the process at startup.
 */
public static class AbiProbe
{
    private static int _hasRun = 0;

    /**
     * Returns the address of a local in THIS method's frame. Because this frame is laid out
     * immediately below the caller's stack pointer, the low bits reveal the CALLER's SP alignment.
     *
     * WHY THIS MATTERS. AArch64 requires SP to be 16-byte aligned at all times. If any frame in the
     * call chain leaves it aligned to 8 instead, caller and callee compute stack-passed argument
     * slots from different bases and every stack-passed argument is read exactly 8 BYTES EARLY.
     *
     * That single mechanism accounts for every observation that killed the previous hypotheses:
     * the skew is always exactly 8 (the misalignment quantum, not 4 or 16); it hits a 12-byte
     * Vector3 as readily as a 32-byte struct, so size and layout are irrelevant; it is
     * bit-identical across launches because frame layout is deterministic; passing by `in`
     * SIGSEGVs because a POINTER read from a misaligned slot is garbage; and all twelve probe
     * cases pass because AbiProbe is called from a properly aligned stack while production is
     * called from a lambda inside an async state machine reached through a task-scheduler
     * continuation.
     *
     * Compare the value & 15 across call sites: 0 is correct, 8 is the fault.
     */
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static unsafe long FrameMark()
    {
        byte probe;
        return (long)(&probe);
    }

    /**
     * Convenience for logging the caller's stack alignment from anywhere.
     */
    public static string StackAlign()
    {
        long m = FrameMark();
        return $"frameAddr=0x{m:X} align16={m & 15} align8={m & 7} "
               + $"{((m & 15) == 0 ? "OK" : "MISALIGNED")}";
    }

    /*
     * Mirrors BepuUtilities.Symmetric3x3: six floats, no layout attribute.
     */
    public struct ProbeSymmetric3x3
    {
        public float XX, YX, YY, ZX, ZY, ZZ;
    }

    /*
     * Mirrors BepuPhysics.BodyInertia exactly, including the attribute: 28 natural bytes declared
     * as 32.
     */
    [StructLayout(LayoutKind.Sequential, Size = 32, Pack = 4)]
    public struct ProbeInertia32
    {
        public ProbeSymmetric3x3 Tensor;
        public float InverseMass;
    }

    /*
     * Same seven floats but WITHOUT the declared-size override, so natural extent == 28 and no
     * padding is forced. Isolates whether the attribute matters at all.
     */
    public struct ProbeInertia28
    {
        public ProbeSymmetric3x3 Tensor;
        public float InverseMass;
    }

    private static ProbeInertia32 _makeSentinel32() => new()
    {
        Tensor = new ProbeSymmetric3x3 { XX = 1f, YX = 2f, YY = 3f, ZX = 4f, ZY = 5f, ZZ = 6f },
        InverseMass = 7f
    };

    private static ProbeInertia28 _makeSentinel28() => new()
    {
        Tensor = new ProbeSymmetric3x3 { XX = 1f, YX = 2f, YY = 3f, ZX = 4f, ZY = 5f, ZZ = 6f },
        InverseMass = 7f
    };

    private static string _fmt(in ProbeSymmetric3x3 t, float m)
        => $"[{t.XX} {t.YX} {t.YY} {t.ZX} {t.ZY} {t.ZZ} | {m}]";

    private static bool _ok(in ProbeSymmetric3x3 t, float m)
        => t.XX == 1f && t.YX == 2f && t.YY == 3f && t.ZX == 4f
           && t.ZY == 5f && t.ZZ == 6f && m == 7f;

    /*
     * CASE A - the exact shape of CreateDynamic.Execute: two reference parameters (which occupy
     * integer registers and no SIMD registers), then Vector3 + Quaternion (7 SIMD registers), then
     * the 32-byte struct. This is the configuration that corrupts in production.
     */
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool _caseA(object r0, object r1, Vector3 v, Quaternion q, ProbeInertia32 s, int tail)
    {
        bool ok = _ok(s.Tensor, s.InverseMass);
        if (!ok)
        {
            Warning($"ABIPROBE A FAIL: expected [1 2 3 4 5 6 | 7] got {_fmt(s.Tensor, s.InverseMass)} "
                    + $"(q={q}, v={v}, tail={tail}, r0={r0 != null}, r1={r1 != null})");
        }

        return ok;
    }

    /*
     * CASE B - identical parameters, struct moved to the FRONT. If argument position is the
     * trigger, this passes while A fails.
     */
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool _caseB(object r0, object r1, ProbeInertia32 s, Vector3 v, Quaternion q, int tail)
    {
        bool ok = _ok(s.Tensor, s.InverseMass);
        if (!ok)
        {
            Warning($"ABIPROBE B FAIL: expected [1 2 3 4 5 6 | 7] got {_fmt(s.Tensor, s.InverseMass)}");
        }

        return ok;
    }

    /*
     * CASE C - no preceding float aggregates at all. Isolates "struct > 16 bytes by value" from
     * "struct > 16 bytes by value after SIMD pressure".
     */
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool _caseC(ProbeInertia32 s, int tail)
    {
        bool ok = _ok(s.Tensor, s.InverseMass);
        if (!ok)
        {
            Warning($"ABIPROBE C FAIL: expected [1 2 3 4 5 6 | 7] got {_fmt(s.Tensor, s.InverseMass)}");
        }

        return ok;
    }

    /*
     * CASE D - case A's shape but with the struct carrying NO declared-size override. If D passes
     * while A fails, the attribute is implicated after all, despite the earlier refutation.
     */
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool _caseD(object r0, object r1, Vector3 v, Quaternion q, ProbeInertia28 s, int tail)
    {
        bool ok = _ok(s.Tensor, s.InverseMass);
        if (!ok)
        {
            Warning($"ABIPROBE D FAIL: expected [1 2 3 4 5 6 | 7] got {_fmt(s.Tensor, s.InverseMass)}");
        }

        return ok;
    }

    /*
     * CASE E - the REAL BepuPhysics.BodyInertia, in case A's shape. Removes any doubt that the
     * mimic differs from the production type in some way that matters.
     */
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool _caseE(object r0, object r1, Vector3 v, Quaternion q, BodyInertia s, TypedIndex tail)
    {
        var t = s.InverseInertiaTensor;
        bool ok = t.XX == 1f && t.YX == 2f && t.YY == 3f && t.ZX == 4f
                  && t.ZY == 5f && t.ZZ == 6f && s.InverseMass == 7f;
        if (!ok)
        {
            Warning($"ABIPROBE E FAIL (real BodyInertia): expected [1 2 3 4 5 6 | 7] got "
                    + $"[{t.XX} {t.YX} {t.YY} {t.ZX} {t.ZY} {t.ZZ} | {s.InverseMass}] (q={q})");
        }

        return ok;
    }

    /*
     * A struct with 16-BYTE ALIGNMENT, mirroring the C++ `alignas(16)` in the MSVC ARM64 defect
     * (developercommunity 10576646, "Incorrect code generation for Arm64 code with aligned structs
     * pushed to parameter stack", Closed - Fixed). There, an aligned struct spilled to the
     * parameter stack was stored by the caller at [sp,#0x20] and read back by the callee at
     * 0x18 - one 8-byte slot too early, because the caller inserted alignment padding the callee
     * did not account for. That is the same 8-byte skew we measure here.
     *
     * C# has no alignas, but a Vector4 field forces 16-byte alignment, which is the closest
     * equivalent. If this case fails while ProbeInertia32 passes, alignment is the trigger rather
     * than size or position.
     */
    public struct ProbeAligned16
    {
        public Vector4 Aligner;
        public float A, B, C, D;
    }

    /*
     * CASE F - mirrors the MSVC repro's shape directly: enough leading integer parameters to
     * exhaust the integer argument registers (X0-X7), forcing the struct - or the pointer to it -
     * onto the parameter STACK rather than into a register. Production's signature does not
     * exhaust integer registers, so if F fails while A passes, the trigger is stack spilling and
     * our real call site is being hit some other way.
     */
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool _caseF(
        int i0, int i1, int i2, int i3, int i4, int i5, int i6, int i7, int i8, int i9,
        ProbeInertia32 s)
    {
        bool ok = _ok(s.Tensor, s.InverseMass);
        if (!ok)
        {
            Warning($"ABIPROBE F FAIL: expected [1 2 3 4 5 6 | 7] got {_fmt(s.Tensor, s.InverseMass)}");
        }

        return ok;
    }

    /*
     * CASE G - the 16-byte-aligned struct spilled past the integer registers, i.e. as close to the
     * MSVC reproducer as C# allows.
     */
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool _caseG(
        int i0, int i1, int i2, int i3, int i4, int i5, int i6, int i7, int i8, int i9,
        ProbeAligned16 s)
    {
        bool ok = s.Aligner.X == 1f && s.Aligner.Y == 2f && s.Aligner.Z == 3f && s.Aligner.W == 4f
                  && s.A == 5f && s.B == 6f && s.C == 7f && s.D == 8f;
        if (!ok)
        {
            Warning($"ABIPROBE G FAIL: expected [1 2 3 4 | 5 6 7 8] got "
                    + $"[{s.Aligner.X} {s.Aligner.Y} {s.Aligner.Z} {s.Aligner.W} "
                    + $"| {s.A} {s.B} {s.C} {s.D}]");
        }

        return ok;
    }

    /*
     * CASE H - mirrors engine.physics.Object's CONSTRUCTOR signature exactly, which cases A-G do
     * not cover and which is the only unmeasured hop between the correct value at the call site and
     * the corrupt one in CreateDynamic.Execute:
     *
     *   Object(Engine, DefaultEcs.Entity, BodyInertia, TypedIndex, Vector3, Quaternion,
     *          Vector3 bodyOffset = default)
     *
     * Distinctive features, none of them present in A-G:
     *   - an instance constructor, so `this` occupies X0 and shifts every integer argument
     *   - a struct (DefaultEcs.Entity) ahead of the 32-byte struct
     *   - THREE float aggregates totalling 10 SIMD registers against the 8 that exist, so the
     *     last one must spill - exactly the "spilled to the parameter stack" condition of the
     *     MSVC ARM64 defect (developercommunity 10576646)
     *   - a trailing optional parameter
     */
    private sealed class ProbeCtorMirror
    {
        public readonly bool Ok;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public ProbeCtorMirror(
            object engine,
            DefaultEcs.Entity entity,
            ProbeInertia32 s,
            int shape,
            Vector3 position, Quaternion orientation,
            Vector3 bodyOffset = default)
        {
            Ok = _ok(s.Tensor, s.InverseMass);
            if (!Ok)
            {
                Warning($"ABIPROBE H FAIL (ctor shape): expected [1 2 3 4 5 6 | 7] got "
                        + $"{_fmt(s.Tensor, s.InverseMass)} (orientation={orientation}, "
                        + $"position={position}, bodyOffset={bodyOffset}, shape={shape})");
            }
        }
    }

    /*
     * CASE I - the same signature as H but as a STATIC method, isolating whether the `this`
     * pointer is what tips it over.
     */
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool _caseI(
        object engine,
        DefaultEcs.Entity entity,
        ProbeInertia32 s,
        int shape,
        Vector3 position, Quaternion orientation,
        Vector3 bodyOffset = default)
    {
        bool ok = _ok(s.Tensor, s.InverseMass);
        if (!ok)
        {
            Warning($"ABIPROBE I FAIL (ctor shape, static): expected [1 2 3 4 5 6 | 7] got "
                    + $"{_fmt(s.Tensor, s.InverseMass)}");
        }

        return ok;
    }

    /*
     * CASE J - what H should have been.
     *
     * H had two blind spots that made it pass vacuously:
     *   1. it only validated the 32-byte struct and never checked bodyOffset
     *   2. it passed bodyOffset EXPLICITLY, so the compiler-inserted default for the optional
     *      parameter was never exercised
     *
     * Production shows the inertia arriving CORRECT at the constructor while bodyOffset arrives as
     * <Orientation.Z, Orientation.W, 0> - the same 8-byte skew, on a 12-byte Vector3. So the
     * defect is not about the struct at all; it is about whichever argument SPILLS. This signature
     * requests Vector3 + Quaternion + Vector3 = 10 SIMD registers against the 8 that exist, so the
     * third aggregate must go to the parameter stack - precisely the condition in the MSVC ARM64
     * defect (developercommunity 10576646).
     *
     * This case therefore omits bodyOffset, exactly as the real call site does, and validates
     * EVERY parameter.
     */
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool _caseJ(
        object engine,
        DefaultEcs.Entity entity,
        ProbeInertia32 s,
        int shape,
        Vector3 position, Quaternion orientation,
        Vector3 bodyOffset = default)
    {
        bool structOk = _ok(s.Tensor, s.InverseMass);
        bool posOk = position.X == 11f && position.Y == 12f && position.Z == 13f;
        bool quatOk = orientation.X == 0f && orientation.Y == 0.22612044f
                      && orientation.Z == 0f && orientation.W == 0.9740994f;
        bool offsetOk = bodyOffset == Vector3.Zero;
        bool shapeOk = shape == 77;

        if (!structOk || !posOk || !quatOk || !offsetOk || !shapeOk)
        {
            Warning(
                $"ABIPROBE J FAIL (ctor shape, optional omitted): "
                + $"struct={(structOk ? "ok" : _fmt(s.Tensor, s.InverseMass))} "
                + $"position={(posOk ? "ok" : position.ToString())} "
                + $"orientation={(quatOk ? "ok" : orientation.ToString())} "
                + $"bodyOffset={(offsetOk ? "ok" : bodyOffset.ToString())} (expected <0,0,0>) "
                + $"shape={(shapeOk ? "ok" : shape.ToString())}");
        }

        return structOk && posOk && quatOk && offsetOk && shapeOk;
    }

    /*
     * The remaining difference between the probe and production.
     *
     * Cases A-J all pass, so the SIGNATURE is not the trigger. But every probe so far passed
     * arguments that were already sitting in locals. The real call sites do not:
     *
     *   Execute(engine.PLog, engine.Simulation, Position + BodyOffset, Orientation, inertia, shape)
     *           ^^^^^^^^^^^  ^^^^^^^^^^^^^^^^^
     *           property     property chain (Engine.Simulation => _aPhysics.Simulation)
     *
     * If the JIT materialises some arguments into their final registers or stack slots and a LATER
     * argument's evaluation performs a call that clobbers them, the callee reads stale or shifted
     * data - which is what we measure. These cases interleave non-inlined calls with the aggregate
     * arguments to test exactly that.
     */
    private static object _refA = new object();
    private static object _refB = new object();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static object _getRef() => _refA;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static object _getRef2() => _refB;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Vector3 _getVec() => new Vector3(11f, 12f, 13f);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Quaternion _getQuat() => new Quaternion(0f, 0.22612044f, 0f, 0.9740994f);

    /*
     * CASE K - Execute's shape, with the two leading reference arguments produced by non-inlined
     * calls, exactly as engine.PLog and engine.Simulation are.
     */
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool _caseK(object r0, object r1, Vector3 v, Quaternion q, ProbeInertia32 s, int tail)
    {
        bool ok = _ok(s.Tensor, s.InverseMass);
        if (!ok)
        {
            Warning($"ABIPROBE K FAIL (args from calls): expected [1 2 3 4 5 6 | 7] got "
                    + $"{_fmt(s.Tensor, s.InverseMass)} (q={q}, v={v})");
        }

        return ok;
    }

    /*
     * CASE L - the ctor shape with EVERY aggregate argument produced by a non-inlined call and the
     * optional parameter omitted. This is as close to the production call site as the probe can get
     * without the surrounding closure.
     */
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool _caseL(
        object engine,
        DefaultEcs.Entity entity,
        ProbeInertia32 s,
        int shape,
        Vector3 position, Quaternion orientation,
        Vector3 bodyOffset = default)
    {
        bool structOk = _ok(s.Tensor, s.InverseMass);
        bool offsetOk = bodyOffset == Vector3.Zero;
        bool quatOk = orientation.W == 0.9740994f;
        bool posOk = position.X == 11f;

        if (!structOk || !offsetOk || !quatOk || !posOk)
        {
            Warning(
                $"ABIPROBE L FAIL (ctor shape, args from calls): "
                + $"struct={(structOk ? "ok" : _fmt(s.Tensor, s.InverseMass))} "
                + $"bodyOffset={(offsetOk ? "ok" : bodyOffset.ToString())} (expected <0,0,0>) "
                + $"orientation={(quatOk ? "ok" : orientation.ToString())} "
                + $"position={(posOk ? "ok" : position.ToString())}");
        }

        return structOk && offsetOk && quatOk && posOk;
    }

    /*
     * CASE M - the decisive one.
     *
     * Case E already passes the EXACT production signature of CreateDynamic.Execute, real
     * BodyInertia and TypedIndex included, so that callee's prologue is provably correct. The only
     * remaining difference is the CALLER: case E is invoked from RunOnce, production from
     * engine.physics.Object's constructor - and that constructor receives its own bodyOffset
     * corrupt too. One method is therefore getting both its incoming arguments and its outgoing
     * call setup wrong, by the same 8 bytes.
     *
     * What that constructor has and every previous probe lacked is a PROLOGUE. Field and property
     * initialisers run before the constructor body, and one of them is a GENERIC METHOD CALL:
     *
     *     Engine Engine = I.Get<Engine>();
     *     public Vector3 BodyOffset { get; set; } = Vector3.Zero;
     *     public Quaternion BodyRotation { get; set; } = Quaternion.Identity;
     *
     * The incoming arguments have to survive that call. If they are spilled or restored at the
     * wrong offsets across a generic call in the prologue, the constructor reads them 8 bytes early
     * and passes them on 8 bytes off - exactly what is measured.
     *
     * This mirrors that shape: initialisers including a generic call, then the same signature, then
     * a forwarding call with the arguments in the same order - so it tests BOTH the incoming
     * arguments and the forwarded ones.
     */
    private static T _genericGet<T>() where T : class, new() => new T();

    /**
     * Case N: a prologue containing ONLY `Vector3.Zero` and `Quaternion.Identity`.
     *
     * THE QUESTION THIS ANSWERS: does it take a real, non-intrinsified call to corrupt the
     * trailing argument, or is any static PROPERTY read enough?
     *
     * It matters because `engine.physics.Object` carried exactly these two initialisers long
     * after `= I.Get<Engine>()` was removed. Both are static properties, so both are `call`
     * in IL; the JIT normally intrinsifies them to a zeroing, and an earlier revision
     * concluded from a green device run that this made them safe. That conclusion was drawn
     * from a build which ALSO still had the field-by-field inertia repair in HoverModule -
     * remove the repair and the runaway returns. So the evidence was confounded and the
     * question was never actually answered.
     *
     * N differs from M in exactly one respect: no generic call, only the two property reads.
     *
     *   N PASS + M FAIL -> intrinsics really are safe; the remaining Object initialisers were
     *                      innocent and the runaway has another cause. Look elsewhere.
     *   N FAIL          -> ANY static property initialiser is enough, the rule is "no
     *                      initialisers at all on a class with a trailing struct parameter",
     *                      and removing them from Object is the fix.
     */
    private sealed class ProbeCtorIntrinsicPrologue
    {
        public Vector3 SomeOffset { get; set; } = Vector3.Zero;
        public Quaternion SomeRotation { get; set; } = Quaternion.Identity;

        public readonly bool Ok;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public ProbeCtorIntrinsicPrologue(
            object engine,
            DefaultEcs.Entity entity,
            ProbeInertia32 s,
            int shape,
            Vector3 position, Quaternion orientation,
            Vector3 bodyOffset = default)
        {
            bool structOk = _ok(s.Tensor, s.InverseMass);
            bool offsetOk = bodyOffset == Vector3.Zero;

            Ok = structOk && offsetOk;
            if (!Ok)
            {
                Warning(
                    $"ABIPROBE N FAIL (intrinsic-only prologue): "
                    + $"incomingStruct={(structOk ? "ok" : _fmt(s.Tensor, s.InverseMass))} "
                    + $"incomingBodyOffset={(offsetOk ? "ok" : bodyOffset.ToString())} "
                    + $"(expected <0,0,0>) | orientation={orientation}");
            }
        }
    }

    private sealed class ProbeCtorWithPrologue
    {
        /*
         * A generic call in a field initialiser, mirroring `Engine Engine = I.Get<Engine>();`.
         */
        private readonly object _fromGenericCall = _genericGet<object>();

        public Vector3 SomeOffset { get; set; } = Vector3.Zero;
        public Quaternion SomeRotation { get; set; } = Quaternion.Identity;
        public int Flags = 0;
        public int IntHandle = -1;

        public readonly bool Ok;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public ProbeCtorWithPrologue(
            object engine,
            DefaultEcs.Entity entity,
            ProbeInertia32 s,
            int shape,
            Vector3 position, Quaternion orientation,
            Vector3 bodyOffset = default)
        {
            bool structOk = _ok(s.Tensor, s.InverseMass);
            bool offsetOk = bodyOffset == Vector3.Zero;

            /*
             * Forward exactly as the real constructor does, so the OUTGOING setup is tested too.
             */
            bool forwardedOk = _forward(_fromGenericCall, _fromGenericCall,
                position + SomeOffset, orientation, s, shape);

            Ok = structOk && offsetOk && forwardedOk;
            if (!Ok)
            {
                Trace(
                    $"ABIPROBE M FAIL (ctor with prologue, EXPECTED): "
                    + $"incomingStruct={(structOk ? "ok" : _fmt(s.Tensor, s.InverseMass))} "
                    + $"incomingBodyOffset={(offsetOk ? "ok" : bodyOffset.ToString())} "
                    + $"(expected <0,0,0>) forwardedStruct={(forwardedOk ? "ok" : "CORRUPT")} "
                    + $"| orientation={orientation}");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool _forward(
            object r0, object r1, Vector3 v, Quaternion q, ProbeInertia32 s, int tail)
        {
            bool ok = _ok(s.Tensor, s.InverseMass);
            if (!ok)
            {
                Trace($"ABIPROBE M FORWARD FAIL: expected [1 2 3 4 5 6 | 7] got "
                        + $"{_fmt(s.Tensor, s.InverseMass)} (q={q})");
            }

            return ok;
        }
    }

    /**
     * Runs the probe exactly once per process. Safe to call from anywhere; cheap after the first
     * call. Logs at Warning level so it cannot be silenced by a debug category being off - the
     * whole point is that it reports without anyone having to switch anything on.
     */
    /**
     * Bumped by hand whenever this file changes. Printed first thing, so a device log can be
     * matched against the build it came from without inferring it from which cases happen to be
     * present.
     */
    public const int ProbeRevision = 16;

    /**
     * Identity of the running build, so a stale deployment is impossible to mistake for a fresh
     * one.
     *
     * The MVID is a GUID the compiler regenerates on EVERY distinct compilation of the assembly.
     * Two runs showing the same MVID are the same binary; a different MVID means the device really
     * did get the new build. That holds even if ProbeRevision was forgotten.
     */
    public static string BuildMarker()
    {
        var asm = typeof(AbiProbe).Assembly;
        return $"probeRev={ProbeRevision} "
               + $"mvid={asm.ManifestModule.ModuleVersionId} "
               + $"asm={asm.GetName().Name} v={asm.GetName().Version}";
    }

    public static void RunOnce()
    {
        if (System.Threading.Interlocked.Exchange(ref _hasRun, 1) != 0) return;

        Warning($"ABIPROBE BUILD: {BuildMarker()}");

        /*
         * Deliberately non-constant so the JIT cannot fold the arguments away and skip the call
         * setup entirely.
         */
        var v = new Vector3(11f, 12f, 13f);
        var q = new Quaternion(0f, 0.22612044f, 0f, 0.9740994f);
        object r0 = new object();
        object r1 = new object();

        var s32 = _makeSentinel32();
        var s28 = _makeSentinel28();

        BodyInertia real = default;
        real.InverseInertiaTensor.XX = 1f;
        real.InverseInertiaTensor.YX = 2f;
        real.InverseInertiaTensor.YY = 3f;
        real.InverseInertiaTensor.ZX = 4f;
        real.InverseInertiaTensor.ZY = 5f;
        real.InverseInertiaTensor.ZZ = 6f;
        real.InverseMass = 7f;

        bool a = _caseA(r0, r1, v, q, s32, 99);
        bool b = _caseB(r0, r1, s32, v, q, 99);
        bool c = _caseC(s32, 99);
        bool d = _caseD(r0, r1, v, q, s28, 99);
        bool e = _caseE(r0, r1, v, q, real, default);
        bool f = _caseF(0, 1, 2, 3, 4, 5, 6, 7, 8, 9, s32);

        var aligned = new ProbeAligned16
        {
            Aligner = new Vector4(1f, 2f, 3f, 4f), A = 5f, B = 6f, C = 7f, D = 8f
        };
        bool g = _caseG(0, 1, 2, 3, 4, 5, 6, 7, 8, 9, aligned);

        var v3Offset = new Vector3(21f, 22f, 23f);
        bool h = new ProbeCtorMirror(r0, default, s32, 77, v, q, v3Offset).Ok;
        bool i = _caseI(r0, default, s32, 77, v, q, v3Offset);
        bool j = _caseJ(r0, default, s32, 77, v, q);
        bool k = _caseK(_getRef(), _getRef2(), _getVec(), _getQuat(), s32, 99);
        bool l = _caseL(_getRef(), default, s32, 77, _getVec(), _getQuat());
        bool m = new ProbeCtorWithPrologue(_getRef(), default, s32, 77, _getVec(), _getQuat()).Ok;
        bool n = new ProbeCtorIntrinsicPrologue(_getRef(), default, s32, 77, _getVec(), _getQuat()).Ok;

        /*
         * EXPECTED OUTCOME, and therefore the pass condition of this probe:
         *
         *   A..L PASS   - the twelve controls. A by-value struct argument of this shape is
         *                 delivered correctly whatever its size, layout, position, alignment or
         *                 provenance. If ANY of these fails, something new is broken: shout.
         *
         *   M    FAIL   - the deliberate reproduction. M differs from the controls in exactly one
         *                 respect: its class has a field initialiser containing a call, which the
         *                 compiler emits into the constructor prologue. On Mono/ARM64 that prologue
         *                 corrupts the trailing value-type parameter. M failing is the defect still
         *                 being present in the runtime; it is NOT a regression in our code, so it
         *                 is reported at Trace and does not raise the summary to Warning.
         *
         *   M    PASS   - the runtime has been fixed upstream. Worth knowing, so say so once.
         *
         * See docs/BUGS/MONO-ARM64-CTOR-PROLOGUE-ARG-CORRUPTION.md.
         */
        bool controlsOk = a && b && c && d && e && f && g && h && i && j && k && l;

        string summary =
            $"ABIPROBE RESULT: A(struct after Vector3+Quaternion, Size=32)={(a ? "PASS" : "FAIL")} "
            + $"B(struct first)={(b ? "PASS" : "FAIL")} "
            + $"C(struct alone)={(c ? "PASS" : "FAIL")} "
            + $"D(after HFAs, no declared Size)={(d ? "PASS" : "FAIL")} "
            + $"E(real BodyInertia after HFAs)={(e ? "PASS" : "FAIL")} "
            + $"F(struct spilled past 10 int args)={(f ? "PASS" : "FAIL")} "
            + $"G(16-byte-aligned struct spilled)={(g ? "PASS" : "FAIL")} "
            + $"H(Object ctor shape, instance)={(h ? "PASS" : "FAIL")} "
            + $"I(Object ctor shape, static)={(i ? "PASS" : "FAIL")} "
            + $"J(ctor shape, optional OMITTED, all params checked)={(j ? "PASS" : "FAIL")} "
            + $"K(Execute shape, ref args from calls)={(k ? "PASS" : "FAIL")} "
            + $"L(ctor shape, ALL aggregates from calls)={(l ? "PASS" : "FAIL")} "
            + $"M(ctor WITH PROLOGUE: field inits + generic call, incoming+forwarded)="
            + $"{(m ? "PASS" : "FAIL")} "
            + $"N(ctor prologue, INTRINSIC-ONLY: Vector3.Zero + Quaternion.Identity)="
            + $"{(n ? "PASS" : "FAIL")} "
            + $"| sizeof: ProbeInertia32={Unsafe.SizeOf<ProbeInertia32>()} "
            + $"ProbeInertia28={Unsafe.SizeOf<ProbeInertia28>()} "
            + $"BodyInertia={Unsafe.SizeOf<BodyInertia>()} "
            + $"| runtime={RuntimeInformation.FrameworkDescription} "
            + $"arch={RuntimeInformation.ProcessArchitecture} "
            + $"os={RuntimeInformation.OSDescription}";

        if (!controlsOk)
        {
            Warning($"ABIPROBE BUILD: {BuildMarker()}");
            Warning($"ABIPROBE STACK (probe caller): {StackAlign()}");
            Warning(summary);
            Warning(
                "ABIPROBE: a CONTROL case failed. A plain by-value struct argument is no longer "
                + "delivered correctly, which is broader than the known constructor-prologue "
                + "defect. The PASS/FAIL pattern above identifies the trigger: A fail + B pass "
                + "means argument POSITION; A fail + D pass means the declared StructLayout Size; "
                + "C fail means size alone suffices. Treat any struct-carrying call as suspect.");
        }
        else if (!n)
        {
            Warning(
                "ABIPROBE: case N FAILED. An intrinsic-only prologue - just Vector3.Zero and "
                + "Quaternion.Identity - is ENOUGH to corrupt the trailing argument. The rule is "
                + "therefore 'no instance field or property initialisers at all on a class whose "
                + "constructor takes a trailing value type', not merely 'no calls'. Every class "
                + "in list B of the sweep needs re-checking against that stricter rule.");
        }
        else if (m)
        {
            Warning(
                "ABIPROBE: all cases PASS, INCLUDING M. The Mono/ARM64 constructor-prologue "
                + "argument corruption appears to be FIXED in this runtime. The workarounds "
                + "documented in docs/BUGS/MONO-ARM64-CTOR-PROLOGUE-ARG-CORRUPTION.md can be "
                + "revisited - verify on device before removing any of them.");
        }
        else
        {
            Trace($"ABIPROBE: controls pass, M fails as expected. {summary}");
        }
    }
}
