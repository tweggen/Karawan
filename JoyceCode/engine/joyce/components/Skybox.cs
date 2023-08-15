using System;
using System.Collections.Generic;
using System.Text;

namespace engine.joyce.components
{
    /**
     * Transformation and anchor for skyboxes.
     * Skyboxes do not have any transformation applied, they 
     * are just rendered as is, without even translating.
     */
    public struct Skybox
    {
        public float Distance;
        public uint CameraMask;


        public override string ToString()
        {
            return $"Distance: {Distance}, CameraMask {CameraMask:X}";
        }
        
        public Skybox(float distance, uint cameraMask)
        {
            Distance = distance;
            CameraMask = cameraMask;
        }
    }
}
