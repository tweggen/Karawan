using System.Numerics;

namespace Karawan.nogame.components
{
    struct CubeSpinner
    {
        public Quaternion Spin;

        public CubeSpinner( in Quaternion spin )
        {
            Spin = spin;
        }
    }
}
