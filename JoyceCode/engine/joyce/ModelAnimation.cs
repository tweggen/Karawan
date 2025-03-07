using System.Collections.Generic;

namespace engine.joyce;

/**
 * Represents one specific animation that can be applied to a skeleton.
 */
public class ModelAnimation
{
    public int Index;
    public string Name;
    public float Duration;
    public float TicksPerSecond;
    public SortedDictionary<ModelNode, ModelAnimChannel> MapChannels;
    public ModelAnimChannel[] Channels;
}