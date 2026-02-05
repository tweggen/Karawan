# L-System Editor and Language Features

This document describes the data-driven L-System features in Karawan, including the expression language, JSON format, Lua integration, and the Aihao visual editor.

## Overview

L-Systems (Lindenmayer Systems) are used in Karawan to procedurally generate trees, buildings, and other structures. The system has been enhanced to support:

- **JSON-based definitions** instead of hardcoded C# lambdas
- **Expression language** for conditions and parameter calculations
- **Lua scripting** for complex logic
- **Visual editor** in the Aihao IDE

## Expression Language

The expression language allows you to write conditions and calculate parameter values without writing C# code.

### Variables

Reference L-system parameters using the `$` prefix:

```
$r          # radius parameter
$l          # length parameter
$height     # any named parameter
```

### Operators

**Arithmetic:**
```
$r * 0.6        # multiplication
$l + 1.5        # addition
$x - $y         # subtraction
$a / $b         # division
$n % 2          # modulo
```

**Comparison:**
```
$r > 0.02       # greater than
$l < 1.0        # less than
$x >= $y        # greater or equal
$a <= $b        # less or equal
$x == $y        # equality
$x != $y        # inequality
```

**Logical:**
```
$r > 0.02 && $l > 0.1    # and
$a || $b                  # or
!$flag                    # not
```

**Ternary:**
```
$r > 0.5 ? 1.0 : 0.5     # conditional
```

### Built-in Functions

**Random:**
```
rnd()              # random float 0.0 to 1.0
rnd(10, 20)        # random float in range
```

**Trigonometry:**
```
sin($angle)        # sine (radians)
cos($angle)        # cosine
tan($angle)        # tangent
```

**Math:**
```
abs($x)            # absolute value
sqrt($x)           # square root
pow($base, $exp)   # power
min($a, $b)        # minimum
max($a, $b)        # maximum
floor($x)          # floor
ceil($x)           # ceiling
round($x)          # round
```

**Utilities:**
```
clamp($v, $lo, $hi)    # clamp to range
lerp($a, $b, $t)       # linear interpolation
deg2rad($degrees)      # degrees to radians
rad2deg($radians)      # radians to degrees
```

## JSON Format

L-Systems are defined in JSON with three main sections: seed, rules, and macros.

### Basic Structure

```json
{
  "definitions": {
    "tree1": {
      "name": "tree1",
      "seed": {
        "parts": [
          { "name": "rotate(d,x,y,z)", "params": { "d": 90, "x": 0, "y": 0, "z": 1 } },
          { "name": "stem(r,l)", "params": { "r": 0.10, "l": "1 + 3 * rnd()" } }
        ]
      },
      "rules": [
        {
          "match": "stem(r,l)",
          "probability": 1.0,
          "condition": "$r > 0.02 && $l > 0.1",
          "transform": [
            { "name": "stem(r,l)", "params": { "r": "$r * 1.05", "l": "$l * 0.8" } },
            { "name": "push()" },
            { "name": "rotate(d,x,y,z)", "params": { "d": "30 + rnd() * 30", "x": 0, "y": 0, "z": 1 } },
            { "name": "stem(r,l)", "params": { "r": "$r * 0.6", "l": "$l * 0.8" } },
            { "name": "pop()" }
          ]
        }
      ],
      "macros": [
        {
          "match": "stem(r,l)",
          "transform": [
            { "name": "fillrgb(r,g,b)", "params": { "r": 0.4, "g": 0.25, "b": 0.1 } },
            { "name": "cyl(r,l)", "params": { "r": "$r", "l": "$l" } }
          ]
        }
      ]
    }
  }
}
```

### Sections Explained

**Seed**: The initial state of the L-system. Contains the starting parts.

**Rules**: Transformation rules that are applied iteratively. Each rule has:
- `match`: The part pattern to match (e.g., `stem(r,l)`)
- `condition`: Expression that must be true for the rule to apply (optional)
- `probability`: Chance of applying this rule, 0.0 to 1.0 (default: 1.0)
- `transform`: List of parts that replace the matched part

**Macros**: Final expansion rules applied once at the end to convert abstract parts into geometry.

### Configuration Objects

