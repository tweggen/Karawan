using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using engine.streets.generation;
using engine.world;
using static engine.Logger;

namespace engine.streets
{
    public class Generator
    {
        private static readonly engine.Dc _dc = engine.Dc.StreetGen;

        private void trace(string message)
        {
            Trace(_dc, $"{_annotation}: {message}");
        }

        private CandidateQueue _queue;
        private StrokeStore _strokeStore;
        private generation.NetworkBuilder _networkBuilder;
        private ConnectComponentsPass _connectPass;

        /**
         * Set to collect per-constraint rejection counts for a run. Null costs nothing.
         */
        public bool CollectReport { get; set; } = false;
        private GenerationReport _report;
        private ExpansionRuleTable _ruleTable;
        private SuccessorEmitter _emitter;

        /**
         * The ruleset to grow with. Null means the shipped defaults.
         */
        internal ExpansionRuleTable RuleTable { get; set; }

        /**
         * Whether this run may build ramps, bridges and tunnels.
         *
         * Injected exactly like RuleTable rather than read from GlobalSettings here:
         * the setting is process global, so a generator that consulted it directly
         * could not be driven both ways in one test run without leaking into whatever
         * is generating beside it. ClusterDesc._generateStrokes does the one read of
         * the setting and passes the answer in.
         *
         * Off, this run is bit for bit the ground-only generator: the two structure
         * constraints are in the pipeline but each returns on its first line, because
         * RampClearance stays zero and no candidate is a Bridge or a Tunnel.
         */
        public bool EnableGradeSeparation { get; set; } = false;

        private float _rampClearance = -1f;

        /**
         * How far, in plan view, an ordinary stroke must stay from a ramp that reaches
         * into its own deck. Supplied to the pipeline only when grade separation is on.
         *
         * Negative - the default - means "derive": the widest carriageway this ruleset
         * can build, so that two carriageways at that plan separation just touch. That
         * is a floor, not a policy; what a structure should actually reserve beside
         * itself is WP-B2/B3's decision and is expected to set this explicitly.
         */
        public float RampClearance
        {
            get => _rampClearance >= 0f ? _rampClearance : Stroke.WidthForWeight(weightMax);
            set => _rampClearance = value;
        }

        private float _minSpanLength = -1f;

        /**
         * Shortest bridge or tunnel deck worth building. Negative means "derive": a
         * deck has to at least span the widest carriageway that can pass under it.
         */
        public float MinSpanLength
        {
            get => _minSpanLength >= 0f ? _minSpanLength : Stroke.WidthForWeight(weightMax);
            set => _minSpanLength = value;
        }

        /**
         * Longest bridge or tunnel deck. Zero means unbounded, which is what a WP-B1
         * world gets: how long a deck may stand up is a structural question this work
         * package deliberately does not answer.
         */
        public float MaxSpanLength { get; set; } = 0f;
        private ICandidateConstraint[] _pipeline;
        private BoundsConstraint _boundsConstraint;
        private GenerationContext _ctx;

        /**
         * A constraint may rewrite a candidate and demand that everything runs again.
         * The original loop was unbounded; this is a backstop, not a tuning knob.
         */
        private const int MaxRestartsPerCandidate = 32;
        private bool _traceGenerator = false;
        private string _annotation = "";
        private ClusterDesc _clusterDesc;

        private int _generationCounter;
        private builtin.tools.RandomSource _rnd;

        private Vector2 _bl;
        private Vector2 _tr;

        public float minPointToCandPointDistance { get; set; } = 30f;
        public float minPointToCandStrokeDistance { get; set; } = 30f;
        public float minPointToCandIntersectionDistance { get; set; } = 30f;

        public float weightMin { get; set; } = 0.2f;
        public float weightMax { get; set; } = 1.3f;
        public float weightRange 
        {
            get => weightMax-weightMin;
        }

        public float normWeight(float weight)
        {
            return (weight - weightMin) / weightRange;
        }


        /*
         * All proabilities are given in the 0..256 range to avoid differrences
         * between platforms due to rounding errors on floats.
         */
        public int probabilityNextStrokeForward { get; set; } = 252;
        private int probabilityNextStrokeBranch(float weight) => (int)(150f / (1+4f * (1f-normWeight(weight))));
        private int probabilityNextStrokeRandom(float weight) => (int)(80 - normWeight(weight)*60f);
        public int probabilityNextStrokeStraightDecreaseWeight { get; set; } = 5;
        public int probabilityNextStrokeStraightIncreaseWeight { get; set; } = 10;
        public int probabilityNextStrokeBranchDecreaseWeight { get; set; } = 190;
        public int probabilityNextStrokeBranchIncreaseWeight { get; set; } = 3;

