# Phase 7 Test Scripts — Spatial Grounding & Navigation

**Status**: 102 test scripts created

**Test Location**: `models/tests/tale/phase7-spatial/`

**Key Features Tested**:
- SpatialModel extraction from cluster geometry
- Location type assignment (shop, office, home, street)
- Entry position computation (shop fronts, building centers)
- Travel time calculations over street networks
- Building occupancy (indoor NPCs invisible)
- Fragment-accurate position tracking
- Full Phase 7 integration and edge cases

---

## Test Organization (102 scripts)

### Category 1: SpatialModel Extraction (Tests 01-15)

Tests that verify correct extraction of locations from cluster geometry.

**Test 01: SpatialModel Extracts Shop Locations**
- **File**: `01-extract-shop-locations.json`
- **Priority**: Critical
- **Purpose**: ShopFront objects become Location entries in SpatialModel
- **Validation**: Location count matches shop count, all have type "shop"

**Test 02: SpatialModel Extracts Building Locations**
- **File**: `02-extract-building-locations.json`
- **Priority**: Critical
- **Purpose**: Building objects become Location entries
- **Validation**: All buildings present, type matches building role

**Test 03: SpatialModel Extracts Street Segments**
- **File**: `03-extract-street-segments.json`
- **Priority**: Critical
- **Purpose**: Street waypoints become street_segment locations
- **Validation**: Street segment count matches street point count

**Test 04: Entry Position for Shops**
- **File**: `04-entry-position-shops.json`
- **Priority**: Critical
- **Purpose**: Shop entry position is midpoint of ShopFront points
- **Validation**: EntryPosition ≠ Position, Y = street height, XZ near shop center

**Test 05: Entry Position for Buildings**
- **File**: `05-entry-position-buildings.json`
- **Priority**: Critical
- **Purpose**: Building entry position is center projected to street height
- **Validation**: EntryPosition at building XZ, Y at street level

**Test 06: Entry Position for Streets**
- **File**: `06-entry-position-streets.json`
- **Priority**: High
- **Purpose**: Street entries are the segment positions themselves
- **Validation**: EntryPosition == Position for all street_segment locations

**Test 07: Location Position Validity**
- **File**: `07-location-position-validity.json`
- **Priority**: High
- **Purpose**: All locations have valid positions within cluster AABB
- **Validation**: Position coordinates within cluster bounds, Y at ground

**Test 08: Location Capacity Assignment**
- **File**: `08-location-capacity-assignment.json`
- **Priority**: High
- **Purpose**: Each location gets capacity based on type/size
- **Validation**: Shops capacity > streets, offices capacity > homes

**Test 09: No Duplicate Location IDs**
- **File**: `09-no-duplicate-location-ids.json`
- **Priority**: High
- **Purpose**: SpatialModel assigns unique IDs to each location
- **Validation**: All location.Id values unique

**Test 10: Location Query API**
- **File**: `10-location-query-api.json`
- **Priority**: High
- **Purpose**: GetLocation(id) returns correct location
- **Validation**: GetLocation returns non-null with matching ID

**Test 11: Location Lookup by Type**
- **File**: `11-location-lookup-by-type.json`
- **Priority**: Medium
- **Purpose**: GetLocations(type) returns filtered set
- **Validation**: All returned locations match type filter

**Test 12: Empty Cluster Extraction**
- **File**: `12-empty-cluster-extraction.json`
- **Priority**: Medium
- **Purpose**: Cluster with no buildings/shops extracts cleanly
- **Validation**: SpatialModel created, street segments extracted

**Test 13: Large Cluster Extraction**
- **File**: `13-large-cluster-extraction.json`
- **Priority**: Medium
- **Purpose**: 100+ locations extractable without performance issues
- **Validation**: Extraction completes, all locations accessible