For parametric L-systems (like buildings), you can define configuration objects:

```json
{
  "configs": {
    "house1": {
      "storyHeight": 3.0,
      "minSegmentStories": 4,
      "shrinkAmount": 2.0,
      "segmentProbability": 0.8,
      "materials": {
        "wall": "materials/buildingwall2",
        "roof": "materials/buildingroof3",
        "floor": "materials/buildingfloor1"
      }
    }
  }
}
```

## Lua Integration

For complex logic that can't be expressed in the simple expression language, use Lua with the `lua:` prefix.

### Basic Usage

```json
{
  "condition": "lua: return p.r > 0.02 and customLogic(p.l)",
  "params": {
    "angle": "lua: return 30 + rnd() * 30"
  }
}
```

### Parameter Access

In Lua expressions, parameters are available in two ways:
- Via the `p` table: `p.r`, `p.l`, `p.height`
- Directly as globals: `r`, `l`, `height`

### Available Functions

All expression language functions are available in Lua:
- `sin`, `cos`, `tan`, `abs`, `sqrt`, `pow`, `min`, `max`
- `floor`, `ceil`, `round`
- `rnd()`, `rnd2(min, max)`
- `deg2rad`, `rad2deg`, `clamp`, `lerp`

### Complex Logic Example

```json
{
  "condition": "lua: if p.r < 0.01 then return false end; return p.l > 0.1 and math.random() < 0.8"
}
```

## Aihao Visual Editor

The Aihao IDE includes a visual editor for L-Systems accessible via the "lsystems" section in the project tree.

### Editor Layout

The editor uses a 3-pane layout:

1. **Left Pane**: Tree view listing all L-system definitions and configurations
2. **Center Pane**: Main editor for the selected definition or configuration
3. **Right Pane**: Detail panel for selected rules/macros

### Definition Editor

When editing an L-system definition, you can:
- Edit the name
- Manage seed parts (add, remove, edit names and parameters)
- Manage rules (add, remove, select for detail editing)
- Manage macros (add, remove, select for detail editing)

### Rule Detail Panel

When a rule is selected, the detail panel shows:
- Match pattern
- Condition expression
- Probability (0.0 to 1.0)
- Transform parts with reordering (move up/down) and editing

### Config Editor

For parametric configurations:
- Story height, min segment stories, shrink amount, segment probability
- Materials map with key-value pairs

### Workflow

1. Open your project in Aihao
2. Navigate to the "lsystems" section in the project tree (or find the `*.lsystems.json` file)
3. Double-click to open the L-System editor
4. Select a definition or config from the left pane
5. Edit properties in the center pane
6. For rules/macros, click to select and edit details in the right pane
7. Use the toolbar buttons to add new definitions or configs
8. Save changes with the Save button

## File Locations

- **L-System definitions**: `models/nogame.lsystems.json`
- **Expression evaluator**: `JoyceCode/builtin/tools/Lindenmayer/Expressions/`
- **JSON loader**: `JoyceCode/builtin/tools/Lindenmayer/LSystemLoader.cs`
- **Editor ViewModels**: `Aihao/ViewModels/LSystem/`
- **Editor Views**: `Aihao/Views/LSystemEditor.axaml`

## Example: Creating a New Tree

1. Open the L-System editor in Aihao
2. Click "+ Definition" to create a new L-system
3. Rename it (e.g., "oak_tree")
4. Add seed parts:
   - `rotate(d,x,y,z)` with params: d=90, x=0, y=0, z=1
   - `stem(r,l)` with params: r=0.15, l="2 + rnd() * 2"
5. Add a rule:
   - Match: `stem(r,l)`
   - Condition: `$r > 0.03`
   - Transform: stem, push, rotate, stem, pop, rotate, stem
6. Add a macro:
   - Match: `stem(r,l)`
   - Transform: fillrgb (brown color), cyl with $r and $l
7. Save and test in-game

## Migration from C# Definitions

If you have existing C# L-system definitions:

1. Identify the seed parts and convert to JSON format
2. Convert each `Rule` with its condition `Func<Params, bool>` to a condition expression
3. Convert the production `Func<Params, IList<Part>>` to a transform array
4. Convert terminal rules to macros
5. Test that the generated output matches the original