        public float  newStrokeMinimum { get; set; } = 60f;
        public float newStrokeSquaredWeight { get; set; } = 40f;
        public float newLengthMin { get; set;  } = 75f;

        public float weightIncreaseFactor { get; set; } = 1.1f;
        public float weightDecreaseFactor { get; set; } = 0.9f;
        public float probabilityAngleSlightTurn { get; set; } = 30f;
        public int AngleSlightTurnMax { get; set; } = 6;

        public float AngleMinStrokes { get; set; } = 40.0f;


        private bool _inBounds(in Stroke cand)
        {
            return (true
                && cand.A.Pos.X > _bl.X
                && cand.A.Pos.Y > _bl.Y
                && cand.A.Pos.X < _tr.X
                && cand.A.Pos.Y < _tr.Y
                && cand.B.Pos.X > _bl.X
                && cand.B.Pos.Y > _bl.Y
                && cand.B.Pos.X < _tr.X
                && cand.B.Pos.Y < _tr.Y);
        }


        private bool _haveStrokesToDo()
        {
            return _queue.Count > 0;
        }

        private Stroke _popStrokeToDo()
        {
            Stroke stroke = _queue.Pop();
            OnCandidatePopped?.Invoke(stroke, _queue.Pending);
            return stroke;
        }

        private void _addStrokeToDo(in Stroke stroke)
        {
            if (_inBounds(stroke))
            {
                _queue.Push(stroke);
            }
        }


        /**
         * Called with each candidate as it leaves the queue, together with everything
         * still waiting behind it.
         *
         * The heavy-first ordering is a property of the order candidates actually leave
         * the queue in, and there is no other way to observe that from outside. Asking
         * the queue's comparer instead would pass with the queue unwired from the
         * generator entirely - which is exactly how WP-B1 found two constraints that had
         * had passing tests for months while sitting outside the pipeline. Null costs
         * nothing.
         */
        internal Action<Stroke, IReadOnlyList<Stroke>> OnCandidatePopped { get; set; }


        /**
         * Build the constraint pipeline for this run.
         *
         * ORDER IS BEHAVIOUR: it is exactly the order these checks appeared in the
         * original validation loop. An earlier rejection means a later constraint never
         * gets to rewrite the candidate. Do not rearrange.
         */
        private void _buildPipeline()
        {
            _ctx = new GenerationContext
            {
                MinPointToCandPointDistance = minPointToCandPointDistance,
                MinPointToCandStrokeDistance = minPointToCandStrokeDistance,
                MinPointToCandIntersectionDistance = minPointToCandIntersectionDistance,
                AngleMinStrokesRad = AngleMinStrokes * (float) Math.PI / 180f,
                ClusterId = _clusterDesc.Id,
                IsTracing = _traceGenerator
            };

            /*
             * Structure tunables are supplied ONLY with the flag on, and that is a cost
             * decision as much as a correctness one: ClearanceConstraint short circuits
             * on RampClearance <= 0 before it reaches the store, whereas
             * StrokeStore.GetRampsNear allocates two lists on every call. Supplied
             * unconditionally the network would still be identical and the allocation
             * gate would trip.
             */
            if (EnableGradeSeparation)
            {
                _ctx.RampClearance = RampClearance;
                _ctx.MinSpanLength = MinSpanLength;
                _ctx.MaxSpanLength = MaxSpanLength;
            }

            /*
             * HEAVY FIRST, and only with the flag on.
             *
             * A structure has to be placed on a heavy corridor before side streets
             * attach to it, or lifting the corridor orphans whatever has already grown
             * off its interior. Draining the queue by weight is what buys that: a branch
             * is emitted from an already accepted stroke and drawn from a weight group
             * whose decrease probability is 190 of 256, so the corridor is finished
             * before its own branches are judged.
             *
             * Off, CandidateQueue.Pop is RemoveAt(Count - 1) and nothing else, which is
             * the stack this generator has always been.
             */
            _queue.HeavyFirst = EnableGradeSeparation;

            _boundsConstraint = new BoundsConstraint(_bl, _tr);

            _connectPass = new ConnectComponentsPass(
                _strokeStore, _networkBuilder, _clusterDesc.Id, _rnd, _annotation);

            _report = CollectReport ? new GenerationReport() : null;

            _ruleTable = RuleTable ?? ExpansionRuleTable.Defaults();

            _emitter = new SuccessorEmitter(
                _ruleTable, _rnd, _clusterDesc,
                new EmitterSettings
                {
                    WeightMin = weightMin,
                    WeightMax = weightMax,
                    WeightDecreaseFactor = weightDecreaseFactor,
                    WeightIncreaseFactor = weightIncreaseFactor,
                    NewStrokeMinimum = newStrokeMinimum,
                    NewStrokeSquaredWeight = newStrokeSquaredWeight,
                    NewLengthMin = newLengthMin,
                    ProbabilityAngleSlightTurn = probabilityAngleSlightTurn,
                    AngleSlightTurnMax = AngleSlightTurnMax,
                    BottomLeft = _bl,
                    TopRight = _tr
                },
                stroke => _addStrokeToDo(stroke));

            _pipeline = new ICandidateConstraint[]
            {
                new MinLengthConstraint(),
                new SnapToNearbyPointConstraint(),
                new AlreadyConnectedConstraint(),
                new AngleSeparationConstraint(atB: false),
                new AngleSeparationConstraint(atB: true),
                new StrokeNearPointConstraint(),
                new PointNearStrokeConstraint(),

                /*
                 * WHERE THE TWO STRUCTURE CONSTRAINTS GO, AND WHY.
                 *
                 * After StrokeNearPointConstraint, which is the last constraint that can
                 * return Restart. Everything above may still move the candidate's far
                 * end onto an existing junction, and a Reject placed before those would
                 * throw away a candidate that was about to snap clear of the ramp it is
                 * being rejected for - the rejection has to be judged on the geometry
                 * the candidate finally has.
                 *
                 * Before IntersectionConstraint, which is much the most expensive check
                 * here, so a candidate that is going to be refused does not pay for it.
                 * Span length first of the two: it is pure arithmetic on the candidate,
                 * while clearance queries the stroke octree.
                 *
                 * Neither can move a ground-only city. SpanLengthConstraint returns on
                 * its first line for Street and ConnectorBridge, which are the only two
                 * kinds a flag-off city contains, and ClearanceConstraint returns on its
                 * first line whenever RampClearance is zero, which is what the flag-off
                 * context above leaves it at.
                 */
                new SpanLengthConstraint(),
                new ClearanceConstraint(),

                new IntersectionConstraint(),
            };
        }