**Test 14: Multiple ShopFront Tags**
- **File**: `14-multiple-shopfront-tags.json`
- **Priority**: High
- **Purpose**: ShopFront.Tags ("shop Eat", "shop Drink") reflected in SpatialModel
- **Validation**: Location.ShopType correctly set from tags

**Test 15: Height Variance in Cluster**
- **File**: `15-height-variance-cluster.json`
- **Priority**: Medium
- **Purpose**: Street height computed from cluster AverageHeight + offsets
- **Validation**: All entry positions at consistent street height, no clipping

---

### Category 2: Location Type Assignment (Tests 16-30)

Tests role-based assignment of NPCs to location types.

**Test 16: Merchant Home Location Type**
- **File**: `16-merchant-home-location-type.json`
- **Priority**: High
- **Purpose**: Merchants assigned to residential areas
- **Validation**: HomeLocationId points to "office" or "home" type

**Test 17: Worker Home Location Type**
- **File**: `17-worker-home-location-type.json`
- **Priority**: High
- **Purpose**: Workers assigned to residential areas
- **Validation**: HomeLocationId != workplace, both valid

**Test 18: Socialite Venue Assignment**
- **File**: `18-socialite-venue-assignment.json`
- **Priority**: High
- **Purpose**: Socialites have SocialVenueIds pointing to meeting spots
- **Validation**: SocialVenueIds all valid location IDs

**Test 19: Authority Location Assignment**
- **File**: `19-authority-location-assignment.json`
- **Priority**: High
- **Purpose**: Authority roles assigned to street/patrol points
- **Validation**: WorkplaceLocationId type "street_segment"

**Test 20: Drifter No Fixed Workplace**
- **File**: `20-drifter-no-fixed-workplace.json`
- **Priority**: Medium
- **Purpose**: Drifters have HomeLocationId but no WorkplaceLocationId
- **Validation**: WorkplaceLocationId == -1 or null

**Test 21: Role Distribution by Cluster**
- **File**: `21-role-distribution-by-cluster.json`
- **Priority**: Medium
- **Purpose**: Downtown clusters have more merchants, residential have more workers
- **Validation**: Merchant % higher in downtown, worker % higher in residential

**Test 22: Workplace Distinct from Home**
- **File**: `22-workplace-distinct-from-home.json`
- **Priority**: High
- **Purpose**: Most NPCs have different home and workplace
- **Validation**: HomeLocationId != WorkplaceLocationId for workers/merchants

**Test 23: All Generated NPCs Have Home**
- **File**: `23-all-generated-npcs-have-home.json`
- **Priority**: Critical
- **Purpose**: Every NPC gets a valid HomeLocationId
- **Validation**: HomeLocationId != -1 for all NPCs

**Test 24: Location Capacity Respected**
- **File**: `24-location-capacity-respected.json`
- **Priority**: High
- **Purpose**: Assignment doesn't violate location capacity
- **Validation**: Count of NPCs at location ≤ Location.Capacity

**Test 25: Multiple NPCs at Same Location**
- **File**: `25-multiple-npcs-at-same-location.json`
- **Priority**: Medium
- **Purpose**: Multiple NPCs can share a workplace
- **Validation**: Several NPCs have same WorkplaceLocationId

**Test 26: Socialite Multiple Venues**
- **File**: `26-socialite-multiple-venues.json`
- **Priority**: High
- **Purpose**: Socialites assigned 3+ social venues
- **Validation**: SocialVenueIds.Count >= 3 for socialites

**Test 27: Location Assignment Seed-Stable**
- **File**: `27-location-assignment-seed-stable.json`
- **Priority**: High
- **Purpose**: Same cluster seed → same location assignments
- **Validation**: Two generations produce identical location IDs per NPC

**Test 28: Skip Mask Preserves Assignments**
- **File**: `28-skip-mask-preserves-assignments.json`
- **Priority**: High
- **Purpose**: Skipping NPC index N doesn't change NPC N+1's locations
- **Validation**: Location assignments identical with skip mask

