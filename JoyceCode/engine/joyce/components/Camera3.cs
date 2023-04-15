using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace engine.joyce.components
{
    public struct Camera3
    {
        public float Angle;
        public float NearFrustum;
        public float FarFrustum;
        public uint CameraMask;
        
        public override string ToString()
        {
            return $"{base.ToString()}, Angle={Angle}, NearFrustum={NearFrustum}, NearFrustum={NearFrustum}, CameraMask={CameraMask:X}";
        }
    }
}
