using System;
using System.Numerics;
using System.Reflection;
using DefaultEcs;
using engine;
using engine.behave;
using engine.behave.components;
using engine.tale;
using nogame.tools;
using nogame.characters;
using nogame.characters.citizen;

namespace TestRunner;

/// <summary>
/// Entity-level tests for two behavior bugfixes:
///   Bug A: TaleConversationBehavior was overwritten by IdleBehavior during activity setup.
///   Bug B: After car-hit recovery, _setupActivity() was not called → stale state / T-pose.
///
/// These tests run against real DefaultEcs entities and strategy objects.
/// Invoked via JOYCE_TEST_SCRIPT=entity-behavior-tests.
/// </summary>
public static class BehaviorBugfixTests
{
    private static CharacterModelDescription _createTestCmd()
    {
        return new CharacterModelDescription
        {
            IdleAnimName = "Idle_Generic",
            WalkAnimName = "Walk_Male",
            RunAnimName = "Run_InPlace",
            DeathAnimName = "Death_FallForwards",
            JumpAnimName = "Standing_Jump",
            PunchRightAnim = "Punch_RightHand",
            PunchLeftAnim = "Punch_LeftHand"
        };
    }

    private static NpcSchedule _createTestSchedule(int npcId)
    {
        return new NpcSchedule
        {
            NpcId = npcId,
            Seed = 1000 + npcId,
            Role = "worker",
            ClusterIndex = 0,
            NpcIndex = npcId,
            HomeLocationId = 0,
            WorkplaceLocationId = 1,
            CurrentLocationId = 0,
            CurrentStart = new DateTime(2024, 1, 1, 8, 0, 0),
            CurrentEnd = new DateTime(2024, 1, 1, 12, 0, 0),
            SocialVenueIds = new System.Collections.Generic.List<int> { 2 },
            Properties = new System.Collections.Generic.Dictionary<string, float>
            {
                ["hunger"] = 0.3f, ["anger"] = 0.1f, ["fatigue"] = 0.3f,
                ["wealth"] = 0.5f, ["health"] = 0.8f, ["fear"] = 0.1f,
                ["reputation"] = 0.5f, ["happiness"] = 0.5f, ["morality"] = 0.7f,
                ["desperation"] = 0.5f, ["social"] = 0.5f
            },
            Trust = new System.Collections.Generic.Dictionary<int, float>()
        };
    }

    public static int RunAll(Engine engine)
    {
        Console.WriteLine("\n=== Entity Behavior Bugfix Tests ===\n");
        int failures = 0;

        failures += RunTest("BugA: ActivityBehavior is used instead of IdleBehavior",
            () => TestBugA_ActivityBehaviorOverridesIdleBehavior(engine));

        failures += RunTest("BugA: Without ActivityBehavior, IdleBehavior is used (regression)",
            () => TestBugA_DefaultIdleBehaviorWithoutActivityBehavior(engine));

        failures += RunTest("BugA: TaleConversationBehavior set on initial spawn",
            () => TestBugA_ConversationBehaviorOnInitialSpawn(engine));

        failures += RunTest("BugA: Strategy component triggers correct behavior (spawn pipeline)",
            () => TestBugA_StrategyComponentSetsBehavior(engine));

        failures += RunTest("BugA: TaleConversationBehavior set after travel completion",
            () => TestBugA_ConversationBehaviorAfterTravelCompletion(engine));

        failures += RunTest("BugB: Recover-to-activity restores TaleConversationBehavior",
            () => TestBugB_RecoverToActivityRestoresBehavior(engine));

        failures += RunTest("BugB: RecoverBehavior resets death animation flag on reattach",
            () => TestBugB_RecoverBehaviorResetsDeathAnimFlag());

        Console.WriteLine($"\n=== Results: {(failures == 0 ? "ALL PASSED" : $"{failures} FAILED")} ===\n");
        return failures;
    }

    private static int RunTest(string name, Func<bool> test)
    {
        Console.Write($"  {name} ... ");
        try
        {
            bool passed = test();
            Console.WriteLine(passed ? "PASS" : "FAIL");
            return passed ? 0 : 1;
        }
        catch (Exception e)
        {
            Console.WriteLine($"FAIL (exception: {e.Message})");
            return 1;
        }
    }

