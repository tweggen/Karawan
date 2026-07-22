# Bird Swarm Over Non-Cluster Fragments — Implementation Plan

## Goal

A swarm of ~40 flying birds (tetrahedron geometry) over **non-cluster** (void) fragments, with **10% chance** per such fragment. The swarm is **owned by the fragment** and **unloaded** when the fragment unloads. Implemented as a **FragmentOperator**, with the option to use a particle-style or per-entity approach.

---

## Architecture (from notebook & codebase)

### Fragment / cluster / world (notebook entry `2b5420a8`)

- **Fragment**: 400×400 units; grid Index3; elevation 21×21; visibility flags.
- **Cluster**: 800–4000 units; contains streets, quarters; has AABB.
- **Fragment loading**: PlayerViewer drives loading; 5×5 fragments (25) around player; Loader aggregates visibility; new fragments get `EnsureVisibility()` → fragment operators.
- **Unloading**: Fragment not in visibility set + age > KeepFragmentMinTime → purge. **Entity cleanup**: entities whose **Owner** component matches the fragment ID are queued for deletion.
- **Operator pipeline**: WorldOperator → ClusterOperator → FragmentOperator. FragmentOperator runs **per fragment** when visibility is applied; **AABB-filtered** (only if operator AABB intersects fragment).

### World generation pipeline (notebook `c9504aed`)

- FragmentOperator: applied to every fragment after (re-)load. Configured under `/metaGen/fragmentOperators`.
- Everything is (re-)creatable on demand.

### Entity lifecycle (notebook `5491230e`)

- **Owner** controls lifetime. Examples: Fragment (buildings, polytopes), Static Scene, Play state.
- Entities with **Owner(fragment)** are removed when the fragment is purged.

### metaGen.json layout

- **Root** `fragmentOperators` children: `CreateTerrainOperator`, `CreateTerrainMeshOperator`, `PlaceDebrisOperator` — run on **every** fragment.
- **Under** `selector: "clusterDescList"` / `target: "clusterDesc"`: cluster-specific operators (streets, houses, trees, polytopes, cubes, tram, niceday) — instantiated **per cluster** and only run for fragments that **intersect that cluster’s AABB**.
- So **“non-cluster” fragments** = fragments that never get the cluster block; i.e. they don’t intersect any cluster. Our bird operator should run only on those (and with 10% chance).

### PlaceDebrisOperator (non-cluster / void)

- **AABB**: `AABB.All` → runs on every fragment.
- **Skip condition**: `GetElevationPixelAt(worldFragment.Position + vCenter).Biome != 0` → only places in **void** (Biome 0). Cluster areas have Biome 1 (ClusterBaseElevationOperator).
- **Placement**: Merged static mesh per fragment; `worldFragment.AddStaticInstance("debris", InstanceDesc.CreateFromMatMesh(matmesh, 800f))` → entities get **Owner(fragment)** and are unloaded with the fragment.

### AddStaticInstance and Owner

- `Fragment.AddStaticInstance(...)` creates entities with `Owner(_id)` (fragment id), so they are unloaded when the fragment is purged.

---

## Design choices

1. **Non-cluster detection**  
   Same as PlaceDebris: run at **root** fragment-operator level (so every fragment is considered), then in `FragmentOperatorApply` only add the swarm when the fragment is void. Use **Biome == 0** at fragment center (or a few samples) so we only place over non-cluster (void) terrain.

2. **10% chance**  
   Seeded RNG from fragment id (e.g. `RandomSource(_myKey + worldFragment.GetId())`). If `rnd.GetFloat() >= 0.1f` → return without adding anything.

3. **~40 birds, tetrahedrons**  
   - **Option A (particle-style)**: One entity per fragment with a “swarm” component holding N positions/velocities; one system updates them and either pushes instance matrices to one InstanceDesc or emits many small InstanceDesc draws. More work, one draw call per swarm.  
   - **Option B (per-entity)**: 40 entities per swarm, each with Instance3 (tetrahedron mesh), Transform3, Owner(fragment), and a small “FlyingBird” (or “SwarmBird”) component; a system moves them in a simple flying path. Reuses existing patterns; many entities/draw calls.  
   Recommend **Option B** for simplicity and consistency with “entity owned by fragment”.

4. **FragmentOperator**  
   - **Path**: e.g. `5050/PlaceBirdSwarmOperator/{_myKey}` so it runs after terrain/debris, before or after cluster block doesn’t matter (we skip when cluster).  
   - **AABB**: `AABB.All` so we’re invoked for every fragment; we skip inside Apply when not void or when 90% roll fails.

