/*
 * Reconstruction of Silk.NET's libmain.so shim, recovered by disassembling
 * jni/arm64-v8a/libmain.so from Silk.NET.Windowing.Sdl 2.23.0.
 *
 * Original exports (from .dynsym):
 *   CurrentMain : GLOBAL OBJECT, 8 bytes   (function pointer, in .bss)
 *   sdSetMain   : GLOBAL FUNC,  16 bytes   (store x0 -> CurrentMain)
 *   SDL_main    : GLOBAL FUNC,  52 bytes   (if (!CurrentMain) return -1;
 *                                           CurrentMain(); return 0;)
 *
 * The callback is invoked with no arguments (blr x8 with no register setup)
 * and its return value is discarded (SDL_main returns w0 = 0 on success).
 */

int (*CurrentMain)(void) = 0;

void sdSetMain(int (*fn)(void))
{
    CurrentMain = fn;
}

int SDL_main(int argc, char *argv[])
{
    (void)argc;
    (void)argv;
    if (!CurrentMain) {
        return -1;
    }
    CurrentMain();
    return 0;
}
