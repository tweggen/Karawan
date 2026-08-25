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

        private List<Stroke> _listStrokesToDo;
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
            return _listStrokesToDo.Count > 0;
        }

        private Stroke _popStrokeToDo()
        {
            var idx = _listStrokesToDo.Count - 1;
            Stroke stroke = _listStrokesToDo[idx];
            _listStrokesToDo.RemoveAt(idx);
            return stroke;
        }

        private void _addStrokeToDo(in Stroke stroke)
        {
            if (_inBounds(stroke))
            {
                _listStrokesToDo.Add(stroke);
            }
        }


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

            _boundsConstraint = new BoundsConstraint(_bl, _tr);

            _connectPass = new ConnectComponentsPass(
                _strokeStore, _clusterDesc.Id, _rnd, _annotation);

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
         * Iterate until the queue of strokes is empty again.
         */
        public void Generate()
        {
            _buildPipeline();

            int maxGenerations = (int)(_clusterDesc.Size * _clusterDesc.Size / 1000f);
            
            while (true)
            {

                if (maxGenerations < _generationCounter)
                {
                    Trace(_dc, $"Returning: max generations reached.");
                    if (_report != null) Trace(_dc, $"{_annotation}: {_report.Describe()}");
                    _connectPass.Run();
                    return;
                }

                if (!_haveStrokesToDo())
                {
                    Trace(_dc, $"Returning: no more streets to do.");
                    if (_report != null) Trace(_dc, $"{_annotation}: {_report.Describe()}");
                    _connectPass.Run();
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

                            // As this is a stack, first the continuation, then the head.
                            _listStrokesToDo.Add(currTail);
                        }

                        _listStrokesToDo.Add(curr);
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
            _listStrokesToDo.Add(stroke0);
        }

        
        public void Reset(
            in string seed0,
            in StrokeStore strokeStore,
            in ClusterDesc clusterDesc
        ) {
            _rnd = new builtin.tools.RandomSource(seed0);
            _listStrokesToDo = new List<Stroke>();
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