5. **Tetrahedron mesh**  
   - Add `engine.joyce.mesh.Tools.CreateTetrahedronMesh(string name, float size)` (4 vertices, 4 triangular faces). Use a small size (e.g. 0.3–0.5) for each bird.

6. **Flying motion**  
   - Minimal: each bird has position + velocity or phase; a system advances them (e.g. sine/circular or linear drift) so they stay roughly over the fragment. No physics needed unless you want it later.

---

## Implementation steps

### 1. Tetrahedron mesh (JoyceCode)

- In `JoyceCode/engine/joyce/mesh/Tools.cs` add:

  - `CreateTetrahedronMesh(string name, float size)`  
  - Regular tetrahedron: 4 vertices, 4 faces, 12 indices; compute from `size` (e.g. half-edge or circumradius). Reuse existing mesh API (Vertices, UVs, Indices; normals if required by pipeline).

### 2. Fragment operator (nogameCode)

- New class, e.g. `nogame.terrain.PlaceBirdSwarmOperator` or `nogame.void.BirdSwarmFragmentOperator`, implementing `IFragmentOperator`:

  - **Constructor**: take a `strKey` (and any config you want for count/chance/size).
  - **FragmentOperatorGetAABB**: `out aabb = AABB.All` (run on every fragment).
  - **FragmentOperatorGetPath**: e.g. `$"5050/PlaceBirdSwarmOperator/{_myKey}"`.
  - **FragmentOperatorApply**:
    - If `(visib.How & FragmentVisibility.Visible3dAny) == 0` → return.
    - `RandomSource rnd = new(_myKey + worldFragment.GetId())`.
    - If `rnd.GetFloat() >= 0.1f` → return (10% chance).
    - Check void: e.g. center of fragment `worldFragment.Position + new Vector3(MetaGen.FragmentSize/2, 0, MetaGen.FragmentSize/2)`; `GetElevationPixelAt`; if `Biome != 0` → return.
    - Create **40** entities (or configurable N):
      - Mesh: `Tools.CreateTetrahedronMesh("bird", 0.4f)` (or similar).
      - Material: register a simple material (e.g. solid color or existing “debris”-style) in constructor.
      - InstanceDesc from one mesh + material; or one shared InstanceDesc and 40 entities with different Transform3.
      - Position: random in fragment XZ, Y above terrain (e.g. `GetHeightAt` + 5..15). Slight random offset so they’re not one point.
      - Each entity: `Instance3`, `Transform3`, **Owner(worldFragment.Id)**. Optional: add a **FlyingBird** component (e.g. phase, base position, amplitude, speed) for a system to animate.

  - **InstantiateFragmentOperator**: same pattern as PlaceDebris: `(string)p["strKey"]`.

### 3. Flying behavior (optional but recommended)

- **FlyingBird** component: e.g. `Phase` (float), `BasePosition` (Vector3), `Amplitude` (float), `Speed` (float).
- **System**: queries entities with `Instance3` + `Transform3` + `FlyingBird` (and optionally `Owner`); each frame updates `Transform3` from `FlyingBird` (e.g. base + horizontal circle or figure-8, vertical sine). No physics.

### 4. Config and registration

- **metaGen.json**  
  In `fragmentOperators.children`, same level as `PlaceDebrisOperator`, add one entry:

  - `"implementation": "nogame.terrain.PlaceBirdSwarmOperator.InstantiateFragmentOperator"`  
  - and a `strKey` (or equivalent) in the same way as PlaceDebris.

- **Implementations** (if needed)  
  If your operator is created via the same factory pattern as PlaceDebris, ensure `/implementations` (or the fragment-operator factory) has a record that supplies `strKey` and calls `InstantiateFragmentOperator`.

### 5. Unload behavior

- No extra work: entities created with **Owner(fragment)** are already purged when the fragment is unloaded (not in visibility set + age > KeepFragmentMinTime). So the swarm is unloaded with the fragment as required.

---

## Summary

| Item | Choice |
|------|--------|
| Where | Non-cluster (void) fragments only; Biome == 0 at fragment center. |
| Chance | 10% per fragment (seeded RNG from fragment id). |
| Count | ~40 birds per swarm. |
| Geometry | Tetrahedrons via new `Tools.CreateTetrahedronMesh`. |
| Lifetime | Owner(fragment) → unloaded with fragment. |
| Motion | Optional FlyingBird component + system (simple sine/circular). |
| Placement | New FragmentOperator at root level; AABB.All; skip when not void or 90% roll. |

This matches the notebook’s fragment/owner/unload model and reuses PlaceDebris’s “void only” and AddStaticInstance’s Owner pattern, while keeping the swarm as fragment-owned entities (or one swarm entity with many logical “birds” if you later switch to Option A).