**Test 29: Invalid Location Fallback**
- **File**: `29-invalid-location-fallback.json`
- **Priority**: Medium
- **Purpose**: If preferred location unavailable, fallback works
- **Validation**: NPC still gets assigned a valid location

**Test 30: Mixed Role Population**
- **File**: `30-mixed-role-population.json`
- **Priority**: Medium
- **Purpose**: Cluster population includes variety of roles
- **Validation**: All 5 roles present (worker, merchant, socialite, drifter, authority)

---

### Category 3: Entry Position Computation (Tests 31-45)

Tests that entry positions are computed correctly and prevent clipping.

**Test 31: Shop Entry Within ShopFront Bounds**
- **File**: `31-shop-entry-within-bounds.json`
- **Priority**: Critical
- **Purpose**: Shop entry position between first two ShopFront points
- **Validation**: Entry position XZ between p0 and p1, or near midpoint

**Test 32: Entry Position at Street Height**
- **File**: `32-entry-position-at-street-height.json`
- **Priority**: Critical
- **Purpose**: All entry positions have Y = street height
- **Validation**: EntryPosition.Y consistent across all locations

**Test 33: Building Entry Not at Centroid**
- **File**: `33-building-entry-not-at-centroid.json`
- **Priority**: High
- **Purpose**: Building entry position ≠ center, near surface
- **Validation**: EntryPosition within building outline at street level

**Test 34: Street Entry at Segment Point**
- **File**: `34-street-entry-at-segment-point.json`
- **Priority**: High
- **Purpose**: Street locations use segment position as entry
- **Validation**: EntryPosition == Position for street_segment type

**Test 35: No Entry Position Clipping**
- **File**: `35-no-entry-position-clipping.json`
- **Priority**: Critical
- **Purpose**: Entry positions outside building/shop geometry
- **Validation**: Raycasts from entry position into air, not walls

**Test 36: Entry Position Accessibility**
- **File**: `36-entry-position-accessibility.json`
- **Priority**: High
- **Purpose**: Entry positions near street network
- **Validation**: Distance to nearest street segment < cluster size

**Test 37: Consistent Entry Height Across Building**
- **File**: `37-consistent-entry-height-building.json`
- **Priority**: Medium
- **Purpose**: Multiple buildings in cluster have same entry Y
- **Validation**: All EntryPosition.Y values identical

**Test 38: Shop Entry Varies by Front**
- **File**: `38-shop-entry-varies-by-front.json`
- **Priority**: Medium
- **Purpose**: Different shops have different entry positions
- **Validation**: No two shop entries at same XZ

**Test 39: Large Building Entry Centered**
- **File**: `39-large-building-entry-centered.json`
- **Priority**: Medium
- **Purpose**: Large building entry at geometric center
- **Validation**: EntryPosition.XZ near building AABB center

**Test 40: Entry Position After Cluster Shift**
- **File**: `40-entry-position-after-cluster-shift.json`
- **Priority**: Medium
- **Purpose**: Shifting cluster in world doesn't affect internal entry positions
- **Validation**: Relative entry positions stable

**Test 41: Negative Coordinate Handling**
- **File**: `41-negative-coordinate-handling.json`
- **Priority**: Medium
- **Purpose**: Clusters with negative coordinates extract correctly
- **Validation**: Entry positions computed properly regardless of world offset

**Test 42: Zero-Height Cluster**
- **File**: `42-zero-height-cluster.json`
- **Priority**: Medium
- **Purpose**: Cluster at Y=0 computes entry height correctly
- **Validation**: Entry positions at ground level, non-negative

**Test 43: Very Tall Cluster**
- **File**: `43-very-tall-cluster.json`
- **Priority**: Medium
- **Purpose**: Cluster with large height variance handles entry positions
- **Validation**: Street height computed from top elevation, not average

**Test 44: Multiple Entry Candidates**
- **File**: `44-multiple-entry-candidates.json`
- **Priority**: Medium
- **Purpose**: If building has multiple doors, only one entry point stored
- **Validation**: Location has single EntryPosition, not array

