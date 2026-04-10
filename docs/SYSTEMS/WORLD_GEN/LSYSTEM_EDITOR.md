# Plan: Data-Driven L-System Editor for Aihao IDE

## Summary

Replace C# lambda-based L-system definitions with a JSON-based format that can be edited visually in the Aihao IDE using a node graph editor. Support simple expressions for most cases with optional Lua for complex logic.

## Current State

L-systems for trees and buildings are defined in C# with lambdas:
- `TreeInstanceGenerator.cs` - Tree L-systems with hardcoded rules
- `HouseInstanceGenerator.cs` - Building L-systems with hardcoded rules
- Rules use `Func<Params, bool>` for conditions and `Func<Params, IList<Part>>` for transformations

## Proposed Solution

### 1. JSON Schema for L-Systems

Define L-systems in JSON with expression strings instead of C# lambdas:

```json
{
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
        { "name": "fillrgb(r,g,b)", "params": { "r": 0.2, "g": 0.7, "b": 0.1 } },
        { "name": "cyl(r,l)", "params": { "r": "$r", "l": "$l" } }
      ]
    }
  ]
}
```

### 2. Expression Language

**Simple expressions** (cover 80%+ of use cases):
- Parameter references: `$r`, `$l`, `$x`
- Arithmetic: `+`, `-`, `*`, `/`, `%`
- Comparisons: `>`, `<`, `>=`, `<=`, `==`, `!=`
- Logical: `&&`, `||`, `!`
- Functions: `rnd()`, `rnd(min,max)`, `sin`, `cos`, `abs`, `min`, `max`, `clamp`, `lerp`

**Lua escape** for complex cases:
```json
{ "condition": "lua: return p.r > 0.02 and customLogic(p.l)" }
```

### 3. Implementation Components

#### Phase 1: Expression Evaluator (Engine)
| File | Purpose |
|------|---------|
| `JoyceCode/builtin/tools/Lindenmayer/Expressions/ExpressionLexer.cs` | Tokenize expression strings |
| `JoyceCode/builtin/tools/Lindenmayer/Expressions/ExpressionParser.cs` | Parse to AST |
| `JoyceCode/builtin/tools/Lindenmayer/Expressions/ExpressionEvaluator.cs` | Evaluate AST |
| `JoyceCode/builtin/tools/Lindenmayer/Expressions/ExprNode.cs` | AST node types |
| `JoyceCode/builtin/tools/Lindenmayer/Expressions/ExpressionContext.cs` | Variable/function context |

#### Phase 2: JSON Loading (Engine)
| File | Purpose |
|------|---------|
| `JoyceCode/builtin/tools/Lindenmayer/LSystemDefinition.cs` | JSON DTO classes |
| `JoyceCode/builtin/tools/Lindenmayer/LSystemLoader.cs` | Load JSON, create System objects |

#### Phase 3: Lua Integration (Engine)
| File | Purpose |
|------|---------|
| `JoyceCode/builtin/tools/Lindenmayer/Expressions/LuaExpressionEvaluator.cs` | Evaluate `lua:` prefixed expressions |

#### Phase 4: L-System Definitions (Game Config)
| File | Purpose |
|------|---------|
| `models/nogame.lsystems.json` | Tree and building L-system definitions |

#### Phase 5: Visual Node Graph Editor (Aihao IDE)
| File | Purpose |
|------|---------|
| `Aihao/ViewModels/LSystem/LSystemEditorViewModel.cs` | Main editor ViewModel |
| `Aihao/ViewModels/LSystem/LSystemNodeViewModel.cs` | Node in graph |
| `Aihao/ViewModels/LSystem/LSystemConnectionViewModel.cs` | Connection between nodes |
| `Aihao/ViewModels/LSystem/LSystemDocumentViewModel.cs` | Dockable document wrapper |
| `Aihao/Views/LSystem/LSystemEditor.axaml` | Node graph canvas view |
| `Aihao/Views/LSystem/LSystemNodeView.axaml` | Individual node template |
| `Aihao/Views/LSystem/LSystemDocumentView.axaml` | Document view |

