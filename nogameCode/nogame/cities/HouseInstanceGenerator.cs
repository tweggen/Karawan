
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using builtin.tools.Lindenmayer;
using engine;
using engine.casette;
using engine.geom;
using engine.joyce;
using static engine.Logger;
using static builtin.extensions.JsonObjectNumerics;

namespace nogame.cities;

/**
 * Create an individual house.
 * Unfortunately, instead of re-using houses, we need to re-create houses one
 * by one to fit the surrounding. However, this is less of an effort when compared
 * to the trees.
 */
public class HouseInstanceGenerator
{
    static private object _lo = new();

    /**
     * Cached configuration loaded from JSON.
     */
    private static LSystemConfig? _cachedConfig = null;
    private static bool _configLoaded = false;

    /**
     * Default configuration values.
     */
    private const float DefaultStoryHeight = 3.0f;
    private const int DefaultMinSegmentStories = 4;
    private const float DefaultShrinkAmount = 2.0f;
    private const float DefaultSegmentProbability = 0.8f;
    private const float DefaultUpperSegmentProbability = 0.9f;
    private const float DefaultSingleBlockProbability = 0.1f;
    private const string DefaultWallsMaterial = "nogame.cities.houses.materials.houses.win3";
    private const string DefaultPowerlinesMaterial = "nogame.characters.house.materials.powerlines";

    public HouseInstanceGenerator()
    {
        I.Get<ObjectRegistry<Material>>().RegisterFactory("nogame.characters.house.materials.powerlines",
            name => new Material()
            {
                EmissiveTexture = I.Get<TextureCatalogue>().FindColorTexture(0xff33ffff)
            });
    }


