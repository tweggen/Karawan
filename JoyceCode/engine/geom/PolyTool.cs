using System.Collections.Generic;
using System.Numerics;
using ClipperLib;

namespace engine.geom;

public class PolyTool
{
    private Vector3 _v3Up;
    private IEnumerable<Vector3> _listV3;

    public IEnumerable<Vector3> Extend(float d)
    {
        List<Vector3> p = new();

        List<IntPoint> polyPoints = new();
        List<List<IntPoint>> polyList = new();
        float polyY = 0f;
        polyList.Add(polyPoints);
        var clipperOffset = new ClipperOffset();
        foreach (var point in _listV3)
        {
            polyY = point.Y;
            polyPoints.Add(new IntPoint((int)(point.X * 100f), (int)(point.Z * 100f)));
        }

        if (polyPoints.Count > 0)
        {
            clipperOffset.AddPaths(polyList, JoinType.jtMiter, EndType.etClosedPolygon);
            List<List<IntPoint>> solution2 = new();


            clipperOffset.Execute(ref solution2, d*100f);

            foreach (var polygon in solution2)
            {
                foreach (var point in polygon)
                {
                    float x = point.X / 100f;
                    float y = point.Y / 100f;
                    p.Insert(0, new Vector3(x, polyY, y));
                }
            }

        }

        return p;
    }

    public PolyTool(in IEnumerable<Vector3> listV3, in Vector3 v3Up)
    {
        _listV3 = listV3;
        _v3Up = v3Up;
    }
}