# project Karawan
This project consists of 
- joyce game engine
- silicon desert 2 game


## Build instructions

Check out this repo to any directory. Directly next to this directory, check out the projects 
- [BepuPhysics2] (https://github.com/bepu/bepuphysics2.git) 
- [Default Ecs entity components system] (https://github.com/TimosForks/DefaultEcs.git),
- [3d obj file loader ObjLoader] (https://github.com/TimosForks/ObjLoader.git),
- [fbx model loader] (https://github.com/TimosForks/FbxSharp.git) 
- [glTF model loader] (https://github.com/KhronosGroup/glTF-CSharp-Loader.git)

Then, load the Karawan solution in the Karawan sub-directory of the Karawan repo and build it.
It references the other projects.

Current builds actively supported are Windows 11 x64; Linux x86 and ARM64 Android 13 . Windows 10
and other Android versions also work.

## Configuration

### Historic: nogame.json

The project started out using one single json file containing all of the required configuration 
entires, including assets, engine options and debug options. As this both becomes unmaintable and
is unsuitable for composing the entire project from several files, it was later extended by a
configuration approach that combines several files and blends with the serialization approach.

### Defined configuration hierarchy entry points

#### /defaults/loader/assembly

a string, specifies the root dll of the game to load.

#### /globalSettings

A dictionary of one-time settings required for startup of engine and game.

#### /implementations

A dictionary of implementation records specifying implementations to have available
in the factory.

**implementation record**
- `properties` *(dictionary)* The properties of the implementation object to set after 
  initialization.
- `className` *(string)* The fully qualified class name of the object to instantiate. 
  If not given, the dictionary key is used as a class name. However, in some cases you
  will want to use an interface name as a key, requiring you to specify a specific 
  implementation class here.
- `config` *(any)* When using a custom factory for this type, this contains the input
  data for the deserializer (or the output from the serializer) (see engine.ISerializable
  interface).

#### /mapProviders

A dictionary specifying all map providers for the game. The keys of the dictionary
will be used in alphabetic order by the runtime.

**map provider record**
- `className` *(string)* the class name of the map provider to instantiate.

#### /metaGen

#### /metaGen/fragmentOperators

#### /metaGen/buildingOperators

#### /metaGen/populatingOperators

#### /metaGen/clusterOperators

#### /modules/root/className

A string specifying the class name of the main modules of the game, assumed to be
inside the main dll.

#### /properties

A set of defaults for run-time configuration. These values are modifyable at run
time and monitored for changes. Implementation can subscribe for events specificially
for each of the values.

#### /quests

A dictionary of class descriptors used to load quest definitions.

The key is supposed to be the quests identifier. The value is an instantiation 
rectod

**instantiation record**

either 

- `implementation` *(string)* The fully qualified name of a (static) method to
  instantiate the given object. The method does not take any parameter and is 
  expected to return an object of the appropriate interface 

or

- `className` *(string)* THe fully qualified name of a class to instantiate.

#### /layers

A dictionary of layers for the screen composer with their respective properties.
The key names can be selected without any specific semantics.

**layer record**
- `zOrder` *(number)* A number specifying the zOrder of the layer when composed 
   on screen. Larger numbers or closer to the viewer.

#### /scenes

#### /scenes/catalogue

A dictionary of scenes of the game engine that can be loaded/unloaded. The keys
an be chosen arbitrarily.

**scene record**
- `className` *(string)* the fully qualified class name of the scene
  implementation.

#### /scenes/startup

A string specifying the key of the scene to be displayed on startup.

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
is represented using a single entity of its own. If the node carries
mesh information, the mesh and the materials are represented using
an InstanceDesc object.

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


## Splash Renderer

Splash is the second renderer for joyce. It is now based on OpenGL 3 as provided
by the Silk.NET framework.


### Platform representations

Platform primitives as meshes, materials and textures do have dedicated data
structures on platform side: AMeshEntry, AMaterialEntry and ATextureEntry.
This abstract classes are subclassed by the actual splash platform implementation.
The current Silk Implementation provides the subclasses SkMeshEntry, SkMaterialEntry
and SkTextureEntry. Although it might appear obvious, let's be clear what
these data structures represent:
- AMeshEntry: represents a platform specific object associated with a set of 
  mesh parameters, that is the joyce.Mesh object plus the AMeshParams. The 
  AMeshParams include the scaling of the UVs, which depends on the texture atlas
  used.
- AMaterialEntry: The material entry is a combination of shader parameters plus
  references to ATextureEntry objects that are in use in the material's shader.
- ATextureEntry: A reference to an actual physical texture in use (note: the 
  texture entry is the actual memory object in GPU memory).


#### Common operations

These platform primitives implement
- create: Create the platform representation.
- fill: Prepare and gather data to be available for a later upload.
- upload: upload the data onto the GPU
- unload: remove the data from the GPU
- dispose: delete the entire data structure.

Today, none of these platform objects exactly follows this scheme. However,
they should be converted to follow this scheme very soon.


#### Life cycles

As soon InstanceDesc objects are created (which happens by the time a threeD 
object is created), the platform objects representing Meshes and Materials are
created along the way. That means,  they do exist even for far-away objects that
would not be rendered at all. As such, they should be cheap. 
They are deleted (by help of garbage collection), as soon the last entity 
referencing any of them are deleted.

As soon materials are created, the dependent ATextureEntries shall be found.
They might be in any state, created, filled, uploaded.

As soon materials are filled, the dependent ATextureEntries shall be at least 
filled. We do need the information about the texture's metadata when filling
the mesh (namely the UV scales of a potential texture atlas).

As soon the logical renderer decides to have any InstanceDesc rendered, it would
trigger "Fill" on each of these objects: This should make the implementation
prepare and fetch all platform specific data to prepare an upload.

As soon the physical renderer really needs to render it, it will trigger upload
on any of these items.


#### Caveats

ATextureEntry instances are a special beast: In addition to not being uploaded
as anything else, they also can be outdated. This would happen if they are 
rendered from a framebuffer who is newer inside memory than on GPU. In that case
the texture also would be uploaded by the renderer.

## Entity lifecycle

### Introduction

Entities may be created by several different use cases:
- as an effect of initializing a module internally, like a camera
- as an effect of creatng a new game state or loading an existing one.
- as an effect of dynamic entity based particle systems
- as an effect of world-building operators
- as an effect of fragment-building operators
- built by character spawn behaviors
- built by code from e.g. the scene sequencer.

That is, to re-create the entity, the creating module possibly needs to
assign a creator that is capable of setting up the entity from serializable
data after reload.

Also, each entity, no matter what creator it origins, may have a different
life cycle: Characters' lifetimes may eventually be bound to their location
relatively to the player's location. Static entities are possibly bound to
the fragments they become loaded for, other eyntities, such as particles, may
have a pre-determined, dynamic lifetime, as defined by their behaviours.
Collectible items however, might have an infinite lifetime, only to be
wused at some point by the player.

Therefore we introduce two concepts: creators, and owners.
- Creators: Are capable of (re-)creating entities from serializable data. The 
  data might be part of a save game state, or it may be part of the world
  defining state. The creator id is required to find the right creator to restore 
  a given entity or set of entities.
- Owners: Are capable of determining the life-time of an object and responsible 
  for disposing it after use.

The easiest and preferrable way is to have an entity recreated using automatic
generic deserialization. Generic deser is applied to all entities that
carry a creator id.

In addition, if the creator id is > CreatorId_HardcodeMax, the ICreator that
is registered for this id is asked to serialize / deserialize that particular
interface.

To make all this possible during init/load/save/garbage collect, each
entity may be associated with the Owner component that contains both the
creator and the owner id. During save/load/new/garbage collect cycles, the 
engine this way can select the proper creator to setup the entity accordingly,
during run-time, it can be deleted accordingly. 

Copmponents have serialization information on their own: They can have the
persistable attribute set.

### Deserialization

#### First pass

First, all entities are deserialized by creating the objects with the default
ctor and setting the properties from json. Alternatively, a custom defined
JsonConverter is used to setup the entity. After the component has been  setup
that way, any SetupFrom function is called on the component if it exists.

Finally, if the entity had a creator tag serialized, the ICreator of this 
component is called.

The setup logic takes care that the ICreator SetupFrom is called only after
all individual SetupFrom Tasks have terminated.

### Use Cases

#### Car Characters

#### Player 

#### Camera

#### Buildings

Owner: Fragment

#### Polytopes

Owner: Fragment

#### Cubic stuff in the void

Owner: Fragment

#### Some pre-placed pickable item outside

Owner: Play state setup

#### Polytope particles

Owner: Fragment

#### Lights

Owner: Static Scene Setup

## Save game integration

Any implementation part that wants to be part of a load/save process can register
itself with the Saver module, using its OnBeforeSaveGame and OnAfterLoadGame hooks.

## Defining an NPC

An NPC requires:
- a model, including possible additional animation urls
- a start position (absolute, cluster rel, cluster streetpoint, random proc)
- a navigation behavior, controlling animation as well
- interaction behavior, possibly chaning animation

- requires a behavior state machine
 
## About Narration

The narration is based on inky. It hooks into the savegame system to be able to 
restore itself.

## What do I want?

I want to ride through the waste land.
I want to sometimes encounter TRON like sail ships.
I want to cross data highways.
I want to see entraces to tunnels.
I want to see digital trees.
I want to see beautiful landscape
I want to see skylines
I want to see busy-ness in the cities.
I want to uncover a story by narration elements
I want to buy/sell things
I want to earn money by transporting persons
I want to earn monry by transporting things
I want to be able to be part of something
I want to sometimes fly
I want to look out of a window when resting
I want to know that there are sewers / dungeons below the city
I want to use public transport
I want riding to consume fuel
I want to get out of the car
I want to have a closer work in the city's green parks
I want long travels to require different means of transport
I want to have at least one city - the second one - beautifully set in a valley
I want to ride in a digital sail thing across the void
I want to buy a house to have flowers grow on the outside of the building
I want to earn money to give live to the world
I want to encounter digital beings in the void
I want to have hurds of birds flying across the sky
I want to have a couple of birds once and then on the floor that eventually get disturbed a fly away
I would love to see a being build of segments like in old-school games (r-type)
I want to see equalizer vu meters once and then
I might need to fight virusses or code worms




## Pickable objects

All pickable objects are kept inside the PickableDirectory. This holds the
descriptions and the associated actions for the objects. The Pickabledirectory 
is populated from the game definition json.
During serialization, pickable objects refer to their description using a 
string path.


# Animation mapping

These are the rig namings of our favourite models.

breast.L
breast.R
Fingers.L.001
Fingers.L.002
Fingers.R.001
Fingers.R.002
foot.L
foot.R
forearm.L
forearm.R
hand.L
hand.R
heel.02.L
heel.02.R
Index.L.001
Index.L.002
Index.R.001
Index.R.002
pelvis.L
pelvis.R
shin.L
shin.R
shoulder.L
shoulder.R
spine
spine.001
spine.002
spine.003
spine.004
spine.005
spine.006
thigh.L
thigh.R
Thumb.L.001
Thumb.L.002
Thumb.L.003
Thumb.R.001
Thumb.R.002
Thumb.R.003
toe.L
toe.R
upper_arm.L
upper_arm.R