**Test 45: Entry Position Roundtrip**
- **File**: `45-entry-position-roundtrip.json`
- **Priority**: High
- **Purpose**: Entry positions survive SpatialModel serialization/deserialization
- **Validation**: EntryPosition values identical before/after roundtrip

---

### Category 4: Travel Time Calculations (Tests 46-60)

Tests that NPC travel times and pathfinding distances are computed correctly.

**Test 46: Straight-Line Travel Distance**
- **File**: `46-straight-line-travel-distance.json`
- **Priority**: High
- **Purpose**: Travel from location A to B uses entry positions
- **Validation**: Distance(entryA, entryB) < Distance(centerA, centerB) or similar

**Test 47: Same Location Zero Travel**
- **File**: `47-same-location-zero-travel.json`
- **Priority**: High
- **Purpose**: Staying at same location has zero travel time
- **Validation**: TravelTime == 0 when source == destination

**Test 48: Adjacent Locations Short Travel**
- **File**: `48-adjacent-locations-short-travel.json`
- **Priority**: High
- **Purpose**: Nearby locations have short travel times
- **Validation**: TravelTime < TravelTime to far location

**Test 49: Distant Locations Long Travel**
- **File**: `49-distant-locations-long-travel.json`
- **Priority**: High
- **Purpose**: Distant locations have longer travel times
- **Validation**: TravelTime scales with distance

**Test 50: Travel Time Consistency**
- **File**: `50-travel-time-consistency.json`
- **Priority**: High
- **Purpose**: Travel time A→B == Travel time B→A
- **Validation**: Symmetric distances

**Test 51: Street Segment Travel**
- **File**: `51-street-segment-travel.json`
- **Priority**: High
- **Purpose**: Traveling between street segments is fast
- **Validation**: Street-to-street time < street-to-building time

**Test 52: Shop to Shop Travel**
- **File**: `52-shop-to-shop-travel.json`
- **Priority**: Medium
- **Purpose**: Shop-to-shop distances are direct
- **Validation**: Uses entry positions, not shop centers

**Test 53: Cluster-Boundary Travel**
- **File**: `53-cluster-boundary-travel.json`
- **Priority**: Medium
- **Purpose**: Travel to cluster edge precomputes reasonable distance
- **Validation**: Distance reasonable, no infinite values

**Test 54: Multi-Waypoint Route**
- **File**: `54-multi-waypoint-route.json`
- **Priority**: Medium
- **Purpose**: SegmentRoute with multiple waypoints accumulates distance
- **Validation**: Total route distance > straight line

**Test 55: Route Caching**
- **File**: `55-route-caching.json`
- **Priority**: Medium
- **Purpose**: PrecomputedRoute reduces per-frame calculations
- **Validation**: Second travel to same destination reuses route

**Test 56: Fallback to Straight Line**
- **File**: `56-fallback-to-straight-line.json`
- **Priority**: High
- **Purpose**: If pathfinding fails, straight line is used
- **Validation**: NPC still moves, distance reasonable

**Test 57: Travel Time Variation by NPC**
- **File**: `57-travel-time-variation-by-npc.json`
- **Priority**: Medium
- **Purpose**: Different NPCs may have different speeds
- **Validation**: Can configure speed modifier per role or property

**Test 58: Precomputed Route Validity**
- **File**: `58-precomputed-route-validity.json`
- **Priority**: High
- **Purpose**: PrecomputedRoute stored on GoToStrategyPart is valid
- **Validation**: Route connects start to end, doesn't loop

**Test 59: Empty Route Handling**
- **File**: `59-empty-route-handling.json`
- **Priority**: Medium
- **Purpose**: Zero-waypoint route handled gracefully
- **Validation**: NPC stays at current location or jumps directly

