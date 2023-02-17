using System;
using System.Collections.Generic;
using System.Text;

namespace JoyceCode.engine.transform.components
{
    /**
     * Transformation and anchor for skyboxes.
     * Skyboxes do not have any transformation applied, they 
     * are just rendered as is, without even translating.
     */
    public struct Skybox
    {
        public float Distance;

        public Skybox(float distance)
        {
            Distance = distance;
        }
    }
}
