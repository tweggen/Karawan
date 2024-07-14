using System.Collections.Generic;
using System;
using System.Numerics;
using BepuPhysics;
using engine;
using engine.joyce;
using engine.physics;
using FbxSharp;
using static builtin.extensions.JsonObjectNumerics;
using static engine.Logger;

namespace builtin.tools.Lindenmayer;

class AlphaState {
    public Quaternion Rotation;
    public Vector3 Position;
    public Vector3 Color;
    public string Material;

    public AlphaState( AlphaState parentState ) 
    {
        if (null==parentState)
        {
            Rotation = new Quaternion( 0f, 0f, 0f, 1f );
            Position = new Vector3(  0f, 0f, 0f );
            Color = new Vector3( 1f, 1f, 1f );
            Material = "";
        } 
        else
        {
            Rotation = parentState.Rotation;
            Position = parentState.Position;
            Color = parentState.Color;
            Material = parentState.Material;
        }
    }
}


internal class AlphaResources
{
    public AlphaResources()
    {
        I.Get<ObjectRegistry<Material>>().RegisterFactory("nogame.cities.trees.materials.treeleave",
            (name) => new Material()
            {
                Texture = I.Get<TextureCatalogue>().FindColorTexture(0xff448822)
            });

    }
}


public class AlphaInterpreter
{

    private Instance _instance;

    private List<AlphaState> _stack;

    private Lazy<AlphaResources> _alphaResources = new (new AlphaResources());

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
        MatMesh mmTarget,
        IList<Func<IList<StaticHandle>, Action>> listCreatePhysicsTarget
    )
    {
        var alphaResources = _alphaResources.Value;
        var parts = _instance.State.Parts;
        
        var state = new AlphaState(null);
        state.Position = start;

        var matnameLeaves = "nogame.cities.trees.materials.treeleave";
        var matLeaves = I.Get<ObjectRegistry<Material>>().Get(matnameLeaves);
        
        foreach (Part part in parts)
        {
            // trace('Before part: rotation is ${state.rotation}.');
            // trace('Before part: position is ${state.position}.');
            // trace('Now part ${part.name}');
            var p = part.Parameters?.Map;
            switch (part.Name)
            {
                case "rotate(d,x,y,z)":
                {
                    var qrot = Quaternion.CreateFromAxisAngle(
                        new Vector3((float)p["x"], (float)p["y"], (float)p["z"]),
                        (float)p["d"] / 180f * (float)Math.PI
                    );
                    state.Rotation = state.Rotation * qrot;
                    break;
                }

                
                case "fillrgb(r,g,b)":
                {
                    state.Color = new Vector3((float)p["r"], (float)p["g"], (float)p["b"]);
                    break;
                }

                
                case "cyl(r,l)":
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

                    vd *= (float)p["l"];
                    vr *= (float)p["r"];
                    vt *= (float)p["r"];
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
                    engine.joyce.Mesh meshExtrusion = new("mesh_cyl_rl");
                    ext.BuildGeom(meshExtrusion);
                    state.Position += vd;
                    mmTarget.Add(matLeaves, meshExtrusion);

                    break;
                }


                case "flat(r,l)":
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

                    vd *= (float)p["l"];
                    vr *= (float)p["r"];
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
                    engine.joyce.Mesh meshExtrusion = new("mesh_flat_rl");
                    var ext = new ExtrudePoly(poly, path, 27, 100f, false, false, false);
                    ext.BuildGeom(meshExtrusion);
                    state.Position += vd;
                    mmTarget.Add(matLeaves, meshExtrusion);

                    break;
                }

                case "extrudePoly(A,h,mat)":
                {
                    var listEdges = new List<Vector3>(ToVector3List(p["A"]));
                    for (int i = 0; i < listEdges.Count; ++i)
                    {
                        listEdges[i] += state.Position;
                    }
                    var path = new List<Vector3>();
                    Vector3 v3h = new Vector3(0f, (float)p["h"], 0f);
                    path.Add( v3h);

                    engine.joyce.Mesh meshExtrusion = new("mesh_flat_rl");
                    state.Material = (string)p["mat"];
                    var opExtrudePoly = new ExtrudePoly(
                        listEdges, 
                        path, 
                        27, /* magic for houses */ 
                        24f,/* standard for windows */
                        false, true, true /* standard values for houses */
                        )
                    {
                        /* also standard values for houses */
                        PairedNormals = true,
                        TileToTexture = true
                    };
                    opExtrudePoly.BuildGeom(meshExtrusion);
                    state.Position += v3h;
                    mmTarget.Add(I.Get<ObjectRegistry<Material>>().Get(state.Material), meshExtrusion);



                    if (null != listCreatePhysicsTarget)
                    {
                        /*
                         * Finally, add physics for this part.
                         */
                        CollisionProperties props = new()
                        {
                            Flags =
                                CollisionProperties.CollisionFlags.IsTangible
                                | CollisionProperties.CollisionFlags.IsDetectable,
                            Name = $"house-{listEdges[0] + worldFragment.Position}",
                        };
                        try
                        {
                            var fCreatePhysics = opExtrudePoly.BuildStaticPhys(worldFragment, props);
                            listCreatePhysicsTarget.Add(fCreatePhysics);
                        }
                        catch (Exception e)
                        {
                            Trace($"Unknown exception creating extrusion physics: {e}");
                        }
                    }

                    break;
                }
                

                case "push()":
                {
                    _stack.Add(state);
                    state = new AlphaState(state);
                    break;
                }
                

                case "pop()":
                {
                    state = _stack[_stack.Count - 1];
                    _stack.RemoveAt(_stack.Count - 1);
                    break;
                }
                

                default:
                    Warning($"Unknown part {part}.");
                    break;
            }
        }
    }


    public AlphaInterpreter( Instance instance ) {
        _instance = instance;
        _stack = new List<AlphaState>();
    }
}