**Test 60: Route Persistence**
- **File**: `60-route-persistence.json`
- **Priority**: Medium
- **Purpose**: PrecomputedRoute survives activity transitions
- **Validation**: Route data not lost between phases

---

### Category 5: Building Occupancy & Indoor Activities (Tests 61-75)

Tests that NPCs at indoor locations become invisible.

**Test 61: Shop Activity is Indoor**
- **File**: `61-shop-activity-is-indoor.json`
- **Priority**: Critical
- **Purpose**: NPCs at shop locations set IsIndoorActivity=true
- **Validation**: StayAtStrategyPart.IsIndoorActivity == true

**Test 62: Office Activity is Indoor**
- **File**: `62-office-activity-is-indoor.json`
- **Priority**: Critical
- **Purpose**: NPCs at office locations are indoor
- **Validation**: IsIndoorActivity == true

**Test 63: Street Activity is Outdoor**
- **File**: `63-street-activity-is-outdoor.json`
- **Priority**: Critical
- **Purpose**: NPCs at street_segment locations are outdoor
- **Validation**: IsIndoorActivity == false

**Test 64: Home Activity is Indoor**
- **File**: `64-home-activity-is-indoor.json`
- **Priority**: Critical
- **Purpose**: NPCs sleeping at home are indoor
- **Validation**: IsIndoorActivity == true

**Test 65: Indoor NPC No Idle Behavior**
- **File**: `65-indoor-npc-no-idle-behavior.json`
- **Priority**: Critical
- **Purpose**: Indoor NPCs skip Behavior component setup
- **Validation**: Entity lacks behave.Behavior component

**Test 66: Outdoor NPC Has Idle Behavior**
- **File**: `66-outdoor-npc-has-idle-behavior.json`
- **Priority**: High
- **Purpose**: Outdoor NPCs setup idle animation
- **Validation**: Entity has behave.Behavior component

**Test 67: Indoor NPC Position Still Valid**
- **File**: `67-indoor-npc-position-still-valid.json`
- **Priority**: High
- **Purpose**: Indoor NPCs have correct position even without visuals
- **Validation**: NpcSchedule.CurrentWorldPosition correct

**Test 68: Occupancy Density at Location**
- **File**: `68-occupancy-density-at-location.json`
- **Priority**: Medium
- **Purpose**: Multiple indoor NPCs at same location tracked correctly
- **Validation**: Fragment doesn't exceed capacity

**Test 69: Transition Indoor to Outdoor**
- **File**: `69-transition-indoor-to-outdoor.json`
- **Priority**: High
- **Purpose**: NPC moving from shop to street becomes visible
- **Validation**: IsIndoorActivity flips from true to false

**Test 70: Transition Outdoor to Indoor**
- **File**: `70-transition-outdoor-to-indoor.json`
- **Priority**: High
- **Purpose**: NPC moving from street to shop becomes invisible
- **Validation**: IsIndoorActivity flips from false to true

**Test 71: Multiple Indoor Transitions**
- **File**: `71-multiple-indoor-transitions.json`
- **Priority**: Medium
- **Purpose**: NPC rapidly switching indoor/outdoor handled correctly
- **Validation**: Visibility state correct after each transition

**Test 72: Indoor Activity Duration**
- **File**: `72-indoor-activity-duration.json`
- **Priority**: Medium
- **Purpose**: Indoor activities have same duration as outdoor equivalents
- **Validation**: Time spent in shop == time spent on street (same storylet)

**Test 73: Indoor Occupancy Persistence**
- **File**: `73-indoor-occupancy-persistence.json`
- **Priority**: High
- **Purpose**: Indoor occupancy survives save/load
- **Validation**: After load, same NPCs invisible at same locations

**Test 74: Empty Building No Crash**
- **File**: `74-empty-building-no-crash.json`
- **Priority**: Medium
- **Purpose**: Building with capacity but no assigned NPCs doesn't error
- **Validation**: StayAtStrategyPart.OnEnter() handles gracefully

