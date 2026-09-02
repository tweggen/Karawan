using System;
using System.Collections.Generic;
using System.Numerics;
using engine.geom;

namespace engine.streets;

public class Quarter
{
    private object _lo = new();

    public required engine.world.ClusterDesc ClusterDesc; 
    
    private List<QuarterDelim> _delims = new();
    private bool _isInvalid = false;
    private bool _hasDeadEnd = false;

    private List<Estate> _estates = new();
    private Dictionary<string, string> _debugMap = new();
    private AABB _aabb = new();

    [Flags] 
    public enum QuarterAttributes
    {
        Forest = 0x00000002,
        Building = 0x00000004
    }

    public QuarterAttributes Attributes = 0;
    
    
    public AABB AABB {
        get => _aabb;
        
    }

    /**
     * The delims are stored clockwise in the quarter.
     */
    public void AddQuarterDelim(in QuarterDelim quarterDelim)
    {
        lock (_lo)
        {
            _aabb.Add(new Vector3(quarterDelim.StartPoint.X, 0f, quarterDelim.StartPoint.Y));
            _delims.Add(quarterDelim);
        }
    }

    /**
     * The delims are stored clockwise in the quarter.
     */
    public List<QuarterDelim> GetDelims()
    {
        lock (_lo)
        {
            return _delims;
        }
    }

    public Vector2 GetCenterPoint()
    {
        return new Vector2(AABB.Center.X, AABB.Center.Z);
    }

    public Vector3 GetCenterPoint3()
    {
        return new Vector3(AABB.Center.X, 0f, AABB.Center.Z);
    }

    public void SetInvalid(bool i)
    {
        lock (_lo)
        {
            _isInvalid = i;
        }
    }

    public bool IsInvalid()
    {
        lock (_lo)
        {
            return _isInvalid;
        }
    }

    public void SetDeadEnd(bool i)
    {
        lock (_lo)
        {
            _hasDeadEnd = i;
        }
    }

    public bool GetDeadEnd()
    {
        lock (_lo)
        {
            return _hasDeadEnd;
        }
    }

    public List<Estate> GetEstates()
    {
        lock (_lo)
        {
            return _estates;
        }
    }


    private bool _isSidewalkWidthValid;
    private float _sidewalkWidth;


    /**
     * How wide this block's pavement is, in metres.
     *
     * Two things need this number and they have to be the SAME number: the block floor
     * insets its cap by it, so that the strip along the kerb is level across, and
     * QuarterGenerator._createBuildings insets the estate by it to find the building
     * footprint. If the two ever drift apart, the pavement and the building wall stop
     * meeting - a gap or an overlap all the way round every block.
     *
     * It has always been computed, used and thrown away inside _createBuildings, which is
     * also why it is in tenth metres there: that is the unit ClipperOffset works in. Metres
     * here, converted at the one call site that wants Clipper units.
     *
     * Constant per block and derived only from the cluster's downtown field at the block's
     * centre, so it is cached rather than recomputed. Note this may not be read before the
     * block's delimiters are complete - GetCenterPoint comes from the AABB they build.
     */
    public float SidewalkWidth
    {
        get
        {
            lock (_lo)
            {
                if (!_isSidewalkWidthValid)
                {
                    var c = GetCenterPoint();
                    float downtownness = ClusterDesc.GetAttributeIntensity(
                        ClusterDesc.Pos + new Vector3(c.X, 0f, c.Y),
                        world.ClusterDesc.LocationAttributes.Downtown);

                    _sidewalkWidth =
                        downtownness < 0.2f ? 1f
                        : downtownness < 0.5f ? 2f
                        : downtownness < 0.7f ? 4f
                        : 6f;

                    _isSidewalkWidthValid = true;
                }

                return _sidewalkWidth;
            }
        }
    }


    /**
     * A city block is a PAD: one plane, tilted to sit on its own corners.
     *
     * Everything a quarter carries - its floor, its buildings, its trees, its shops -
     * asks this, so a block and the things standing on it cannot disagree about where
     * the ground is. That is the property worth having, and it is why this is a plane
     * rather than something that follows the terrain across the block: a plane is
     * exactly reproducible at any point by any caller, cheaply, with no reference to
     * the mesh.
     *
     * The corners are the block's own street junctions. The pad does NOT meet them
     * exactly, and the residual is not negligible: a block's corners are section points
     * displaced from their junctions by different amounts in different directions, so
     * they are not coplanar even over an exactly planar hillside. Measured over the
     * generated cities on a 5.8 % plane the residual is 0.02 m at the median, 0.36 m at
     * the 99th percentile and 1.66 m at the worst corner. That is why the block's FLOOR
     * takes CornerGroundHeightAt at its boundary rather than the pad - the kerb is where
     * a residual is visible - and why what the pad is for is the block's interior, where
     * the buildings are. At the block's centroid the pad is the mean of its corner
     * heights exactly, since the fit is parametrised about that centroid, so the pad and
     * the floor agree in the middle by construction.
     *
     * The alternative was a flat pad at the mean, which is what a terraced hillside city
     * really looks like - but it steps at every block edge by up to half the fall across
     * the block, and nothing renders that step. Tilting the pad removes it.
     */
    private bool _isPadValid;
    private float _padA, _padB, _padC;


