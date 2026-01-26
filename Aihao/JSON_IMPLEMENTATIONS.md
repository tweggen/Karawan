# Implementations JSON Format

## Overview

The implementations JSON file (`nogame.implementations.json`) defines how interface types are bound to concrete implementations at runtime. The `ImplementationLoader` class processes the `/implementations` section from the Mix configuration and registers factory methods with the engine's dependency injection system (`I.Instance`).

## Location in Mix

- **Path**: `/implementations`
- **Typical file**: `nogame.implementations.json`
- **Loaded by**: `engine.ImplementationLoader`
- **Processed by**: `engine.casette.Loader.CreateFactoryMethod()`

## JSON Structure

The implementations file is a JSON object where:
- **Keys** are fully-qualified interface/type names
- **Values** define how to create instances (can be `null`, an object with options, or specify a factory method)

```json
{
  "Full.Namespace.InterfaceName": <implementation-spec>,
  "Another.Interface": <implementation-spec>,
  ...
}
```

## Implementation Specification Types

### 1. Null / Self-Registering (Default Constructor)

The simplest form: the key itself is used as the class name, instantiated via its default constructor.

```json
{
  "engine.news.InputEventPipeline": null,
  "engine.world.CreatorRegistry": null,
  "Boom.Jukebox": null
}
```

**Behavior**: Creates instance via `engine.rom.Loader.LoadClass(defaultAssembly, key)`

### 2. Explicit Class Name

Specify a different class to instantiate for an interface:

```json
{
  "builtin.map.IMapProvider": {
    "className": "builtin.map.DefaultMapProvider"
  }
}
```

**Behavior**: Creates instance of `className` instead of the key type.

### 3. Factory Method

Invoke a static factory method to create the instance:

```json
{
  "some.IService": {
    "implementation": "some.namespace.FactoryClass.CreateInstance"
  }
}
```

**Behavior**: Calls `FactoryClass.CreateInstance()` static method. The method name is extracted from the last `.` segment.

### 4. Properties Injection

Set public properties on the created instance:

```json
{
  "builtin.controllers.InputMapper": {
    "properties": {
      "MapLogicalToDescription": {
        "<interact>": "Interact with your environment.",
        "<change>": "Change something about you",
        "<map>": "Toggle map"
      },
      "MapButtonToLogical": {
        "input.key.pressed:e": "input.button.pressed:<interact>",
        "input.key.released:e": "input.button.released:<interact>"
      }
    }
  }
}
```

**Supported property value types**:
- `string` values
- `number` values (converted to `float`)
- Nested `object` → `SortedDictionary<string, string>`

### 5. Config Object (ISerializable)

Pass a config object to instances implementing `ISerializable`:

```json
{
  "builtin.modules.inventory.PickableDirectory": {
    "config": {
      "sunglasses": {
        "name": "BB Sunglasses",
        "description": "This is the infamous sunglasses from the X2 fame.",
        "weight": 0.01,
        "volume": 0.2
      },
      "fakesilverwatch": {
        "name": "Silver Looking Watch",
        "description": "This watch might have had better days...",
        "weight": 0.02,
        "volume": 0.1
      }
    }
  }
}
```

**Behavior**: After instantiation, calls `instance.SetupFrom(configJsonNode)` if the instance implements `ISerializable`.

### 6. Cassette Path Reference

Load configuration from another Mix path:

```json
{
  "some.ConfigurableService": {
    "implementation": "some.Factory.Create",
    "cassettePath": "/config/myService"
  }
}
```

**Behavior**: After instantiation, loads the subtree at `cassettePath` and calls `SetupFrom()` on the instance.

## Complete Specification Schema

```json
{
  "InterfaceTypeName": null
  // OR
  "InterfaceTypeName": {
    "className": "string (optional) - class to instantiate",
    "implementation": "string (optional) - static factory method (Namespace.Class.Method)",
    "properties": {
      "PropertyName": "string | number | { key: value, ... }"
    },
    "config": { /* arbitrary JSON passed to ISerializable.SetupFrom() */ },
    "cassettePath": "string (optional) - Mix path to load config from"
  }
}
```

## Processing Rules

1. **Keys starting with `__`** are skipped (reserved for Mix internals like `__include__`)
2. **Creation priority**:
   - If `implementation` is set → Factory method creation
   - If `className` is set → Constructor creation with that class
   - Otherwise → Constructor creation using the key as class name
3. **Post-creation pipeline**:
   1. Instance created (via factory or constructor)
   2. `properties` applied via reflection
   3. `config` passed to `SetupFrom()` if present
   4. `cassettePath` content loaded and passed to `SetupFrom()` if present
4. **Registration**: The factory is registered with `I.Instance.RegisterFactory(interfaceType, factory)`

## Example: Full File

```json
{
  "__include__": "shared.implementations.json",
  
  "engine.news.InputEventPipeline": null,
  "engine.world.CreatorRegistry": null,
  "engine.Saver": null,
  "engine.physics.ShapeFactory": null,
  
  "builtin.map.IMapProvider": {
    "className": "builtin.map.DefaultMapProvider"
  },
  
  "builtin.controllers.InputMapper": {
    "properties": {
      "MapLogicalToDescription": {
        "<interact>": "Interact with your environment.",
        "<map>": "Toggle map"
      },
      "MapButtonToLogical": {
        "input.key.pressed:e": "input.button.pressed:<interact>",
        "input.key.released:e": "input.button.released:<interact>",
        "input.key.pressed:(tab)": "input.button.pressed:<map>",
        "input.key.released:(tab)": "input.button.released:<map>"
      }
    }
  },
  
  "builtin.modules.inventory.PickableDirectory": {
    "config": {
      "sunglasses": {
        "name": "BB Sunglasses",
        "description": "Famous sunglasses from the X2 fame.",
        "weight": 0.01,
        "volume": 0.2
      }
    }
  },
  
  "game.services.ICustomService": {
    "implementation": "game.factories.ServiceFactory.CreateCustomService",
    "cassettePath": "/customServiceConfig"
  }
}
```

## Runtime Behavior

The `ImplementationLoader` constructor subscribes to Mix load completion:

```csharp
I.Get<engine.casette.Loader>().WhenLoaded("/implementations", _whenLoaded);
```

When the implementations section is loaded:
1. Iterates over each key-value pair
2. Creates a factory method via `Loader.CreateFactoryMethod()`
3. Resolves the interface type via `engine.rom.Loader.LoadType()`
4. Registers the factory with `I.Instance.RegisterFactory(type, factory)`

## Compile Mode

If `joyce.CompileMode` global setting is `"true"`, the `ImplementationLoader` is disabled (throws if called). This is for pre-compilation scenarios where implementations are baked in.

## Error Handling

- **Missing type**: Logs warning, continues with other implementations
- **Invalid factory definition**: Throws `ArgumentException`
- **Missing factory method**: Throws `ArgumentException` with details
- **Property not found**: Silently skipped