**Test 75: Capacity Overflow Handling**
- **File**: `75-capacity-overflow-handling.json`
- **Priority**: Medium
- **Purpose**: If more NPCs assigned than building capacity, handled gracefully
- **Validation**: No crash, NPCs don't disappear

---

### Category 6: Fragment-Accurate Position Tracking (Tests 76-90)

Tests that CurrentWorldPosition keeps spawning accurate.

**Test 76: Initial Position is Home**
- **File**: `76-initial-position-is-home.json`
- **Priority**: Critical
- **Purpose**: Newly generated NPCs start at HomePosition
- **Validation**: CurrentWorldPosition == HomePosition on first tick

**Test 77: Position Updates on Travel**
- **File**: `77-position-updates-on-travel.json`
- **Priority**: Critical
- **Purpose**: CurrentWorldPosition updates during travel phase
- **Validation**: Position interpolates towards destination

**Test 78: Position Updates on Arrival**
- **File**: `78-position-updates-on-arrival.json`
- **Priority**: Critical
- **Purpose**: CurrentWorldPosition == destination when arrival at location
- **Validation**: Position == destination.EntryPosition after GoTo completes

**Test 79: Position Updated Every Tick**
- **File**: `79-position-updated-every-tick.json`
- **Priority**: High
- **Purpose**: AdvanceNpc() updates CurrentWorldPosition every call
- **Validation**: Position differs between consecutive ticks during travel

**Test 80: GetNpcsInFragment Uses CurrentWorldPosition**
- **File**: `80-get-npcs-in-fragment-uses-current.json`
- **Priority**: Critical
- **Purpose**: Fragment queries use CurrentWorldPosition, not HomePosition
- **Validation**: Traveling NPC appears in destination fragment

**Test 81: Traveling NPC in Correct Fragment**
- **File**: `81-traveling-npc-in-correct-fragment.json`
- **Priority**: High
- **Purpose**: NPC traveling between fragments is queryable in current fragment
- **Validation**: GetNpcsInFragment returns NPC mid-journey

**Test 82: Arrived NPC in Correct Fragment**
- **File**: `82-arrived-npc-in-correct-fragment.json`
- **Priority**: High
- **Purpose**: NPC at location queryable from location's fragment
- **Validation**: GetNpcsInFragment includes NPC at activity location

**Test 83: Teleport via Location Change**
- **File**: `83-teleport-via-location-change.json`
- **Priority**: Medium
- **Purpose**: If CurrentLocationId changes, position jumps immediately
- **Validation**: CurrentWorldPosition updates to new location

**Test 84: Interpolation During Travel**
- **File**: `84-interpolation-during-travel.json`
- **Priority**: High
- **Purpose**: Position smoothly interpolates from start to end
- **Validation**: Position closer to start early, closer to end late

**Test 85: Multi-Waypoint Position Progress**
- **File**: `85-multi-waypoint-position-progress.json`
- **Priority**: Medium
- **Purpose**: Position follows SegmentRoute waypoints in order
- **Validation**: Position updates towards each waypoint sequentially

**Test 86: Position Clamped to Cluster**
- **File**: `86-position-clamped-to-cluster.json`
- **Priority**: Medium
- **Purpose**: CurrentWorldPosition never leaves cluster AABB
- **Validation**: Position within cluster bounds at all times

**Test 87: Position Precision**
- **File**: `87-position-precision.json`
- **Priority**: High
- **Purpose**: Position stored at float precision
- **Validation**: Sub-unit differences preserved (not rounded to grid)

**Test 88: Position Zero Fallback**
- **File**: `88-position-zero-fallback.json`
- **Priority**: Medium
- **Purpose**: If CurrentWorldPosition not set, fallback to HomePosition
- **Validation**: GetNpcsInFragment still works with null CurrentWorldPosition