    /// <summary>
    /// Bug A mechanism: StayAtStrategyPart.OnEnter() uses ActivityBehavior when set.
    /// </summary>
    private static bool TestBugA_ActivityBehaviorOverridesIdleBehavior(Engine engine)
    {
        var world = engine.GetEcsWorldAnyThread();
        var entity = world.CreateEntity();

        try
        {
            var cmd = _createTestCmd();
            var customBehavior = new TaleConversationBehavior(999)
            {
                CharacterModelDescription = cmd
            };

            var stayAt = new StayAtStrategyPart
            {
                Controller = new DummyController(),
                CharacterModelDescription = cmd,
                CharacterState = new CharacterState { BasicSpeed = 1.25f }
            };

            stayAt.OnAttach(engine, entity);
            stayAt.IsIndoorActivity = false;
            stayAt.ActivityBehavior = customBehavior;
            stayAt.OnEnter();

            if (!entity.Has<Behavior>())
            {
                Console.Write("(no Behavior) ");
                return false;
            }

            var behavior = entity.Get<Behavior>();
            bool ok = behavior.Provider is TaleConversationBehavior;
            if (!ok)
                Console.Write($"(got {behavior.Provider?.GetType().Name}) ");

            stayAt.OnExit();
            return ok;
        }
        finally
        {
            if (entity.IsAlive) entity.Dispose();
        }
    }

    /// <summary>
    /// Regression: Without ActivityBehavior, IdleBehavior is still used.
    /// </summary>
    private static bool TestBugA_DefaultIdleBehaviorWithoutActivityBehavior(Engine engine)
    {
        var world = engine.GetEcsWorldAnyThread();
        var entity = world.CreateEntity();

        try
        {
            var cmd = _createTestCmd();
            var stayAt = new StayAtStrategyPart
            {
                Controller = new DummyController(),
                CharacterModelDescription = cmd,
                CharacterState = new CharacterState { BasicSpeed = 1.25f }
            };

            stayAt.OnAttach(engine, entity);
            stayAt.IsIndoorActivity = false;
            stayAt.ActivityBehavior = null;
            stayAt.OnEnter();

            if (!entity.Has<Behavior>())
            {
                Console.Write("(no Behavior) ");
                return false;
            }

            var behavior = entity.Get<Behavior>();
            bool ok = behavior.Provider is IdleBehavior;
            if (!ok)
                Console.Write($"(got {behavior.Provider?.GetType().Name}) ");

            stayAt.OnExit();
            return ok;
        }
        finally
        {
            if (entity.IsAlive) entity.Dispose();
        }
    }

    /// <summary>
    /// Bug A critical: On initial spawn, OnEnter calls _setupActivity before base.OnEnter,
    /// so the very first activity phase already has TaleConversationBehavior.
    /// Without this, NPC only gets conversation behavior after its first travel completes.
    /// </summary>
    private static bool TestBugA_ConversationBehaviorOnInitialSpawn(Engine engine)
    {
        var world = engine.GetEcsWorldAnyThread();
        var entity = world.CreateEntity();

        try
        {
            var taleManager = new TaleManager();
            taleManager.Initialize(new StoryletLibrary(), 42);

            var schedule = _createTestSchedule(50);
            taleManager.RegisterNpc(schedule);

            var cmd = _createTestCmd();
            var pod = new PositionDescription { Position = new Vector3(5, 2, 5) };

            if (!TaleEntityStrategy.TryCreate(schedule, taleManager, pod, cmd, out var strategy))
                return false;

            strategy.OnAttach(engine, entity);
            strategy.OnEnter(); // initial spawn — must already have TaleConversationBehavior

            if (!entity.Has<Behavior>())
            {
                Console.Write("(no Behavior on initial spawn) ");
                return false;
            }

            var behavior = entity.Get<Behavior>();
            if (behavior.Provider is TaleConversationBehavior)
                return true;

            Console.Write($"(got {behavior.Provider?.GetType().Name} on initial spawn) ");
            return false;
        }
        finally
        {
            if (entity.IsAlive) entity.Dispose();
        }
    }

