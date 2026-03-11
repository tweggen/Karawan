# Android Keyboard Input

## Problem

On Android in landscape mode, the system keyboard opens in **fullscreen/extract mode** by default. This covers the entire screen with a text editor overlay, hiding the game. Additionally, autocomplete and word replacement send composed text that conflicts with the engine's character-by-character input handling.

### IME Composition Bug (Fixed)

Android IME uses a composing region for autocorrect/prediction. SDL translates IME operations (`commitText`, `setComposingText`) into backspace + character sequences. But InputWidget doesn't track the composing region, so SDL's backspaces (meant to clear the composition buffer) eat into committed text, corrupting the cursor position. After selecting an autocorrect suggestion and continuing to type, characters would appear at position 0 in reverse order.

## Solution

### Custom InputConnection (KarawanInputConnection)

`KarawanInputConnection` wraps SDL's `IInputConnection` and intercepts IME operations directly:

- **`CommitText`** — emits `INPUT_TEXT_REPLACE` event (deletes composing region + inserts committed text atomically)
- **`SetComposingText`** — tracks composing length only (no preview in text field)
- **`DeleteSurroundingText`** — emits proper backspace/delete events for real text deletion
- **`SendKeyEvent`** — delegates to SDL's connection for physical key handling

This replaces SDL's broken backspace+retype translation with clean, atomic text operations.

### Input Type Support

InputWidget supports an `inputType` attribute (set in JT XML) that configures the Android keyboard:

| inputType | Android InputType | Composition | Use case |
|-----------|------------------|-------------|----------|
| `text` (default) | ClassText + NoSuggestions | Via KarawanInputConnection | General text |
| `email` | TextVariationEmailAddress | Disabled (per-char commit) | Email addresses |
| `password` | TextVariationVisiblePassword | Disabled | Passwords |
| `number` | ClassNumber | Disabled | Numeric input |

Usage in JT XML:
```xml
<input inputType="email" text="" />
```

### Control Character Filtering

`Platform._onKeyChar` translates control characters into proper key events instead of inserting them as literal text:

- `\b` (0x08) → `(backspace)` press+release
- `\t` (0x09) → `(tab)` press+release
- `\n`/`\r` → `(enter)` press+release
- `\x7f` (DEL) → `(delete)` press+release
- Other `char.IsControl()` characters → silently dropped

### Keyboard Landscape Mode

Override `OnCreateInputConnection()` in `GameSurface.cs`:

- **`ImeFlags.NoFullscreen`** on `EditorInfo.ImeOptions` -- prevents fullscreen/extract mode keyboard in landscape
- Input type flags set based on the active widget's `inputType` attribute

## Input Architecture

### Event Flow (Desktop)

```
Platform._onKeyChar(IKeyboard, char)
  -> control char filtering
  -> EventQueue.Push(INPUT_KEY_CHARACTER event)
  -> Engine._onLogicalFrame() processes event queue
  -> Widget.HandleInputEvent() dispatches to focussed widget
  -> InputWidget._handleSelfInputEvent() inserts/deletes characters
```

### Event Flow (Android with KarawanInputConnection)

```
Android IME commitText("hello")
  -> KarawanInputConnection.CommitText()
  -> EventQueue.Push(INPUT_TEXT_REPLACE, code="hello", data1=composingLength)
  -> Engine._onLogicalFrame() processes event queue
  -> InputWidget._handleSelfInputEvent()
  -> _replaceText(): backspace composingLength chars, then insert "hello"
```

### Keyboard Enable/Disable Flow

```
InputWidgetImplementation.OnPropertyChanged("focussed", true)
  -> Engine.SetKeyboardInputType(inputType)  (reads widget's inputType attr)
  -> Engine.EnableKeyboard()                 (CountedEnabler pattern)
  -> Platform.KeyboardEnabled = true
  -> _setKeyboardEnabled(true)
  -> IKeyboard.BeginInput()                  (Silk.NET -> SDL_StartTextInput)
  -> Android: GameSurface.OnCreateInputConnection() called
  -> Returns KarawanInputConnection (text) or SDL's connection (email/password/number)
```

### Key Classes

| Class | Location | Role |
|-------|----------|------|
| InputWidget | JoyceCode/builtin/jt/InputWidget.cs | JT text input widget, handles INPUT_KEY_CHARACTER and INPUT_TEXT_REPLACE |
| InputWidgetImplementation | JoyceCode/builtin/jt/InputWidgetImplementation.cs | Manages keyboard enable/disable, passes inputType |
| KarawanInputConnection | Wuka/Platforms/Android/KarawanInputConnection.cs | Custom InputConnection wrapping SDL's, emits INPUT_TEXT_REPLACE |
| TextWidgetImplementation | JoyceCode/builtin/jt/TextWidgetImplementation.cs | Computes OSDText position/size for rendering |
| OSDText | JoyceCode/engine/draw/components/OSDText.cs | Component with Position, Size, GaugeValue (cursor pos) |
| Engine | JoyceCode/engine/Engine.cs | EnableKeyboard/DisableKeyboard, SetKeyboardInputType |
| IPlatform | JoyceCode/engine/IPlatform.cs | Interface: `KeyboardEnabled`, `KeyboardInputType` |
| Platform | Splash.Silk/Platform.cs | Calls Silk.NET BeginInput/EndInput, control char filtering |
| GameSurface | Wuka/Platforms/Android/GameSurface.cs | Android SDL surface, input type selection |

## Future: Input Field Position Awareness

Currently **no position information** flows from the input field to the platform layer. `IKeyboard.BeginInput()` takes no arguments. The input widget knows its position (via `OSDText.Position` and `OSDText.Size`) but this is only used for rendering.

A future enhancement could:

1. Extend `IPlatform` with `SetInputFieldRect(rect)` to pass the active input field bounds.
2. On Android, report the cursor location via `InputMethodManager.updateCursorAnchorInfo()` / `CursorAnchorInfo`.
3. Listen for keyboard insets (`WindowInsets`) to know the keyboard height.
4. Fire an event back to the game so the UI layer can reposition input fields above the keyboard.

Note: Android does not support moving the keyboard to an arbitrary screen position. The keyboard always renders at the bottom. The game must move its own UI to avoid being covered.
