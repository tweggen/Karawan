### 2026-11-20 — KAR-425: control characters in typed text act as keys again

- **Found** while checking ANDROID.md against the code: `Platform._onKeyChar`, which turned control characters in typed text into key presses and dropped the rest, went with the Silk input path (WP-3.5/WP-5.3, `7dec038e`). Since then nothing filtered.
  - `INPUT_KEY_CHARACTER` (SDL text input) and `INPUT_TEXT_REPLACE` (the Android soft keyboard's commit, `KarawanInputConnection`) both went straight into `InputWidget._insert`.
  - A soft keyboard's Enter, commonly committed as `"\n"`, was stored in the field as a character.
- **Change:**
  - `engine.inputs.TextInputFilter.Split` separates printable runs from control characters: `\b` → Backspace, DEL → Delete, `\n`/`\r` (and `\r\n`, once) → Enter, `\t` → Tab, others dropped.
  - `InputWidget._applyText` runs both text paths through it and applies the actions directly. Enter selects (accepts) the field as the `(enter)` key does; Tab moves focus as the `(tab)` key does, through `Widget._focusNext`, extracted from the base Tab handling.
  - No synthetic key events: they resolve by `ScanCode`, and a synthetic one has none.
- **Docs:** ANDROID.md "Control Character Filtering" describes this.
- **Tests:**
  - `TextInputTests` (5): printable runs, every control mapping, `\r\n` once, others dropped, both widget paths scanned.
  - xUnit 2282 pass; the 26 failures are the known KAR-461 drift. TALE 133/133.
- **Not verified:** the Android build and a soft-keyboard check on a device.