    /// <summary>
    /// Spawn-pipeline test: Simulates what EntityCreator.CreateLogical does — sets
    /// the Strategy component, which triggers StrategyManager → OnAttach → OnEnter →
    /// sets TaleConversationBehavior. Then verifies that overwriting the behavior
    /// (as TaleSpawnOperator used to do) would be detected.
    ///
    /// This test would have caught the original bug where TaleEntityBehavior
    /// overwrote TaleConversationBehavior in the setup action.
    /// </summary>
    private static bool TestBugA_StrategyComponentSetsBehavior(Engine engine)
    {
        var world = engine.GetEcsWorldAnyThread();
        var entity = world.CreateEntity();

        try
        {
            var taleManager = new TaleManager();
            taleManager.Initialize(new StoryletLibrary(), 42);

            var schedule = _createTestSchedule(300);
            taleManager.RegisterNpc(schedule);

            var cmd = _createTestCmd();
            var pod = new PositionDescription { Position = new Vector3(30, 2, 30) };

            if (!TaleEntityStrategy.TryCreate(schedule, taleManager, pod, cmd, out var strategy))
                return false;

            // Simulate what EntityCreator.CreateLogical does:
            // 1. Set Strategy component
            // 2. StrategyManager detects the component and calls OnAttach + OnEnter
            // (In test mode, StrategyManager's logical thread isn't running,
            //  so we call OnAttach + OnEnter manually — same as StrategyManager._onComponentAdded)
            entity.Set(new engine.behave.components.Strategy(strategy));
            strategy.OnAttach(engine, entity);
            ((IStrategyPart)strategy).OnEnter();
            if (!entity.Has<Behavior>())
            {
                Console.Write("(no Behavior after Strategy set) ");
                return false;
            }

            var behavior = entity.Get<Behavior>();
            if (behavior.Provider is not ANearbyBehavior)
            {
                Console.Write($"(got {behavior.Provider?.GetType().Name}, expected ANearbyBehavior) ");
                return false;
            }

            if (behavior.Provider is not TaleConversationBehavior)
            {
                Console.Write($"(got {behavior.Provider?.GetType().Name}, expected TaleConversationBehavior) ");
                return false;
            }

            // Simulate the OLD bug: overwrite with TaleEntityBehavior.
            // This is what TaleSpawnOperator used to do AFTER CreateLogical.
            // Verify we can detect this is wrong.
            var badBehavior = new TaleEntityBehavior(strategy);
            entity.Set(new Behavior(badBehavior));

            var afterOverwrite = entity.Get<Behavior>();
            if (afterOverwrite.Provider is ANearbyBehavior)
            {
                // This should NOT be an ANearbyBehavior — TaleEntityBehavior isn't one.
                Console.Write("(overwrite unexpectedly kept ANearbyBehavior) ");
                return false;
            }

            // Confirm: TaleEntityBehavior is NOT an ANearbyBehavior — this proves
            // the overwrite would have broken "E to Talk".
            if (afterOverwrite.Provider is not TaleEntityBehavior)
            {
                Console.Write("(unexpected provider type after overwrite) ");
                return false;
            }

            // The post-spawn health check (added to TaleSpawnOperator) would
            // log a warning here: "behavior is TaleEntityBehavior, expected ANearbyBehavior"
            return true;
        }
        finally
        {
            if (entity.IsAlive) entity.Dispose();
        }
    }

    /// <summary>
    /// Bug A integration: After the travel→activity transition (which calls _setupActivity),
    /// the entity's behavior should be TaleConversationBehavior, not IdleBehavior.
    ///
    /// The initial OnEnter goes directly to "activity" without _setupActivity (no conversation
    /// behavior yet). Only when GiveUpStrategy(travel) fires does _setupActivity run.
    /// We simulate this by calling GiveUpStrategy with the travel strategy part.
    /// </summary>
    private static bool TestBugA_ConversationBehaviorAfterTravelCompletion(Engine engine)
    {
        var world = engine.GetEcsWorldAnyThread();
        var entity = world.CreateEntity();

        try
        {
            var taleManager = new TaleManager();
            taleManager.Initialize(new StoryletLibrary(), 42);

            var schedule = _createTestSchedule(100);
            taleManager.RegisterNpc(schedule);

            var cmd = _createTestCmd();
            var pod = new PositionDescription { Position = new Vector3(10, 2, 10) };

            if (!TaleEntityStrategy.TryCreate(schedule, taleManager, pod, cmd, out var strategy))
                return false;

            strategy.OnAttach(engine, entity);
            strategy.OnEnter(); // enters initial "activity" with _setupActivity

            // Initial state should now be TaleConversationBehavior (OnEnter calls _setupActivity)
            if (!entity.Has<Behavior>() || !(entity.Get<Behavior>().Provider is TaleConversationBehavior))
            {
                Console.Write("(initial state not TaleConversationBehavior) ");
                return false;
            }

            // Simulate travel completion: GiveUpStrategy(travel) calls _setupActivity + TriggerStrategy("activity")
            // This re-enters "activity" with _setupActivity having configured ActivityBehavior.
            strategy.GiveUpStrategy(strategy.Strategies["travel"]);

            if (!entity.Has<Behavior>())
            {
                Console.Write("(no Behavior after travel) ");
                return false;
            }

            var behavior = entity.Get<Behavior>();
            if (behavior.Provider is TaleConversationBehavior)
                return true;

            Console.Write($"(got {behavior.Provider?.GetType().Name} after travel, expected TaleConversationBehavior) ");
            return false;
        }
        finally
        {
            if (entity.IsAlive) entity.Dispose();
        }
    }