**Test 89: Multiple NPCs Same Location Different Positions**
- **File**: `89-multiple-npcs-same-location-different-positions.json`
- **Priority**: Medium
- **Purpose**: Multiple NPCs at same location have slightly different positions
- **Validation**: Small position offsets per NPC (scatter around entry point)

**Test 90: Position After Long Simulation**
- **File**: `90-position-after-long-simulation.json`
- **Priority**: High
- **Purpose**: Position remains valid after 60 days of simulation
- **Validation**: No accumulation errors, still within cluster bounds

---

### Category 7: Integration & Edge Cases (Tests 91-102)

Tests full Phase 7 integration and unusual scenarios.

**Test 91: Full Population Extraction**
- **File**: `91-full-population-extraction.json`
- **Priority**: Critical
- **Purpose**: 100 NPCs assigned locations and entry positions
- **Validation**: All 100 have valid CurrentWorldPosition

**Test 92: Spawn at Entry Position**
- **File**: `92-spawn-at-entry-position.json`
- **Priority**: Critical
- **Purpose**: When NPC entity is created, positioned at EntryPosition
- **Validation**: Entity transform matches NpcSchedule entry position

**Test 93: Multi-Cluster Consistency**
- **File**: `93-multi-cluster-consistency.json`
- **Priority**: High
- **Purpose**: Multiple clusters have independent entry positions
- **Validation**: No position collisions across clusters

**Test 94: Fragment Boundary Cases**
- **File**: `94-fragment-boundary-cases.json`
- **Priority**: Medium
- **Purpose**: NPCs at fragment boundaries handled correctly
- **Validation**: Queryable from correct fragment even at edge

**Test 95: Very Dense Location**
- **File**: `95-very-dense-location.json`
- **Priority**: Medium
- **Purpose**: 50+ NPCs at single location (maxed out)
- **Validation**: All queryable from location's fragment, no duplicates

**Test 96: Sparse Cluster**
- **File**: `96-sparse-cluster.json`
- **Priority**: Medium
- **Purpose**: Cluster with very few NPCs (≤ 3)
- **Validation**: All assigned, spread across available locations

**Test 97: All NPCs at Street**
- **File**: `97-all-npcs-at-street.json`
- **Priority**: Medium
- **Purpose**: Scenario where all NPCs are outdoor (unusual)
- **Validation**: All visible, all have IsIndoorActivity=false

**Test 98: All NPCs at Shops**
- **File**: `98-all-npcs-at-shops.json`
- **Priority**: Medium
- **Purpose**: All NPCs indoor (also unusual)
- **Validation**: All invisible, all IsIndoorActivity=true

**Test 99: Position Serialization Roundtrip**
- **File**: `99-position-serialization-roundtrip.json`
- **Priority**: High
- **Purpose**: CurrentWorldPosition survives save/load
- **Validation**: Position values identical before/after

**Test 100: Role-Based Location Affinity**
- **File**: `100-role-based-location-affinity.json`
- **Priority**: High
- **Purpose**: Different roles prefer different location types
- **Validation**: Merchants in shops, workers in offices, authority on streets

**Test 101: Performance: 500 NPCs**
- **File**: `101-performance-500-npcs.json`
- **Priority**: Medium
- **Purpose**: Extraction and position queries at scale
- **Validation**: Completes in < 100ms

**Test 102: Full Simulation Loop**
- **File**: `102-full-simulation-loop.json`
- **Priority**: Critical
- **Purpose**: 60-day simulation with position tracking
- **Validation**: All NPCs in correct fragments throughout, no position errors

---

## Test Execution

All 102 scripts are JSON format, compatible with the ExpectEngine testbed:

```bash
# Run all Phase 7 tests
dotnet run --project examples/Launcher/Karawan.GenericLauncher.csproj -- test models/tests/tale/phase7-spatial

# Run individual category
dotnet run --project examples/Launcher/Karawan.GenericLauncher.csproj -- test models/tests/tale/phase7-spatial/01-*.json
```

Expected result: **102/102 passing**.