    /**
     * Height of this block's ground surface at a point, in world space.
     *
     * @param v2Cluster
     *     Position in CLUSTER coordinates, the same space QuarterDelim.StartPoint is in.
     */
    public float GroundHeightAt(in Vector2 v2Cluster)
    {
        /*
         * A flat city answers exactly, and short circuits before any arithmetic: a plane
         * fitted to equal corner heights would come back to within a rounding error of
         * the average rather than the average itself, and "the flat path is untouched"
         * is a property this whole line of work is gated on.
         */
        if (ClusterDesc.StreetHeightSource.IsFlat)
        {
            return ClusterDesc.AverageHeight;
        }

        lock (_lo)
        {
            if (!_isPadValid)
            {
                _fitPadNoLock();
            }

            return _padA * v2Cluster.X + _padB * v2Cluster.Y + _padC;
        }
    }


    /**
     * Least squares plane through the corner heights.
     *
     * Falls back to the mean whenever the fit is not determined - fewer than three
     * corners, or corners collinear in plan - since a singular fit would otherwise
     * produce a wildly tilted pad from a rounding error.
     */
    private void _fitPadNoLock()
    {
        _isPadValid = true;
        _padA = 0f;
        _padB = 0f;
        _padC = ClusterDesc.AverageHeight;

        var source = ClusterDesc.StreetHeightSource;
        int n = _delims.Count;
        if (n < 1) return;

        Span<float> hs = n <= 64 ? stackalloc float[n] : new float[n];

        float sx = 0f, sz = 0f, sh = 0f;
        for (int i = 0; i < n; ++i)
        {
            /*
             * The corner's own junction: a delimiter's StartPoint is a section point of
             * its StreetPoint - see QuarterDelim, where that is one write.
             */
            hs[i] = source.GroundHeightAt(_delims[i].StreetPoint);
            sx += _delims[i].StartPoint.X;
            sz += _delims[i].StartPoint.Y;
            sh += hs[i];
        }

        float mx = sx / n, mz = sz / n, mh = sh / n;
        _padC = mh;

        if (n < 3) return;

        /*
         * Normal equations about the centroid, which keeps the numbers small and the
         * constant term trivially the mean height.
         */
        float sxx = 0f, sxz = 0f, szz = 0f, sxh = 0f, szh = 0f;
        for (int i = 0; i < n; ++i)
        {
            float dx = _delims[i].StartPoint.X - mx;
            float dz = _delims[i].StartPoint.Y - mz;
            float dh = hs[i] - mh;

            sxx += dx * dx;
            sxz += dx * dz;
            szz += dz * dz;
            sxh += dx * dh;
            szh += dz * dh;
        }

        float det = sxx * szz - sxz * sxz;

        /*
         * Scaled against the spread, so the test means "collinear" rather than "small",
         * and does not change meaning with the size of the block.
         */
        float scale = sxx + szz;
        if (Single.Abs(det) < 1e-6f * scale * scale)
        {
            return;
        }

        _padA = (szz * sxh - sxz * szh) / det;
        _padB = (sxx * szh - sxz * sxh) / det;
        _padC = mh - _padA * mx - _padB * mz;
    }


    /**
     * Height of the ground at one of the block's own boundary corners, exactly.
     *
     * NOT GroundHeightAt of the same point. The pad is a plane through corners that are
     * not coplanar, so at a corner it answers with a fit residual, and at a corner the
     * residual is the whole problem: the block's floor is built from here and its top face
     * is the pavement, so the kerb would come out as QuarterSidewalkOffset plus that
     * residual - and wherever the residual is below minus the kerb, the pavement is under
     * the roadway. Asking the height source for the corner's own junction makes the kerb
     * exactly the kerb.
     *
     * The GROUND, in the terms BuildingFooting works in. What the floor's own outline takes
     * is the ROAD height at the same junction - generation.RoadSurface.HeightAtJunction,
     * which is this plus the street offset and the deck - because the kerb has to meet a
     * carriageway written by a different operator and a shared quantity gets one
     * expression. The two differ only by constants that are zero on the ground.
     *
     * A flat city answers with AverageHeight, exactly, because FlatStreetHeight does -
     * there is no fit in this path to round-trip through.
     */
    public float CornerGroundHeightAt(in QuarterDelim delim)
    {
        return ClusterDesc.StreetHeightSource.GroundHeightAt(delim.StreetPoint);
    }


    /**
     * World space, for the many callers that have one rather than a cluster coordinate.
     */
    public float GroundHeightAtWorld(in Vector3 v3World)
    {
        return GroundHeightAt(new Vector2(
            v3World.X - ClusterDesc.Pos.X,
            v3World.Z - ClusterDesc.Pos.Z));
    }

    public void AddEstate(in Estate estate)
    {
        lock (_lo)
        {
            estate.Quarter = this;
            _estates.Add(estate);
        }
    }


    /**
     * Compute things like the quarter center.
     */
    public void Polish()
    {
    }

    public void AddDebugTag(string key, string value)
    {
        lock (_lo)
        {
            _debugMap[key] = value;
        }
    }

    public string GetDebugString()
    {
        lock (_lo)
        {
            string s = "{\n";
            foreach (KeyValuePair<string, string> kvp in _debugMap)
            {
                var value = kvp.Value;
                s += $"'{kvp.Key}': '{kvp.Value}',\n";
            }

            s += "}\n";
            return s;
        }
    }

}
