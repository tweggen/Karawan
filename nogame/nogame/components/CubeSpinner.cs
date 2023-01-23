using System.Numerics;

namespace nogame.components
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
