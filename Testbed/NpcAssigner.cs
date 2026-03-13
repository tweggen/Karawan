using System;
using System.Collections.Generic;
using System.Linq;
using engine.tale;

namespace Testbed;

public enum NpcRole
{
    Worker,
    Merchant,
    Socialite,
    Drifter
}

public class NpcAssignment
{
    public int NpcId;
    public int Seed;
    public NpcRole Role;
    public int HomeLocationId;
    public int WorkplaceLocationId;
    public List<int> SocialVenueIds;

    public Dictionary<string, float> Properties;
}

public static class NpcAssigner
{
    public static List<NpcAssignment> Assign(SpatialModel model, int seed, int npcCount)
    {
        var assignments = new List<NpcAssignment>();

        // Partition locations by type
        var homes = model.Locations.Where(l => l.Type == "home").ToList();
        var workplaces = model.Locations.Where(l => l.Type == "workplace").ToList();
        var shops = model.Locations.Where(l => l.Type == "shop").ToList();
        var socialVenues = model.Locations.Where(l => l.Type == "social_venue").ToList();

        if (homes.Count == 0 || workplaces.Count == 0)
        {
            Console.WriteLine("Warning: insufficient locations for NPC assignment " +
                              $"(homes={homes.Count}, workplaces={workplaces.Count})");
        }

        // Merchants can also work at shops
        var merchantWorkplaces = shops.Concat(socialVenues).ToList();
        if (merchantWorkplaces.Count == 0) merchantWorkplaces = workplaces;

        var rng = new Random(seed);

        for (int i = 0; i < npcCount; i++)
        {
            int npcSeed = rng.Next();
            var npcRng = new Random(npcSeed);

            // Deterministic role from seed
            NpcRole role = (NpcRole)(npcRng.Next(100) switch
            {
                < 40 => (int)NpcRole.Worker,
                < 60 => (int)NpcRole.Merchant,
                < 80 => (int)NpcRole.Socialite,
                _ => (int)NpcRole.Drifter
            });

            // Pick home
            int homeId = -1;
            if (homes.Count > 0)
                homeId = homes[npcRng.Next(homes.Count)].Id;

            // Pick workplace based on role
            int workplaceId = -1;
            switch (role)
            {
                case NpcRole.Worker:
                    if (workplaces.Count > 0)
                        workplaceId = workplaces[npcRng.Next(workplaces.Count)].Id;
                    break;
                case NpcRole.Merchant:
                    if (merchantWorkplaces.Count > 0)
                        workplaceId = merchantWorkplaces[npcRng.Next(merchantWorkplaces.Count)].Id;
                    break;
                case NpcRole.Socialite:
                    // Socialites may work at social venues
                    if (socialVenues.Count > 0)
                        workplaceId = socialVenues[npcRng.Next(socialVenues.Count)].Id;
                    else if (workplaces.Count > 0)
                        workplaceId = workplaces[npcRng.Next(workplaces.Count)].Id;
                    break;
                case NpcRole.Drifter:
                    // Drifters have no fixed workplace
                    break;
            }

            // Pick 1-3 social venues
            var venueIds = new List<int>();
            if (socialVenues.Count > 0)
            {
                int venueCount = npcRng.Next(1, Math.Min(4, socialVenues.Count + 1));
                var available = new List<Location>(socialVenues);
                for (int v = 0; v < venueCount && available.Count > 0; v++)
                {
                    int idx = npcRng.Next(available.Count);
                    venueIds.Add(available[idx].Id);
                    available.RemoveAt(idx);
                }
            }

            // Base properties
            var props = new Dictionary<string, float>
            {
                { "anger", (float)(npcRng.NextDouble() * 0.2) },
                { "fear", (float)(npcRng.NextDouble() * 0.2) },
                { "trust", 0.5f },
                { "happiness", 0.3f + (float)(npcRng.NextDouble() * 0.4) },
                { "health", 0.7f + (float)(npcRng.NextDouble() * 0.3) },
                { "fatigue", (float)(npcRng.NextDouble() * 0.3) },
                { "hunger", (float)(npcRng.NextDouble() * 0.3) },
                { "wealth", role switch
                    {
                        NpcRole.Merchant => 0.4f + (float)(npcRng.NextDouble() * 0.4),
                        NpcRole.Worker => 0.2f + (float)(npcRng.NextDouble() * 0.4),
                        NpcRole.Socialite => 0.3f + (float)(npcRng.NextDouble() * 0.5),
                        NpcRole.Drifter => (float)(npcRng.NextDouble() * 0.3),
                        _ => 0.3f
                    }
                },
                { "reputation", 0.3f + (float)(npcRng.NextDouble() * 0.4) }
            };

            assignments.Add(new NpcAssignment
            {
                NpcId = i,
                Seed = npcSeed,
                Role = role,
                HomeLocationId = homeId,
                WorkplaceLocationId = workplaceId,
                SocialVenueIds = venueIds,
                Properties = props
            });
        }

        return assignments;
    }
}
