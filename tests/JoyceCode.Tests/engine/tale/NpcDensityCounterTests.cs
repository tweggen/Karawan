using System;
using System.Collections.Generic;
using System.Numerics;
using engine.tale;
using Xunit;

namespace JoyceCode.Tests.engine.tale;

/// <summary>
/// TALE S0 W9 (KAR-452): the density counter shared by the headless arrival
/// measurement and the in-game cross-check.
/// </summary>
public class NpcDensityCounterTests
{
    private static readonly DateTime Noon = new(2000, 1, 2, 12, 0, 0);

    private static SpatialModel MakeModel()
    {
        var model = new SpatialModel();
        model.Locations.Add(new Location { Id = 1, Type = "home", Position = new Vector3(0, 0, 0) });
        model.Locations.Add(new Location { Id = 2, Type = "office", Position = new Vector3(50, 0, 0) });
        model.Locations.Add(new Location { Id = 3, Type = "social_venue", Position = new Vector3(0, 0, 50) });
        model.Locations.Add(new Location { Id = 4, Type = "street_segment", Position = new Vector3(-50, 0, 0) });
        model.Locations.Add(new Location { Id = 5, Type = "home", Position = new Vector3(500, 0, 0) });
        model.BuildIndex();
        return model;
    }

    private static NpcSchedule At(int id, int locationId) => new()
    {
        NpcId = id, HomeLocationId = 1, WorkplaceLocationId = 2, CurrentLocationId = locationId,
        Properties = new Dictionary<string, float>(), Trust = new Dictionary<int, float>(),
    };


    [Fact]
    public void ClassifiesByWhereabouts()
    {
        var model = MakeModel();
        var npcs = new List<NpcSchedule>
        {
            At(1, 1),   // at home
            At(2, 2),   // at work
            At(3, 3),   // in a venue
            At(4, 4),   // standing on the street
            At(5, 5),   // someone else's home, 500 m away: out of range
        };
        // Walking from home to the far home: halfway, 250 m out, on the street.
        var walker = At(6, 5);
        walker.IsInTransit = true;
        walker.TransitFromLocationId = 1;
        walker.TransitToLocationId = 5;
        walker.TransitStart = Noon.AddMinutes(-10);
        walker.TransitEnd = Noon.AddMinutes(10);
        npcs.Add(walker);

        var near = NpcDensityCounter.CountNear(npcs, model, Vector3.Zero, 100f, Noon);
        Assert.Equal(new NpcDensity(Street: 1, Venue: 1, HomeOrWork: 2), near);

        var wide = NpcDensityCounter.CountNear(npcs, model, Vector3.Zero, 300f, Noon);
        Assert.Equal(new NpcDensity(Street: 2, Venue: 1, HomeOrWork: 2), wide);
    }


    [Fact]
    public void TransitOutsideItsWindowCountsAtTheDestination()
    {
        var model = MakeModel();
        var arrived = At(1, 3);
        arrived.IsInTransit = true;            // flag left set after arrival
        arrived.TransitFromLocationId = 4;
        arrived.TransitToLocationId = 3;
        arrived.TransitStart = Noon.AddMinutes(-30);
        arrived.TransitEnd = Noon.AddMinutes(-20);

        var d = NpcDensityCounter.CountNear(new[] { arrived }, model, Vector3.Zero, 100f, Noon);
        Assert.Equal(new NpcDensity(0, 1, 0), d);
    }


    [Fact]
    public void LocatedOnceCountsLikeDirect()
    {
        var model = MakeModel();
        var npcs = new[] { At(1, 1), At(2, 3), At(3, 4) };
        var located = NpcDensityCounter.Locate(npcs, model, Noon);
        foreach (var centre in new[] { Vector3.Zero, new Vector3(-50, 0, 0), new Vector3(0, 0, 60) })
        {
            Assert.Equal(NpcDensityCounter.CountNear(npcs, model, centre, 40f, Noon),
                NpcDensityCounter.CountNear(located, centre, 40f));
        }
    }
}
