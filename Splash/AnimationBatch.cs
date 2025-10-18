using System.Collections.Generic;
using System.Numerics;
using engine.joyce;
using engine.joyce.components;

namespace Splash;

public class AnimationBatch
{
    public readonly AAnimationsEntry? AAnimationsEntry;
    
    /**
     * If we have a specific animation state in thie batch, this is it.
     * This is valid if we group animation batches by animation or animation plus frame.
     * We might want to this fore timed LOD or due to constraints of the
     * underlying rendering layer.
     */
    public AnimationState? AnimationState;

    /**
     * The animation frame numbers of the models to render.
     */
    public readonly List<uint> FrameNos = new();
    
    /**
     * The transformations of the individual meshes. 
     */
    public readonly List<Matrix4x4> Matrices = new();


    public Vector3 SumOfPositions = Vector3.Zero;

    public Vector3 AveragePosition
    {
        get => Matrices.Count > 0 ? SumOfPositions / Matrices.Count : Vector3.Zero;
    }


    public void Sort(Vector3 v3CameraPos)
    {
        if (Matrices != null && Matrices.Count > 0)
        {
            Matrices.Sort((b, a) =>
            {
                float da = (a.Translation - v3CameraPos).LengthSquared();
                float db = (b.Translation - v3CameraPos).LengthSquared();
                if (da < db)
                {
                    return -1;
                }
                else if (da > db)
                {
                    return 1;
                }

                return 0;
            });
        }
    }

    public AnimationBatch(in AAnimationsEntry aAnimationsEntry)
    {
        AAnimationsEntry = aAnimationsEntry;
    }
}
