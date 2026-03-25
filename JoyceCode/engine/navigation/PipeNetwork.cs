using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace engine.navigation;

/// <summary>
/// Collection of pipes for a specific transportation type.
/// </summary>
public class PipeNetwork
{
    /// <summary>
    /// All pipes in this network.
    /// </summary>
    public List<Pipe> Pipes { get; set; } = new();

    /// <summary>
    /// Transportation type this network serves.
    /// </summary>
    public TransportationType SupportedType { get; set; }

    /// <summary>
    /// Find the pipe containing a specific position.
    /// </summary>
    public Pipe? FindPipeContaining(Vector3 position)
    {
        return Pipes.FirstOrDefault(pipe => pipe.ContainsPosition(position));
    }

    /// <summary>
    /// Find all pipes that could be connected at a junction.
    /// </summary>
    public List<Pipe> FindOutgoingPipes(Pipe fromPipe)
    {
        // Find pipes that start where this pipe ends
        var outgoing = Pipes
            .Where(p => p != fromPipe &&
                       Vector3.Distance(p.StartPosition, fromPipe.EndPosition) < 1.0f)
            .ToList();

        return outgoing;
    }

    /// <summary>
    /// Get total entity count across all pipes.
    /// </summary>
    public int GetTotalEntityCount()
    {
        return Pipes.Sum(p => p.CurrentOccupancy);
    }

    public override string ToString()
        => $"PipeNetwork({SupportedType}, pipes={Pipes.Count}, entities={GetTotalEntityCount()})";
}
