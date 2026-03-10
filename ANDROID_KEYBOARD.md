# Android Keyboard Input

## Problem

On Android in landscape mode, the system keyboard opens in **fullscreen/extract mode** by default. This covers the entire screen with a text editor overlay, hiding the game. Additionally, autocomplete and word replacement send composed text that conflicts with the engine's character-by-character input handling.

## Solution (Implemented)

Override `OnCreateInputConnection()` in `Wuka/Platforms/Android/GameSurface.cs`:

- **`ImeFlags.NoFullscreen`** on `EditorInfo.ImeOptions` -- prevents fullscreen/extract mode keyboard in landscape. The keyboard shows as a compact strip at the bottom instead.
- **`InputTypes.ClassText | InputTypes.TextFlagNoSuggestions`** on `EditorInfo.InputType` -- disables autocomplete and word suggestions.

This is the same approach used by games like Genshin Impact (`IME_FLAG_NO_FULLSCREEN`).

### Alternative Fallback (Not Implemented)

If `OnCreateInputConnection` doesn't propagate through SDL's view hierarchy, an SDL hint can be set in `Platform._setKeyboardEnabled()`:

```csharp
// 0x00080001 = TYPE_CLASS_TEXT | TYPE_TEXT_VARIATION_VISIBLE_PASSWORD
SDL_SetHint("SDL_ANDROID_INPUT_TYPE", "0x00080001");
```

`TYPE_TEXT_VARIATION_VISIBLE_PASSWORD` is a more aggressive fallback that also disables suggestions.

## Input Architecture

### Event Flow

```
Platform._onKeyChar(IKeyboard, char)
  -> EventQueue.Push(INPUT_KEY_CHARACTER event)
  -> Engine._onLogicalFrame() processes event queue
  -> Widget.HandleInputEvent() dispatches to focussed widget
  -> InputWidget._handleInputEvent() inserts/deletes characters
```

### Keyboard Enable/Disable Flow

```
InputWidgetImplementation.OnPropertyChanged("focussed", true)
  -> Engine.EnableKeyboard()          (CountedEnabler pattern)
  -> Platform.KeyboardEnabled = true
  -> _setKeyboardEnabled(true)
  -> IKeyboard.BeginInput()           (Silk.NET -> SDL_StartTextInput)
```

On Android, SDL shows the system soft keyboard. `GameSurface.OnCreateInputConnection()` is called by the Android framework whenever the IME connects, allowing us to configure flags before the keyboard appears.

### Key Classes

| Class | Location | Role |
|-------|----------|------|
| InputWidget | JoyceCode/builtin/jt/InputWidget.cs | JT text input widget, handles INPUT_KEY_CHARACTER events |
| InputWidgetImplementation | JoyceCode/builtin/jt/InputWidgetImplementation.cs | Manages keyboard enable/disable on focus change |
| TextWidgetImplementation | JoyceCode/builtin/jt/TextWidgetImplementation.cs | Computes OSDText position/size for rendering |
| OSDText | JoyceCode/engine/draw/components/OSDText.cs | Component with Position, Size, GaugeValue (cursor pos) |
| Engine | JoyceCode/engine/Engine.cs | EnableKeyboard/DisableKeyboard via CountedEnabler |
| IPlatform | JoyceCode/engine/IPlatform.cs | Interface: `KeyboardEnabled { get; set; }` |
| Platform | Splash.Silk/Platform.cs | Calls Silk.NET IKeyboard.BeginInput/EndInput |
| GameSurface | Wuka/Platforms/Android/GameSurface.cs | Android SDL surface, OnCreateInputConnection override |

## Future: Input Field Position Awareness

Currently **no position information** flows from the input field to the platform layer. `IKeyboard.BeginInput()` takes no arguments. The input widget knows its position (via `OSDText.Position` and `OSDText.Size`) but this is only used for rendering.

A future enhancement could:

1. Extend `IPlatform` with `SetInputFieldRect(rect)` to pass the active input field bounds.
2. On Android, report the cursor location via `InputMethodManager.updateCursorAnchorInfo()` / `CursorAnchorInfo`.
3. Listen for keyboard insets (`WindowInsets`) to know the keyboard height.
4. Fire an event back to the game so the UI layer can reposition input fields above the keyboard.

Note: Android does not support moving the keyboard to an arbitrary screen position. The keyboard always renders at the bottom. The game must move its own UI to avoid being covered.
