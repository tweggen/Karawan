using engine;
using engine.tale;
using engine.tale.bake;
using Xunit;

namespace JoyceCode.Tests.engine.tale.bake;

/// <summary>
/// Determinism smoke test for ScenarioCompiler. Runs CompileInMemory twice
/// with identical inputs and compares the resulting Scenario field-by-field.
/// This catches any unseeded RNG path inside the synthetic spatial model
/// builder, the synthetic NPC builder, or the underlying DesSimulation
/// (which would otherwise let two builds of the same sc-{hash} drift apart
/// — silently breaking the bake/runtime contract).
///
/// The test uses a tiny configuration (12 NPCs, 5 simulation days) to keep
/// runtime tractable: a full 365-day bake takes ~2 seconds per scenario,
/// but a 5-day one finishes in milliseconds and is enough to exercise the
/// initialization + a few day-cycle iterations of the same code paths.
///
/// Uses <see cref="IClassFixture{T}"/> so the four TALE registries (which
/// DesSimulation.SeedNpc reaches for via I.Get) are registered once for
/// the whole test class.
/// </summary>
public class ScenarioCompilerTests : IClassFixture<ScenarioCompilerTests.RegistryFixture>
{
    public class RegistryFixture
    {
        public RegistryFixture()
        {
            // Mirrors TestRunner/TestRunnerMain.cs and Chushi/ConsoleMain.cs:
            // ScenarioCompiler runs DesSimulation, which calls
            // I.Get<RoleRegistry>() inside SeedNpc, so the singleton must
            // exist before any Compile call.
            //
            // I.Register is idempotent-friendly: re-registration logs a
            // warning but does not throw, so this is safe even if another
            // test class also registered earlier.
            try { I.Register<RoleRegistry>(() => MakeRoleRegistry()); } catch { }
            try { I.Register<InteractionTypeRegistry>(() => MakeInteractionTypeRegistry()); } catch { }
            try { I.Register<RelationshipTierRegistry>(() => new RelationshipTierRegistry()); } catch { }
            try { I.Register<GroupTypeRegistry>(() => new GroupTypeRegistry()); } catch { }

            // ScenarioCompiler.LoadStoryletLibrary reads JSON storylet
            // definitions from {Engine.ResourcePath}/tale. With an empty
            // library DesSimulation.ProcessNodeArrival eventually hands a
            // null StoryletDefinition to ResolveLocation and crashes — so
            // we point Engine.ResourcePath at the repo's models/ directory
            // before the first Compile call. Walks up from the test
            // assembly's location to find models/nogame.json, mirroring
            // TestbedMain._determineResourcePath / TestRunnerMain._determineResourcePath.
            string resourcePath = FindModelsDirectory();
            if (resourcePath != null)
            {
                GlobalSettings.Set("Engine.ResourcePath", resourcePath);
            }
        }

        private static string FindModelsDirectory()
        {
            string dir = System.IO.Directory.GetCurrentDirectory();
            for (int i = 0; i < 10; i++)
            {
                string candidate = System.IO.Path.Combine(dir, "models", "nogame.json");
                if (System.IO.File.Exists(candidate))
                    return System.IO.Path.Combine(dir, "models") + System.IO.Path.DirectorySeparatorChar;
                string parent = System.IO.Path.GetDirectoryName(dir);
                if (parent == null || parent == dir) break;
                dir = parent;
            }
            return null;
        }

        private static RoleRegistry MakeRoleRegistry()
        {
            var r = new RoleRegistry();
            r.Add("worker",      new RoleDefinition { Id = "worker",      DisplayName = "Worker",             DefaultWeight = 0.30f });
            r.Add("merchant",    new RoleDefinition { Id = "merchant",    DisplayName = "Merchant",           DefaultWeight = 0.13f });
            r.Add("socialite",   new RoleDefinition { Id = "socialite",   DisplayName = "Socialite",          DefaultWeight = 0.15f });
            r.Add("drifter",     new RoleDefinition { Id = "drifter",     DisplayName = "Drifter",            DefaultWeight = 0.12f });
            r.Add("authority",   new RoleDefinition { Id = "authority",   DisplayName = "Authority",          DefaultWeight = 0.10f });
            r.Add("nightworker", new RoleDefinition { Id = "nightworker", DisplayName = "Night Shift Worker", DefaultWeight = 0.08f });
            r.Add("hustler",     new RoleDefinition { Id = "hustler",     DisplayName = "Street Hustler",     DefaultWeight = 0.07f });
            r.Add("reveler",     new RoleDefinition { Id = "reveler",     DisplayName = "Night Owl",          DefaultWeight = 0.05f });
            return r;
        }