    /**
     * Load house configuration from Mix config.
     */
    private void _ensureConfigLoaded()
    {
        if (_configLoaded)
        {
            return;
        }

        _configLoaded = true;

        try
        {
            var mix = I.Get<Mix>();
            var lsystemsNode = mix.GetTree("lsystems");

            if (lsystemsNode == null)
            {
                Trace("No lsystems config found in Mix, using default house configuration.");
                return;
            }

            var catalog = JsonSerializer.Deserialize<LSystemCatalog>(
                lsystemsNode.ToJsonString(),
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                });

            if (catalog?.LSystems == null)
            {
                return;
            }

            // Find the house1 definition
            var houseDef = catalog.LSystems.FirstOrDefault(d => d.Name == "house1");
            if (houseDef?.Config != null)
            {
                _cachedConfig = houseDef.Config;
                Trace($"Loaded house configuration from JSON: storyHeight={_cachedConfig.StoryHeight}, " +
                      $"shrinkAmount={_cachedConfig.ShrinkAmount}");
            }
        }
        catch (Exception e)
        {
            Warning($"Failed to load house configuration from config: {e.Message}");
        }
    }


    // Configuration accessors with fallback to defaults
    private float StoryHeight => _cachedConfig?.StoryHeight ?? DefaultStoryHeight;
    private int MinSegmentStories => _cachedConfig?.MinSegmentStories ?? DefaultMinSegmentStories;
    private float ShrinkAmount => _cachedConfig?.ShrinkAmount ?? DefaultShrinkAmount;
    private float SegmentProbability => _cachedConfig?.SegmentProbability ?? DefaultSegmentProbability;
    private float UpperSegmentProbability => _cachedConfig?.UpperSegmentProbability ?? DefaultUpperSegmentProbability;
    private float SingleBlockProbability => _cachedConfig?.SingleBlockProbability ?? DefaultSingleBlockProbability;

    private string WallsMaterial =>
        _cachedConfig?.Materials?.GetValueOrDefault("walls", DefaultWallsMaterial) ?? DefaultWallsMaterial;

    private string PowerlinesMaterial =>
        _cachedConfig?.Materials?.GetValueOrDefault("powerlines", DefaultPowerlinesMaterial) ?? DefaultPowerlinesMaterial;


    private Part _createSeed1(Params ini, builtin.tools.RandomSource rnd)
    {
        var jo = new JsonObject
        {
            ["A"] = ini["A"].DeepClone(), ["h"] = (float)ini["h"]
        };
        return new("buildable(A,h)", jo);
    }


    public builtin.tools.Lindenmayer.System CreateHouse1System(
        Params ini,
        builtin.tools.RandomSource rnd
        )
    {
        // Ensure config is loaded
        _ensureConfigLoaded();

        float minSegmentHeight = MinSegmentStories * StoryHeight;

        return new builtin.tools.Lindenmayer.System(new State(new List<Part>
            /*
             * Initial seed
             *
             * Expected context parameters:
             *    "basearea" : Poly
             *        The area we may build our building on
             *    "maxheight" : float
             *        The maximal height of the building.
             */
            {
                /*
                 * Straight up.
                 */
                new Part( "rotate(d,x,y,z)", new JsonObject {
                    ["d"] = 90f,["x"] = 0f, ["y"] = 0f, ["z"] =1f } ),
                _createSeed1(ini, rnd)
            }),

            /*
             * Transformation
             */
            new List<Rule>
            {
                /*
                 * A buildable with available space more than minSegmentHeight may become
                 * segmented into a lower buildableBaseSegment and an upper buildableSegment.
                 */
                new Rule("buildable(A,h)",
                    SegmentProbability, (Params p) => (float)p["h"] > minSegmentHeight,
                    (p) =>
                    {
                        int availableStories = (int)Single.Ceiling((float)p["h"]) / (int)StoryHeight;
                        /*
                         * The base is at least one storey.
                         */
                        int baseStories = 1 + (int)(((float)availableStories - 1f) * rnd.GetFloat());

                        /*
                         * Well, all that remains is the remainder.
                         */
                        int remainingStories = availableStories - baseStories;

                        var v3Edges = ToVector3List(p["A"]);

                        var v3SmallerEdges = new PolyTool(v3Edges, Vector3.UnitY).Extend(-ShrinkAmount);

                        if (null == v3SmallerEdges)
                        {
                            /*
                             * We can't shrink it any more, keep the entire stem.
                             */
                            return new List<Part>
                            {
                                new("buildableBaseSegment(A,h)", new JsonObject
                                {
                                    ["A"] = From(v3Edges), ["h"] = (float)(availableStories * StoryHeight)
                                })
                            };
                        }
                        else
                        {
                            return new List<Part>
                            {
                                new("buildableBaseSegment(A,h)", new JsonObject
                                {
                                    ["A"] = From(v3Edges), ["h"] = (float)(baseStories * StoryHeight)
                                }),
                                new("buildableAnySegment(A,h)", new JsonObject
                                {
                                    ["A"] = From(v3SmallerEdges), ["h"] = (float)(remainingStories * StoryHeight)
                                }),
                            };
                        }
                    }),

                /*
                 * A buildable with available space more than minSegmentHeight may become
                 * segmented into a lower buildableBaseSegment and an upper buildableSegment.
                 */
                new Rule("buildableAnySegment(A,h)",
                    UpperSegmentProbability, (Params p) => (float)p["h"] > minSegmentHeight,
                    (p) =>
                    {
                        int availableStories = (int)Single.Ceiling((float)p["h"]) / (int)StoryHeight;

                        /*
                         * The base is at least one storey.
                         */
                        int lowerStories = 1 + (int)(((float)availableStories - 1f) * rnd.GetFloat());

                        /*
                         * Well, all that remains is the remainder.
                         */
                        int upperStories = availableStories - lowerStories;

                        var v3Edges = ToVector3List(p["A"]);

                        var v3SmallerEdges = new PolyTool(v3Edges, Vector3.UnitY).Extend(-ShrinkAmount);
                        if (null == v3SmallerEdges)
                        {
                            /*
                             * We can't shrink it any more, keep the entire stem.
                             */
                            return new List<Part>
                            {
                                new("segment(A,h)", new JsonObject
                                {
                                    ["A"] = From(v3Edges), ["h"] = (float)(availableStories * StoryHeight)
                                })
                            };
                        }
                        else
                        {
                            return new List<Part>
                            {
                                new("buildableAnySegment(A,h)", new JsonObject
                                {
                                    ["A"] = From(v3Edges), ["h"] = (float)(lowerStories * StoryHeight)
                                }),
                                new("buildableAnySegment(A,h)", new JsonObject
                                {
                                    ["A"] = From(v3SmallerEdges), ["h"] = (float)(upperStories * StoryHeight)
                                }),
                            };
                        }
                    }),

                /*
                 * A buildable may straightforward become a single buildable base part.
                 * That's what we had in the original game all the time.
                 */
                new Rule("buildable(A,h)",
                    SingleBlockProbability, (Params p) => (float)p["h"] > minSegmentHeight,
                    (p) => new List<Part>
                    {
                        new ("buildableBaseSegment(A,h)", new JsonObject
                        {
                            ["A"] = p["A"].DeepClone(), ["h"] = (float)p["h"]
                        }),
                    }),

                new Rule("buildable(A,h)",
                    1f, (Params p) => (float)p["h"] < minSegmentHeight,
                    (p) => new List<Part>
                    {
                        new ("buildableBaseSegment(A,h)", new JsonObject
                        {
                            ["A"] = p["A"].DeepClone(), ["h"] = (float)p["h"]
                        }),
                    }),

                /*
                 * A buildable base segment has neon signs etc. .
                 */
                new Rule("buildableBaseSegment(A,h)",
                    (p) => new List<Part>
                    {
                        new ("powerline(P,h)", new JsonObject
                        {
                            ["P"] = From(AnyOf(rnd, ToVector3List(p["A"].DeepClone()))),
                            ["h"] = (float)p["h"],
                            ["mat"] = PowerlinesMaterial
                        }),
                        new ("segment(A,h)", new JsonObject
                        {
                            ["A"] = p["A"].DeepClone(), ["h"] = (float)p["h"]
                        }),
                        new ("neon(P,h,n)", new JsonObject
                        {
                            ["P"] = From(ToVector3List(p["A"].DeepClone()).First()),
                            ["h"] = ((float)p["h"])*(rnd.GetFloat()*0.7f+0.1f),
                            ["n"] = (rnd.Get8()&3)+2
                        })
                    }),

                /*
                 * Any other segment does not have neon signs.
                 */
                new Rule("buildableAnySegment(A,h)",
                    SingleBlockProbability, Rule.Always,
                    (p) => new List<Part>
                    {
                        new ("powerline(P,h)", new JsonObject
                        {
                            ["P"] = From(AnyOf(rnd, ToVector3List(p["A"].DeepClone()))),
                            ["h"] = (float)p["h"],
                            ["mat"] = PowerlinesMaterial
                        }),
                        new ("segment(A,h)", new JsonObject
                        {
                            ["A"] = p["A"].DeepClone(), ["h"] = (float)p["h"]
                        }),
                    })
            },

            /*
             * Macros: Break down the specific operation stem to a standard turtle operation.
             */
            new List<Rule>
            {
                new Rule("segment(A,h)",
                    (p) => new List<Part>
                    {
                        new ("extrudePoly(A,h,mat)", new JsonObject
                        {
                            ["A"] = p["A"].DeepClone(), ["h"] = (float)p["h"], ["mat"] = WallsMaterial
                        })
                    }),
                new Rule("neon(P,h,n)",
                    (p) => new List<Part>
                    {
                        /* TXWTODO: Write me */
                    })
            });
    }
}
