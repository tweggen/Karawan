using System.Collections.Generic;
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
            Position = new Vector3( 0f, 0f, 0f );
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
        engine.world.Fragment worldFragment,
        Vector3 start,
        SortedDictionary<string, engine.joyce.Mesh> targets
    )
    {
        var parts = _instance.State.Parts;

        var state = new AlphaState(null);
        if (start != null)
        {
            state.Position = start;
        }

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
            targets[matnameLeaves] = g = new engine.joyce.Mesh(
                null, null, null);
        }

        foreach (Part part in parts)
        {
            // trace('Before part: rotation is ${state.rotation}.');
            // trace('Before part: position is ${state.position}.');
            // trace('Now part ${part.name}');
            var p = part.Parameters;
            if (part.Name == "rotate(d,x,y,z)")
            {
                var qrot = Quaternion.CreateFromAxisAngle(
                    new Vector3(p["x"], p["y"], p["z"],
                    p["d"] / 180.*Math.PI
                    )
                );
                state.rotation.multiplyAsA(qrot);
            }
            else if (part.name == "fillrgb(r,g,b)")
            {
                state.color = new geom.Vector3D(p["r"], p["g"], p["b"]);
            }
            else if (part.name == "cyl(r,l)")
            {

                var vs = state.position.clone();

                /*
                 * vd shall be scaled with l
                 */
                var vd = new geom.Vector3D(1., 0., 0.);
                vd.applyQuaternion(state.rotation);
                /*
                 * vRadius shall be scaled with r
                 */
                var vr = new geom.Vector3D(0., 1., 0.);
                vr.applyQuaternion(state.rotation);

                var vt = vr.clone();
                vt.cross(vd);

                vd.scale(p["l"]);
                vr.scale(p["r"]);
                vt.scale(p["r"]);
                // trace( 'LAlphaInterpreter.run(): From ${vs} direction ${vd} radius ${vr}.' );

                /*
                 * Make a trivial four sided poly.
                 */
                var poly = new Array<geom.Vector3D>();
                poly.push(new geom.Vector3D(vs.x + vr.x, vs.y + vr.y, vs.z + vr.z));
                poly.push(new geom.Vector3D(vs.x + vt.x, vs.y + vt.y, vs.z + vt.z));
                //poly.push( new geom.Vector3D( vs.x - vr.x, vs.y - vr.y ,vs.z - vr.z ) );
                poly.push(new geom.Vector3D(vs.x - vt.x, vs.y - vt.y, vs.z - vt.z));
                var path = new Array<geom.Vector3D>();
                path.push(new geom.Vector3D(vd.x, vd.y, vd.z));
                // trace( 'poly: $poly' );
                var ext = new ops.geom.ExtrudePoly(poly, path, 27, 100., false, false, false);
                ext.buildGeom(worldFragment, g);
                state.position.add(vd);
            }
            else if (part.name == "flat(r,l)")
            {

                var vs = state.position.clone();

                /*
                 * vd shall be scaled with l
                 */
                var vd = new geom.Vector3D(1., 0., 0.);
                vd.applyQuaternion(state.rotation);
                /*
                 * vRadius shall be scaled with r
                 */
                var vr = new geom.Vector3D(0., 1., 0.);
                vr.applyQuaternion(state.rotation);

                var vt = vr.clone();
                vt.cross(vd);

                vd.scale(p["l"]);
                vr.scale(p["r"]);
                vt.scale(p["r"]);
                // trace( 'LAlphaInterpreter.run(): From ${vs} direction ${vd} radius ${vr}.' );

                /*
                 * Make a trivial four sided poly.
                 */
                var poly = new Array<geom.Vector3D>();
                poly.push(new geom.Vector3D(vs.x + vr.x, vs.y + vr.y, vs.z + vr.z));
                //poly.push( new geom.Vector3D( vs.x + vt.x, vs.y + vt.y ,vs.z + vt.z ) );
                poly.push(new geom.Vector3D(vs.x - vr.x, vs.y - vr.y, vs.z - vr.z));
                // poly.push( new geom.Vector3D( vs.x - vt.x, vs.y - vt.y ,vs.z - vt.z ) );
                var path = new Array<geom.Vector3D>();
                path.push(new geom.Vector3D(vd.x, vd.y, vd.z));
                // trace( 'poly: $poly' );
                var ext = new ops.geom.ExtrudePoly(poly, path, 27, 100., false, false, false);
                ext.buildGeom(worldFragment, g);
                state.position.add(vd);
            }
            else if (part.name == "push()")
            {
                _stack.push(state);
                state = new LAlphaState(state);
            }
            else if (part.name == "pop()")
            {
                state = _stack.pop();
            }
            else
            {
                trace('LAlphaInterpreter.run(): Unknown part $part.');
            }
        }
    }


    public function new
    ( lInstance: LInstance ) {
        _lInstance = lInstance;
        _stack = new Array<LAlphaState>();
    }
}