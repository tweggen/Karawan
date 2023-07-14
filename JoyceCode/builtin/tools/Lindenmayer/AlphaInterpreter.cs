using System.Collections.Generic;
using System;
using System.Numerics;
using static engine.Logger;

namespace builtin.tools.Lindenmayer;

class AlphaState {
    public Quaternion Rotation;
    public Vector3 Position;
    public Vector3 Color;

    public AlphaState( AlphaState parentState ) 
    {
        if (null==parentState)
        {
            Rotation = new Quaternion( 0f, 0f, 0f, 1f );
            Position = new Vector3(  0f, 0f, 0f );
            Color = new Vector3( 1f, 1f, 1f );
        } 
        else
        {
            Rotation = parentState.Rotation;
            Position = parentState.Position;
            Color = parentState.Color;
        }
    }
}


public class AlphaInterpreter
{

    private Instance _instance;

    private List<AlphaState> _stack;


    /**
     * Render the lindenmayer system within the scope of the current worldfragment.
     * 
     * @param worldFragment 
     *    The context to render in.
     * @param start 
     *    The start position.
     * @param targets 
     *    A map of geom atoms that are reused or generated.
     */
    public void Run(
        engine.world.Fragment? worldFragment,
        Vector3 start,
        SortedDictionary<string, engine.joyce.Mesh> targets
    )
    {
        var parts = _instance.State.Parts;

        var state = new AlphaState(null);
        state.Position = start;

        var matnameLeaves = "LAlphaInterpreter._matAlpha";
        engine.joyce.Mesh g = null;
        if (targets.ContainsKey(matnameLeaves))
        {
            g = targets[matnameLeaves];
            if (g == null)
            {
                Trace("Unable to generate, geom atom is non engine.PlainGeomAtom.");
                return;
            }
        }
        else
        {
            targets[matnameLeaves] = g = engine.joyce.Mesh.CreateListInstance();
        }

        foreach (Part part in parts)
        {
            // trace('Before part: rotation is ${state.rotation}.');
            // trace('Before part: position is ${state.position}.');
            // trace('Now part ${part.name}');
            var p = part.Parameters?.Map;
            if (part.Name == "rotate(d,x,y,z)")
            {
                var qrot = Quaternion.CreateFromAxisAngle(
                    new Vector3(p["x"], p["y"], p["z"]),
                    p["d"] / 180f * (float) Math.PI
                );
                state.Rotation = state.Rotation * qrot;
            }
            else if (part.Name == "fillrgb(r,g,b)")
            {
                state.Color = new Vector3(p["r"], p["g"], p["b"]);
            }
            else if (part.Name == "cyl(r,l)")
            {

                var vs = state.Position;

                /*
                 * vd shall be scaled with l
                 */
                var vd = new Vector3(1f, 0f, 0f);
                vd = Vector3.Transform(vd, state.Rotation);

                /*
                 * vRadius shall be scaled with r
                 */
                var vr = new Vector3(0f, 1f, 0f);
                vr = Vector3.Transform(vr, state.Rotation);

                Vector3 vt = Vector3.Cross(vd, vr);
                //Vector3 vt = Vector3.Cross(vr, vd);

                vd *= p["l"];
                vr *= p["r"];
                vt *= p["r"];
                // trace( 'LAlphaInterpreter.run(): From ${vs} direction ${vd} radius ${vr}.' );

                /*
                 * Make a trivial four sided poly.
                 */
                var poly = new List<Vector3>();
                poly.Add(vs + vr);
                poly.Add(vs + vt);
                poly.Add(vs - vt);
                var path = new List<Vector3>();
                path.Add(vd);
                // trace( 'poly: $poly' );
                var ext = new builtin.tools.ExtrudePoly(poly, path, 
                    27, 100f, false, false, false);
                ext.BuildGeom( g);
                state.Position += vd;
            }
            else if (part.Name == "flat(r,l)")
            {

                var vs = state.Position;

                /*
                 * vd shall be scaled with l
                 */
                var vd = new Vector3(1f, 0f, 0f);
                vd = Vector3.Transform(vd, state.Rotation);
                
                /*
                 * vRadius shall be scaled with r
                 */
                var vr = new Vector3(0f, 1f, 0f);
                vr = Vector3.Transform(vr,state.Rotation);

                Vector3 vt = Vector3.Cross(vd, vr);

                vd *= p["l"];
                vr *= p["r"];
                // vt *= p["r"];
                // trace( 'LAlphaInterpreter.run(): From ${vs} direction ${vd} radius ${vr}.' );

                /*
                 * Make a trivial four sided poly.
                 */
                var poly = new List<Vector3>();
                poly.Add(vs + vr);
                //poly.push( new geom.Vector3D( vs.x + vt.x, vs.y + vt.y ,vs.z + vt.z ) );
                poly.Add(vs - vr); 
                // poly.push( new geom.Vector3D( vs.x - vt.x, vs.y - vt.y ,vs.z - vt.z ) );
                var path = new List<Vector3>();
                path.Add(vd);
                // trace( 'poly: $poly' );
                var ext = new builtin.tools.ExtrudePoly(poly, path, 27, 100f, false, false, false);
                ext.BuildGeom( g);
                state.Position += vd;
            }
            else if (part.Name == "push()")
            {
                _stack.Add(state);
                state = new AlphaState(state);
            }
            else if (part.Name == "pop()")
            {
                state = _stack[_stack.Count - 1];
                _stack.RemoveAt(_stack.Count - 1);
            }
            else
            {
                Warning($"Unknown part {part}.");
            }
        }
    }


    public AlphaInterpreter( Instance instance ) {
        _instance = instance;
        _stack = new List<AlphaState>();
    }
}
