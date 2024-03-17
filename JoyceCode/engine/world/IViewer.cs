using System.Collections.Generic;
using System.Runtime.CompilerServices;
using engine.joyce;

namespace engine.world;


/**
 * A viewer is an abstraction that defines what a particular
 * loader is required to have loaded at a given instance.
 *
 * To be more precise:
 * The world is divided into fragment sized pieces. This
 * viewer emits a list of fragments and their visibility
 * properties.
 *
 * A loader then is responsible to make the data available
 * as described by the viewer.
 *
 * The "real" 3d world, the world maps and the local maps
 * have different viewers describing the respective requirements.
 *
 */
public interface IViewer
{
    public void GetVisibleFragments(ref IList<FragmentVisibility> lsVisib);
}