namespace engine;

/// <summary>
/// Debug output category identifiers for DebugFilter.
/// Rules:
///   - Never reorder/renumber existing entries (GlobalSettings keys are by name).
///   - Keep _Count last; update it when adding entries.
///   - GlobalSettings key: "debug.category.{name-in-lowercase}"
/// </summary>
public enum Dc : int
{
    Pathfinding     = 0,
    TaleManager     = 1,
    SpatialModel    = 2,
    MetaGen         = 3,
    SpawnController = 4,
    StreetGen       = 5,
    NavMap          = 6,
    Physics         = 7,
    AssetLoading    = 8,
    Casette         = 9,
    Animation       = 10,
    ClickModule     = 11,

    _Count          = 12   // Keep last — drives array size
}