        /**
         * Run every constraint once, stopping at the first that has something to say.
         */
        private Verdict _runPipeline(Stroke cand)
        {
            foreach (var constraint in _pipeline)
            {
                Verdict verdict = constraint.Check(cand, _strokeStore, _ctx);
                if (verdict.Kind != VerdictKind.Accept)
                {
                    return verdict;
                }
            }

            return Verdict.Accept;
        }


        /**
         * Grow the network, then reattach whatever it left disconnected.
         *
         * The connect pass used to be called on BOTH of the drain loop's exits, which is
         * one call per Generate() either way but leaves nothing that can run after the
         * drain and before the bridging. WP-B2 needs that gap to exist, so the loop is
         * _drain() and the pass is called once, here.
         *
         * ConnectComponentsPass draws from the RandomSource, so it has to stay at the
         * same point in the sequence of draws it has always been at - the very end of a
         * run - which is what makes this a pure hoist and not a re-ordering.
         */
        public void Generate()
        {
            _buildPipeline();
            _drain();
            _connectPass.Run();
        }


        /**
         * Iterate until the queue of candidates is empty, or the budget is spent.
         *
         * The budget is the cluster's own, computed once per run: it counts strokes
         * judged, not passes over the queue, so nothing that reorders the queue may
         * hand out a fresh allowance.
         */
        private void _drain()
        {
            int maxGenerations = (int)(_clusterDesc.Size * _clusterDesc.Size / 1000f);

            while (true)
            {

                if (maxGenerations < _generationCounter)
                {
                    Trace(_dc, $"Returning: max generations reached.");
                    if (_report != null) Trace(_dc, $"{_annotation}: {_report.Describe()}");
                    return;
                }

                if (!_haveStrokesToDo())
                {
                    Trace(_dc, $"Returning: no more streets to do.");
                    if (_report != null) Trace(_dc, $"{_annotation}: {_report.Describe()}");
                    return;
                }

                Stroke curr = _popStrokeToDo();
                // trace( 'Generator: Starting new generation.' );

                // Option B: Mark new points created for this stroke, in case validation fails
                /*
                 * Check, wether this segment is valid.
                 */

                /*
                 * In bounds of the desired area?
                 */
                if (_boundsConstraint.Check(curr, _strokeStore, _ctx).Kind != VerdictKind.Accept)
                {
                    if (_traceGenerator) Trace(_dc, $"curr is out of bounds: {curr.ToString()}");
                    /*
                     * Out of range, so discard it.
                     */
                    continue;
                }

                /*
                 * Is there a street point close enough to use it?
                 *
                 * (in this implementation we assume no other pair of street points
                 * is too close to each other).
                 */

                bool continueCheck = true;
                bool doAdd = true;

                int restarts = 0;

                while (continueCheck)
                {
                    /*
                     * A Restart means a constraint moved an endpoint and everything has
                     * to be judged again from the top. A Split means the candidate
                     * crosses an existing stroke.
                     */
                    Verdict verdict = _runPipeline(curr);

                    if (verdict.Kind == VerdictKind.Reject)
                    {
                        if (_traceGenerator)
                        {
                            Trace(_dc, $"Discarding candidate ({verdict.Reason}): {curr}");
                        }
                        _report?.CountRejection(verdict.Reason);
                        doAdd = false;
                        continueCheck = false;
                        continue;
                    }

                    if (verdict.Kind == VerdictKind.Restart)
                    {
                        if (_report != null) ++_report.Restarts;

                        if (++restarts > MaxRestartsPerCandidate)
                        {
                            /*
                             * Unreachable for every baseline seed - if it were not, the
                             * fingerprints would have moved when this bound was
                             * introduced. Present so that a future constraint cannot
                             * turn the loop into a hang.
                             */
                            Warning(_dc, $"Restart budget exhausted for {curr}, discarding.");
                            if (_report != null) ++_report.RestartBudgetExhausted;
                            doAdd = false;
                            continueCheck = false;
                            continue;
                        }
                        continue;
                    }

                    if (verdict.Kind == VerdictKind.Split)
                    {
                        if (_report != null) ++_report.Splits;

                        StreetPoint intersectionStreetPoint = verdict.SplitPoint;

                        if (_traceGenerator)
                        {
                            Trace(_dc, $"Trying intersection point {intersectionStreetPoint}");
                        }

                        /*
                         * Split the intersected stroke in two at the intersection point.
                         * All topology mutation lives in NetworkBuilder; the order of
                         * operations in there is part of the generated output.
                         */
                        Stroke oldStrokeExists = verdict.SplitTarget;
                        Stroke newStrokeExists = _networkBuilder.SplitStrokeAt(
                            oldStrokeExists, intersectionStreetPoint);

                        /*
                         * Both halves are in the store now, so both endpoints of both are
                         * necessarily InStore and these checks cannot report anything.
                         * Retained until WP-2c retires the orphan tracking wholesale.
                         */
                        _generationCounter++;

                        /*
                         * Add the candidate stroke, truncated to this intersection
                         */
                        var oldCurrB = curr.B;
                        if( curr.Store != null ) {
                            throw new InvalidOperationException( $"Generator: (intersecting) curr already is in store ({curr})");
                        }
                        curr.B = intersectionStreetPoint;

                        if (verdict.GenerateTail)
                        {
                            /*
                             * And add the continuation, after the intersection.
                             */
                            var currTail = curr.CreateUnattachedCopy();
                            currTail.PushCreator("newTail");
                            currTail.A = intersectionStreetPoint;
                            currTail.B = oldCurrB;

                            /*
                             * First the continuation, then the head. The queue pops the
                             * later of two equal weights, and a split's two halves carry
                             * the candidate's own weight, so the head comes out first
                             * under the heavy-first ordering exactly as it does off it.
                             */
                            _queue.Push(currTail);
                        }

                        _queue.Push(curr);
                        _generationCounter++;

                        // Leave this loop.
                        doAdd = false;
                        continueCheck = false;
                        continue;
                    }

                    /*
                     * If we reached this point, we are clean. No streetpoint closer to another
                     * streetpoint, plus this stroke is not intersecting another one.
                     */
                    break;

                }

                if( !doAdd ) {
                    // trace( 'Generator: Avoiding to add stroke.' );
                    continue;
                }

                /*
                 * Add the stroke to the map, creating a continuation and
                 * pronably side streets.
                 */
                _strokeStore.AddStroke(curr);
                if (_report != null) ++_report.Accepted;
                ++_generationCounter;

                /*
                 * Compute some options.
                 */
                _emitter.Emit(curr);
            }
        }



        public void SetBounds( 
            float blx0, float bly0,
            float trx0, float try0
        ) {
            _tr = new Vector2( trx0, try0 );
            _bl = new Vector2( blx0, bly0 );
        }


        public void SetAnnotation(string annotation)
        {
            _annotation = annotation;
        }
        

        public void AddStartingStroke(in Stroke stroke0){
            _queue.Push(stroke0);
        }

        
        public void Reset(
            in string seed0,
            in StrokeStore strokeStore,
            in ClusterDesc clusterDesc
        ) {
            _rnd = new builtin.tools.RandomSource(seed0);
            _queue = new CandidateQueue();
            _strokeStore = strokeStore;
            _networkBuilder = new generation.NetworkBuilder(strokeStore);
            _clusterDesc = clusterDesc;
            _generationCounter = 0;

            // Reset tracking structures
        }


        public Generator() 
        {
        }
    }
}