        private static InteractionTypeRegistry MakeInteractionTypeRegistry()
        {
            var r = new InteractionTypeRegistry();
            r.Add("greet", new InteractionTypeDefinition { Id = "greet", DisplayName = "Greeting", TrustDelta = 0.04f });
            r.Add("chat",  new InteractionTypeDefinition { Id = "chat",  DisplayName = "Chat",     TrustDelta = 0.06f });
            r.FinalizeOrder();
            return r;
        }
    }

    public ScenarioCompilerTests(RegistryFixture _) { }

    [Fact]
    public void CompileInMemory_TwoCalls_SameSeedSameOutput()
    {
        var compiler1 = new ScenarioCompiler
        {
            CategoryName = "test",
            Index = 0,
            NpcCount = 12,
            Seed = 4242,
            SimulationDays = 5,
            OutputDirectory = ""
        };
        var compiler2 = new ScenarioCompiler
        {
            CategoryName = "test",
            Index = 0,
            NpcCount = 12,
            Seed = 4242,
            SimulationDays = 5,
            OutputDirectory = ""
        };

        var a = compiler1.CompileInMemory();
        var b = compiler2.CompileInMemory();

        Assert.NotNull(a);
        Assert.NotNull(b);
        Assert.Equal(a.NpcCount, b.NpcCount);
        Assert.Equal(a.Npcs.Count, b.Npcs.Count);
        Assert.Equal(a.Groups.Count, b.Groups.Count);
        Assert.Equal(a.Relationships.Count, b.Relationships.Count);

        for (int i = 0; i < a.Npcs.Count; i++)
        {
            Assert.Equal(a.Npcs[i].Rank, b.Npcs[i].Rank);
            Assert.Equal(a.Npcs[i].Role, b.Npcs[i].Role);
            Assert.Equal(a.Npcs[i].GroupRank, b.Npcs[i].GroupRank);
            foreach (var key in a.Npcs[i].Properties.Keys)
            {
                Assert.True(b.Npcs[i].Properties.ContainsKey(key));
                Assert.Equal(a.Npcs[i].Properties[key], b.Npcs[i].Properties[key], 5);
            }
        }

        for (int i = 0; i < a.Relationships.Count; i++)
        {
            Assert.Equal(a.Relationships[i].FromRank, b.Relationships[i].FromRank);
            Assert.Equal(a.Relationships[i].ToRank, b.Relationships[i].ToRank);
            Assert.Equal(a.Relationships[i].TrustAtoB, b.Relationships[i].TrustAtoB, 5);
            Assert.Equal(a.Relationships[i].TrustBtoA, b.Relationships[i].TrustBtoA, 5);
        }
    }

    [Fact]
    public void CompileInMemory_DifferentSeed_DifferentOutput()
    {
        var compiler1 = new ScenarioCompiler
        {
            CategoryName = "test", Index = 0, NpcCount = 12,
            Seed = 1111, SimulationDays = 5, OutputDirectory = ""
        };
        var compiler2 = new ScenarioCompiler
        {
            CategoryName = "test", Index = 0, NpcCount = 12,
            Seed = 2222, SimulationDays = 5, OutputDirectory = ""
        };

        var a = compiler1.CompileInMemory();
        var b = compiler2.CompileInMemory();

        Assert.NotNull(a);
        Assert.NotNull(b);
        // Either roles or properties or relationships must differ between
        // two seeds — extremely unlikely to be byte-identical by chance.
        bool anyDiff = false;
        for (int i = 0; i < a.Npcs.Count && !anyDiff; i++)
        {
            if (a.Npcs[i].Role != b.Npcs[i].Role) { anyDiff = true; break; }
            foreach (var key in a.Npcs[i].Properties.Keys)
            {
                if (b.Npcs[i].Properties.TryGetValue(key, out float bv)
                    && System.Math.Abs(a.Npcs[i].Properties[key] - bv) > 1e-5f)
                {
                    anyDiff = true;
                    break;
                }
            }
        }
        Assert.True(anyDiff, "Two scenarios with different seeds produced identical output — unseeded code path?");
    }
}