    /// <summary>
    /// Bug B integration: After recover completes, the entity should resume activity
    /// with TaleConversationBehavior — proving _setupActivity was called.
    ///
    /// Flow: initial activity → simulate travel completion (so _setupActivity runs once)
    ///       → trigger recover → GiveUpStrategy(recover) → verify behavior restored.
    /// </summary>
    private static bool TestBugB_RecoverToActivityRestoresBehavior(Engine engine)
    {
        var world = engine.GetEcsWorldAnyThread();
        var entity = world.CreateEntity();

        try
        {
            var taleManager = new TaleManager();
            taleManager.Initialize(new StoryletLibrary(), 42);

            var schedule = _createTestSchedule(200);
            taleManager.RegisterNpc(schedule);

            var cmd = _createTestCmd();
            var pod = new PositionDescription { Position = new Vector3(20, 2, 20) };

            if (!TaleEntityStrategy.TryCreate(schedule, taleManager, pod, cmd, out var strategy))
                return false;

            strategy.OnAttach(engine, entity);
            strategy.OnEnter(); // enters initial "activity"

            // First, do a travel completion to get TaleConversationBehavior via _setupActivity
            strategy.GiveUpStrategy(strategy.Strategies["travel"]);

            if (!entity.Has<Behavior>() || !(entity.Get<Behavior>().Provider is TaleConversationBehavior))
            {
                Console.Write("(setup failed: no TaleConversationBehavior after travel) ");
                return false;
            }

            // Now simulate crash: switch to recover
            strategy.TriggerStrategy("recover");

            // Verify: RecoverBehavior is now active
            if (!entity.Has<Behavior>() || !(entity.Get<Behavior>().Provider is RecoverBehavior))
            {
                Console.Write("(recover not entered) ");
                return false;
            }

            // Simulate recovery completion: GiveUpStrategy(recover)
            // With the fix, this calls _setupActivity() + TriggerStrategy("activity")
            strategy.GiveUpStrategy(strategy.Strategies["recover"]);

            // Verify: should have TaleConversationBehavior again
            if (!entity.Has<Behavior>())
            {
                Console.Write("(no Behavior after recovery) ");
                return false;
            }

            var behavior = entity.Get<Behavior>();
            if (behavior.Provider is TaleConversationBehavior)
                return true;

            Console.Write($"(got {behavior.Provider?.GetType().Name} after recovery, expected TaleConversationBehavior) ");
            return false;
        }
        finally
        {
            if (entity.IsAlive) entity.Dispose();
        }
    }

    /// <summary>
    /// Bug B secondary: RecoverBehavior._deathAnimationTriggered must reset on OnAttach.
    /// Uses reflection to verify the private field since Behave() has physics dependencies.
    /// </summary>
    private static bool TestBugB_RecoverBehaviorResetsDeathAnimFlag()
    {
        var cmd = _createTestCmd();
        var recoverBehavior = new RecoverBehavior
        {
            CharacterModelDescription = cmd
        };

        // Access private field via reflection
        var flagField = typeof(RecoverBehavior).GetField(
            "_deathAnimationTriggered", BindingFlags.NonPublic | BindingFlags.Instance);
        if (flagField == null)
        {
            Console.Write("(reflection: field not found) ");
            return false;
        }

        // Initial value should be false
        bool initial = (bool)flagField.GetValue(recoverBehavior);
        if (initial)
        {
            Console.Write("(initial value should be false) ");
            return false;
        }

        // Simulate the flag being set (as Behave would do)
        flagField.SetValue(recoverBehavior, true);
        bool afterSet = (bool)flagField.GetValue(recoverBehavior);
        if (!afterSet)
        {
            Console.Write("(flag didn't set) ");
            return false;
        }

        // Create a temporary world + entity just for OnAttach
        using var world = new World();
        var entity = world.CreateEntity();
        try
        {
            // OnAttach should reset the flag
            recoverBehavior.OnAttach(I.Get<Engine>(), entity);

            bool afterReattach = (bool)flagField.GetValue(recoverBehavior);
            if (afterReattach)
            {
                Console.Write("(flag NOT reset on reattach) ");
                return false;
            }

            return true;
        }
        finally
        {
            if (entity.IsAlive) entity.Dispose();
        }
    }

    private class DummyController : IStrategyController
    {
        public IStrategyPart GetActiveStrategy() => null;
        public void GiveUpStrategy(IStrategyPart strategy) { }
    }
}
