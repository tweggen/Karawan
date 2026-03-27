using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using builtin.modules.satnav;
using builtin.modules.satnav.desc;
using engine;
using engine.navigation;
using engine.tale;
using engine.world;
using nogame.characters.citizen;
using static engine.Logger;

namespace Testbed;

/// <summary>
/// Regression tests for pathfinding failures.
/// Tests specific position pairs from game logs to ensure they can be routed.
/// </summary>
public class PathfindingDiagnostics
{
    /// <summary>
    /// Test case for a pathfinding route attempt.
    /// </summary>
    public class RouteTestCase
    {
        public string Name;
        public Vector3 FromPos;
        public Vector3 ToPos;
        public float ExpectedDistance;
        public bool ShouldSucceed;
    }

    /// <summary>
    /// Test pathfinding with specific test cases from game logs.
    /// </summary>
    public static async Task TestPathfindingRoutes(ClusterDesc clusterDesc)
    {
        if (clusterDesc == null)
        {
            Trace("PathfindingDiagnostics: ClusterDesc is null");
            return;
        }

        Trace($"\n{'='} PATHFINDING REGRESSION TESTS =====================");
        Trace($"Cluster: {clusterDesc.Name}");

        // Get NavMap from DI container
        NavMap navMap = null;
        try
        {
            navMap = I.Get<NavMap>();
        }
        catch
        {
            Trace("PathfindingDiagnostics: NavMap not available in DI container");
            return;
        }

        if (navMap == null)
        {
            Trace("PathfindingDiagnostics: NavMap is null");
            return;
        }

        // Define test cases from known game failures
        var testCases = new List<RouteTestCase>
        {
            // From NPC 11 game log: attempting to route to social_venue
            new RouteTestCase
            {
                Name = "NPC11_SocialVenue_Route",
                FromPos = new Vector3(-136.21152f, 39.856236f, 199.57533f),
                ToPos = new Vector3(-98.05985f, 39.856236f, 179.15295f),
                ExpectedDistance = 43.27f,
                ShouldSucceed = true
            }
        };

        int passCount = 0;
        int failCount = 0;

        foreach (var testCase in testCases)
        {
            Trace($"\nTest: {testCase.Name}");
            Trace($"  From: {testCase.FromPos}");
            Trace($"  To: {testCase.ToPos}");
            Trace($"  Expected distance: {testCase.ExpectedDistance:F2}m");

            try
            {
                // Create position descriptions
                var startPod = new PositionDescription { Position = testCase.FromPos, ClusterDesc = clusterDesc };

                // Attempt to build route
                var route = await StreetRouteBuilder.BuildAsync(
                    testCase.FromPos,
                    testCase.ToPos,
                    navMap,
                    startPod,
                    TransportationType.Pedestrian,
                    null,
                    default);

                if (route != null)
                {
                    Trace($"  ✓ PASS: Route found with {route.Segments.Count} segments");
                    passCount++;
                }
                else
                {
                    if (testCase.ShouldSucceed)
                    {
                        Trace($"  ✗ FAIL: Route should succeed but returned null");
                        failCount++;
                    }
                    else
                    {
                        Trace($"  ✓ PASS: Route correctly returned null (expected failure)");
                        passCount++;
                    }
                }
            }
            catch (Exception ex)
            {
                if (testCase.ShouldSucceed)
                {
                    Trace($"  ✗ FAIL: Exception during pathfinding: {ex.Message}");
                    failCount++;
                }
                else
                {
                    Trace($"  ✓ PASS: Exception expected and caught");
                    passCount++;
                }
            }
        }

        Trace($"\n{'='} PATHFINDING TEST RESULTS =======================");
        Trace($"Passed: {passCount}/{testCases.Count}");
        Trace($"Failed: {failCount}/{testCases.Count}");

        if (failCount == 0)
        {
            Trace("✅ All pathfinding tests passed");
        }
        else
        {
            Trace($"⚠️ {failCount} pathfinding test(s) failed");
        }
    }
}