#### Modifications to Existing Files
| File | Change |
|------|--------|
| `nogameCode/nogame/cities/TreeInstanceGenerator.cs` | Load from JSON via LSystemLoader |
| `nogameCode/nogame/cities/HouseInstanceGenerator.cs` | Load from JSON via LSystemLoader |
| `models/nogame.json` | Add `"__include__": ["nogame.lsystems.json"]` |
| `Aihao/ViewModels/Dock/AihaoDockFactory.cs` | Register LSystemDocumentViewModel |
| `Aihao/ViewModels/MainWindowViewModel.cs` | Add OpenLSystemEditor action |
| `Aihao/Views/MainWindow.axaml` | Add DataTemplate for LSystemDocumentView |

## Node Graph Editor Design

```
+------------------+     +-------------------+     +------------------+
|      SEED        | --> |      RULE         | --> |     MACRO        |
|  [initial parts] |     | match: stem(r,l)  |     | stem -> geometry |
+------------------+     | cond: $r > 0.02   |     +------------------+
                         | prob: 1.0         |
                         +-------------------+
                               |
                         [transform]
                               |
                    +----------+----------+
                    |                     |
              +-----v-----+         +-----v-----+
              |   PART    |         |   PART    |
              | push()    |         | stem(r,l) |
              +-----------+         | r: $r*0.6 |
                                    +-----------+
```

**Node Types:**
- **Seed Node**: Entry point, contains initial parts
- **Rule Node**: Transformation rule with match pattern, condition, probability
- **Macro Node**: Final expansion rule (same as Rule but runs at end)
- **Part Node**: Individual turtle command with parameters
- **Sequence Node**: Groups parts (for visual clarity with push/pop)

## Verification

1. **Unit Tests**: Test expression parser/evaluator with known expressions
2. **Round-Trip Test**: Load JSON L-system, generate tree, compare output to current hardcoded version
3. **Visual Test**: Run game, verify trees/buildings render identically
4. **Editor Test**: Open Aihao, edit L-system, save, reload, verify changes persist
5. **Lua Test**: Create L-system with `lua:` expression, verify it executes correctly

## Implementation Order

1. Expression evaluator (can be tested in isolation)
2. JSON DTOs and loader
3. Convert existing tree L-systems to JSON
4. Verify runtime loading produces identical trees
5. Add Lua support
6. Build Aihao node graph editor
7. Convert building L-systems to JSON

## Current Progress

### Completed:
- [x] `ExprNode.cs` - AST node types (NumberNode, StringNode, BooleanNode, VariableNode, BinaryOpNode, UnaryOpNode, FunctionCallNode, TernaryNode)
- [x] `ExpressionContext.cs` - Variable lookup and built-in functions (rnd, sin, cos, tan, abs, sqrt, pow, min, max, clamp, lerp, floor, ceil, round, deg2rad, rad2deg)
- [x] `ExpressionLexer.cs` - Tokenizer with support for numbers, strings, variables ($name), operators, function calls
- [x] `ExpressionParser.cs` - Recursive descent parser with proper operator precedence
- [x] `ExpressionEvaluator.cs` - High-level API with caching, literal detection, and typed evaluation methods

### Phase 1 Complete!
The expression evaluator is fully implemented. It supports:
- Arithmetic: `+`, `-`, `*`, `/`, `%`
- Comparisons: `>`, `<`, `>=`, `<=`, `==`, `!=`
- Logical: `&&`, `||`, `!`
- Ternary: `condition ? trueExpr : falseExpr`
- Variables: `$r`, `$l`, `$x`
- Functions: `rnd()`, `sin()`, `cos()`, `clamp()`, `lerp()`, etc.
- Literals: numbers, strings, booleans

### Phase 2 Complete!
JSON loading is implemented:
- [x] `LSystemDefinition.cs` - JSON DTO classes (LSystemDefinition, SeedDefinition, RuleDefinition, PartDefinition, LSystemCatalog)
- [x] `LSystemLoader.cs` - Loads JSON and creates runtime System objects with expression-based conditions and transforms

