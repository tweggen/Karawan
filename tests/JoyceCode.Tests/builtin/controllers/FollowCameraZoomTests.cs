using System;
using System.Globalization;
using System.IO;
using builtin.controllers;
using Xunit;

namespace JoyceCode.Tests.builtin.controllers;


/**
 * How far behind the player the follow camera wants to be, and in particular that the
 * game's recommendation for the current way of moving (0.25 on foot, 1.0 in the hover,
 * sent by MainPlayModule through EventTypeRecommendDistance) reaches the distance at
 * all.
 *
 * It did not, for as long as the field the recommendation was stored in was never read:
 * the camera sat at the hover distance behind the walking character, which is KAR-322.
 * The controller needs a booted engine, physics and an input pipeline; the mapping from
 * zoom state and scale to a distance is arithmetic and is tested here, and a source scan
 * pins that _zoomDistance() actually consults the scale, because the pure function being
 * right is exactly the state the game shipped in.
 */
public class FollowCameraZoomTests
{
    /*
     * The controller's defaults, spelled out so a change to them fails here on purpose.
     */
    private const float MinDistance = 5f;
    private const float MaxDistance = 133f;
    private const float DefaultZoomState = 0.15f;
    private const float FootScale = 0.25f;
    private const float HoverScale = 1.0f;


    private static float _distance(float zoomState, float scale)
        => FollowCameraController.ZoomDistanceFor(zoomState, scale, MinDistance, MaxDistance);


    /**
     * A scale of 1.0 is the hover, and the hover must not move: this is the number the
     * game has always produced at the default zoom state.
     */
    [Fact]
    public void TheHoverDistanceIsWhatItAlwaysWas()
    {
        float expected = MinDistance + DefaultZoomState * DefaultZoomState * (MaxDistance - MinDistance);
        Assert.Equal(7.88f, expected, 2);
        Assert.Equal(expected, _distance(DefaultZoomState, HoverScale), 4);
    }


    /**
     * On foot the same wheel position puts the camera a quarter as far away.
     */
    [Fact]
    public void OnFootTheCameraIsAQuarterAsFar()
    {
        Assert.Equal(_distance(DefaultZoomState, HoverScale) * FootScale, _distance(DefaultZoomState, FootScale), 4);
        Assert.Equal(1.97f, _distance(DefaultZoomState, FootScale), 2);
    }


    /**
     * The scale multiplies the minimum too. With the wheel turned all the way in the
     * hover stops at 5 m and the walker at 1.25 m; a scale that only shrank the range
     * above the minimum would have both stop at 5 m, which is the mutation this rules
     * out.
     */
    [Fact]
    public void TheScaleAppliesToTheMinimumToo()
    {
        Assert.Equal(MinDistance, _distance(0f, HoverScale), 4);
        Assert.Equal(MinDistance * FootScale, _distance(0f, FootScale), 4);
    }


    /**
     * The wheel's two ends map onto the two limits, and the mapping is quadratic in
     * between: half the wheel is a quarter of the range, not half of it.
     */
    [Fact]
    public void TheWheelMapsQuadraticallyOntoTheLimits()
    {
        Assert.Equal(MinDistance, _distance(0f, HoverScale), 4);
        Assert.Equal(MaxDistance, _distance(1f, HoverScale), 4);
        Assert.Equal(MinDistance + 0.25f * (MaxDistance - MinDistance), _distance(0.5f, HoverScale), 4);
    }


    /**
     * The recommendation arrives as text. Parsed in the current culture, "0.25" is
     * twenty-five on a German Windows, so the parse is pinned to the invariant culture
     * under exactly that culture.
     */
    [Theory]
    [InlineData("de-DE")]
    [InlineData("en-US")]
    [InlineData("")]
    public void TheRecommendationIsReadTheSameInEveryCulture(string cultureName)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
            Assert.Equal(0.25f, FollowCameraController.ParseRecommendedDistance("0.25"), 6);
            Assert.Equal(1.0f, FollowCameraController.ParseRecommendedDistance("1.0"), 6);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }


    /**
     * The controller's own distance function must read the recommended scale. Dropping
     * the multiplication compiles, passes every test above, and is precisely the defect
     * that shipped.
     */
    [Fact]
    public void TheControllerConsultsTheRecommendedScale()
    {
        string? root = _findRepoRoot();
        Assert.True(null != root, "could not locate the repository root");
        string source = File.ReadAllText(Path.Combine(root!, "JoyceCode", "builtin", "controllers", "FollowCameraController.cs"));

        int start = source.IndexOf("private float _zoomDistance()", StringComparison.Ordinal);
        Assert.True(start >= 0, "_zoomDistance() not found");
        string body = _braceBody(source, start);

        Assert.Contains("_scaleFactor", body);
        Assert.Contains("ZoomDistanceFor(", body);
    }


    /**
     * The event handler must go through the culture-independent parse, not back to
     * Convert.ToDouble.
     */
    [Fact]
    public void TheEventHandlerParsesInvariantly()
    {
        string? root = _findRepoRoot();
        Assert.True(null != root, "could not locate the repository root");
        string source = File.ReadAllText(Path.Combine(root!, "JoyceCode", "builtin", "controllers", "FollowCameraController.cs"));

        int start = source.IndexOf("private void _onRecommendDistance(", StringComparison.Ordinal);
        Assert.True(start >= 0, "_onRecommendDistance not found");
        string body = _braceBody(source, start);

        Assert.Contains("ParseRecommendedDistance(", body);
        Assert.DoesNotContain("Convert.ToDouble", body);
    }


    /**
     * The text from the first '{' after start to its matching '}', by brace counting.
     */
    private static string _braceBody(string source, int start)
    {
        int open = source.IndexOf('{', start);
        int depth = 0;
        for (int i = open; i < source.Length; ++i)
        {
            if (source[i] == '{') ++depth;
            else if (source[i] == '}' && --depth == 0)
            {
                return source.Substring(open, i - open + 1);
            }
        }

        throw new InvalidOperationException("unbalanced braces");
    }


    private static string? _findRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "models", "nogame.resources.json")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return null;
    }
}
