# Vendored: SDL3-CS

**Do not edit `SDL3.Core.cs`.** It is auto-generated upstream and vendored verbatim.
Local edits are silently lost the next time it is refreshed.

## Provenance

| | |
|---|---|
| Upstream | https://github.com/flibitijibibo/SDL3-CS |
| Commit | `c42d07ed852beb522b0e3195ad0cc7884579ab0a` (2026-07-07) |
| File | `SDL3/SDL3.Core.cs`, copied verbatim |
| License | zlib — see `LICENSE.SDL3-CS` |
| Native counterpart | SDL `release-3.4.14` @ `147a8ee32dbf9ac02f3794964490687b6bbda1bc`, built by `.github/workflows/natives.yml` and shipped in `Karawan.Natives` |

## Why this vendor and not another

The implementation plan (Phase 2, WP-2.1) requires **flibitijibibo/SDL3-CS** specifically.
`ppy/SDL3-CS` and `edwardgushchin/SDL3-CS` bundle their own native binaries, which would
reintroduce exactly the third-party-native coupling this migration exists to remove. This one
ships **source only** and binds `nativeLibName = "SDL3"`, so it resolves against whatever
`libSDL3.so` / `SDL3.dll` we ship ourselves.

`SDL3.Core.cs` is the CoreCLR variant (requires .NET 8+, uses `LibraryImport` source generation).
We target net9.0, so the `SDL3.Legacy.cs` variant is not needed.

## Binding-vs-native version alignment

SDL3-CS carries no version constant, so alignment cannot be asserted by reading the C#. It was
checked the other way round — against the symbols actually exported by the `libSDL3.so` we build:

```
$ llvm-readelf --dyn-syms -W libSDL3.so | awk '{print $8}' | sed 's/@@.*//'
```

1269 exported `SDL_*` symbols; every entry point the spike calls is present. Note the exports carry
a **`@@SDL3_0.0.0` version suffix**, so any future check must strip it before matching — a naive
exact-match grep silently finds nothing and looks like "the symbol is missing".

## Updating

1. Bump the commit above and re-copy `SDL3/SDL3.Core.cs` verbatim.
2. Re-run the symbol check against the current `libSDL3.so`.
3. If SDL itself moves, `recipes/versions.env` is the pin that matters — update both together.