### Phase 4 Complete!
L-system JSON definitions and TreeInstanceGenerator integration:
- [x] `models/nogame.lsystems.json` - Tree L-system definitions (tree1 and tree2)
- [x] `models/nogame.json` - Added lsystems include
- [x] `TreeInstanceGenerator.cs` - Now loads from JSON config with fallback to hardcoded definitions

### HouseInstanceGenerator Updated!
House generation now loads configuration from JSON:
- [x] `LSystemDefinition.cs` - Added LSystemConfig class for parametric L-systems
- [x] `models/nogame.lsystems.json` - Added house1 configuration (materials, thresholds, probabilities)
- [x] `HouseInstanceGenerator.cs` - Loads config from JSON, uses configurable values for story height, shrink amount, materials, etc.

**Note**: House generation requires C# polygon operations (`PolyTool.Extend`, etc.) that can't be expressed in simple expressions. The JSON configures parameters, but the core algorithm stays in C#. Full JSON conversion would require Lua (Phase 3).

### Phase 3 Complete!
Lua integration is now available for complex L-system expressions:
- [x] `LuaExpressionEvaluator.cs` - Evaluates `lua:` prefixed expressions using NLua
- [x] `ExpressionEvaluator.cs` - Updated to delegate to Lua evaluator for `lua:` expressions
- [x] `ExpressionContext.cs` - Added Parameters property for Lua access

**Lua Expression Features:**
- Use `lua:` prefix to switch to Lua evaluation
- Full Lua language support for complex logic
- Access L-system parameters via `p.r`, `p.l`, etc. or directly as `r`, `l`
- Built-in functions: `sin`, `cos`, `tan`, `abs`, `sqrt`, `pow`, `min`, `max`, `floor`, `ceil`, `round`
- Random functions: `rnd()`, `rnd2(min, max)`
- Conversion helpers: `deg2rad`, `rad2deg`, `clamp`, `lerp`
- Compiled script caching for performance

**Example:**
```json
{
  "condition": "lua: return p.r > 0.02 and customLogic(p.l)",
  "params": { "angle": "lua: return 30 + rnd() * 30" }
}
```

### Phase 5 Complete!
Aihao L-System Editor is now implemented:
- [x] `Aihao/ViewModels/LSystem/LSystemPartViewModel.cs` - Part (turtle command) ViewModel with parameters
- [x] `Aihao/ViewModels/LSystem/LSystemRuleViewModel.cs` - Rule ViewModel with match, condition, probability, transform
- [x] `Aihao/ViewModels/LSystem/LSystemDefinitionViewModel.cs` - Full L-system definition with seed, rules, macros
- [x] `Aihao/ViewModels/LSystem/LSystemEditorViewModel.cs` - Main editor with definition/config management
- [x] `Aihao/ViewModels/Dock/DockViewModels.cs` - Added LSystemsDocumentViewModel
- [x] `Aihao/Models/SectionDefinition.cs` - Added "lsystems" section
- [x] `Aihao/ViewModels/MainWindowViewModel.cs` - Added case handler for lsystems
- [x] `Aihao/Views/LSystemEditor.axaml` - Main editor view with 3-pane layout
- [x] `Aihao/Views/Dock/LSystemsDocumentView.axaml` - Document wrapper view
- [x] `Aihao/Views/MainWindow.axaml` - Added DataTemplate for LSystemsDocumentViewModel

**Editor Features:**
- Tree view listing all L-system definitions and configurations
- Definition editor with seed parts, rules, and macros sections
- Config editor for parametric L-systems (story height, shrink amount, materials)
- Rule detail panel with match pattern, condition expression, probability, transform parts
- Add/remove operations for definitions, configs, rules, parts, and parameters

## All Phases Complete!

## Key Design Decisions

1. **Expression syntax**: Use `$varname` for variables (clear distinction from identifiers)
2. **Type coercion**: All arithmetic works on floats, booleans coerce to 0/1
3. **Short-circuit evaluation**: `&&` and `||` don't evaluate right side if not needed
4. **Lua prefix**: Use `lua:` prefix to switch to Lua evaluation for complex logic
5. **JSON params**: Parameters can be literal values or expression strings (detected by presence of operators/variables)
