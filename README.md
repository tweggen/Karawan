# project Karawan
This project consists of 
- joyce game engine
- silicon desert 2 game


## Build instructions

Check out this repo to any directory. Directly next to this directory, check out the projects 
- [BepuPhysics2] (https://github.com/tweggen/bepuphysics2.git) (312_workaround_assertion_raised_in_addunsafely),
- [Default Ecs entity components system] (https://github.com/tweggen/DefaultEcs.git),
- [3d obj file loader ObjLoader] (https://github.com/tweggen/ObjLoader.git),
- [fbx model loader] (https://github.com/tweggen/FbxSharp.git) 
- [glTG model loader] (https://github.com/KhronosGroup/glTF-CSharp-Loader.git)

Then, load the Karawan solution in the Karawan sub-directory of the Karawan repo and build it.
It references the other projects.

Current builds actively supported are Windows 11 x64; Linux x86 and ARM64 Android 13 . Windows 10
and other Android versions also work.


## Operators

The basic idea of joyce is that everything is (re-)creatable on demand.
To accomplish this, the entire world is composed from results of operators.

### WorldOperator

Applied to the entire world, executed in sequence

### FragmentOperator

Applied to every fragment after it (re-)loads.

### ClusterOperator

Applied to every cluster immediately after creation.

## About Behaviours

There's a lot of behaving things in the world, like the cars, cubes, the trams.
Due to design, if they were shot of just once per segment, they would diffuse
away. To have all characters appear in a reasonable and controlled manner, a
master spawnsystem is used.

The idea is that rather than iterating over the entities over and over again
we should iterate once over the characters, executing whatever is desired.

### Option 1: Iterative

We have the behaviour manager track the number of behaviours and the 
actual behave system to track fragment transitions.
Over the runtime of the engine, the behavior system keeps the count of
behaviors by behavior type and fragment.

### Option 2: Polling

We have a system running every nth frame to count the behaviors and 
to sort the by behavior type and fragment.

### Actions

For every (behavior type, fragment) we check the desired target status,
eventually asking the ISpawnOperator to spawn new entities or to kill
of some existing.

### Target Status?

Computing the target status might prove a bit difficult as it changes
over time. However, obtaining the target status should be pretty efficient,
given that it is called for each of the active fragments (25?) 
every frame.

## About models and instances

When rendering content, joyce tries to use instance rendering as
much as possible. As such, loaded models and generated geometries
essentially are broken down into "InstanceDesc" objects: They group 
a couple of meshes with their materials. If the rendering engine
encounters different instances of, well an instancedesc in the 
same scene, they are rendered using a single render call.

When working with models, even more hierarchial models, this
becomes a bit more confusing: each node of a hierarchial model
is represented using a single entity of its. If the node carries
mesh information, the mesh and the materials are represented using
a InstanceDesc object.

If, in the end, several similar objects are in the scene, the individual
parts will be rendered with a single drawinstance call.

To make this work, a model that we load from disk must be interpreted
multiple times, it may only once be converted into a mesh. This would
be desirable anyway. Therefore we use the to ModelCache to access 
any given model file. If we don't, we end up in having the geometry
data stored multiple times. Note that we don't auto-merge geometry
just as we auto-merge models.

Note that the model builder will use the InstanceDesc objects as they
are, it will not modify them, it rather just assembles a tree of entities
with the appropriate components from a model tree.


## About models and transformations

### Introduction

Internally, joyce is working strictly entity-component-system. 
However, there are some things that strictly are hierarchial, like
models containing parent-child relations. To map that, the hierarchy
API is used together with the transform API: The Hierarchy API adds a
parent reference component, the transform API adds a Transformation
relative to parent component.

### Influences on the transform of a model

In real life, several things might influence the transform matrix
of a model.

- In terms of gameplay, the model might have a transform matrix.
  This matrix might be either derived from an attached physics component
  or defined directly by a gameloop, usually using the behaviour component
  of that entity.
- The author might want to use one pre-configured matrix to be applied 
  to the model they loaded: To e.g. make up for a difference in scale,
  rotation or orientation, or to simply offset the object.
- The model itself comes with a top level matrix, representing the 
  transformations to be done to the root level of the model.

In reality, we want to minimize the amount of work requried to render
a certain model/character/entity. Let's therefore analyse the most common 
use case, a model controlled by a behaviour by setting its position etc. 
directly. Still, this model would come with a "adjustment" matrix, fitting the
model geometry into the game. However, let's assume this model is not hierarchial, i.e. consists just
of one layer of geometry.

#### Non-hierarchial model

To keep execution fast, the recommended way is:
- bake model adjustment matrix into the InstanceDesc of the object.
- directly set TransformToWorld matrix, or let physics do that.

#### Hierarchial model

If the model itself is hierarchial, you cannot use the instancedesc
transform matrix to adjust the model. Instead, it is recommended to
bake the adjustment into the top-level "ToParent" component of the model.
This does, however, not solve the problem of how to set the object's position.
We do recommmend to insert an additional entity here. It contain any sort
of transformation component (ToWorld or ToParent). That component can
be updated by physics or by behavior.

