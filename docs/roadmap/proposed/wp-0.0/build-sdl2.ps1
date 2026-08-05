$ErrorActionPreference = "Continue"
$scratch = "C:\Users\timow\AppData\Local\Temp\claude\C--Users-timow-coding-github-Karawan\f71c3165-c124-4d56-b5f6-8ece857a37bf\scratchpad\wp00"
$ndk     = "C:\Program Files (x86)\Android\AndroidNDK\android-ndk-r27c"
$tools   = Join-Path $scratch "tools"
$src     = Join-Path $scratch "SDL2-src"
$ninja   = Join-Path $tools "ninja.exe"
$env:PATH = "$tools;$env:PATH"

Write-Output "=== source version ==="
Select-String -Path "$src\CMakeLists.txt" -Pattern "^set\(SDL_(MAJOR|MINOR|MICRO)_VERSION" | ForEach-Object { $_.Line.Trim() }

foreach ($abi in @("arm64-v8a", "x86_64")) {
    $bld = Join-Path $scratch "build-$abi"
    Write-Output ""
    Write-Output "############ CONFIGURE $abi ############"
    $cfg = @(
        "-S", $src, "-B", $bld, "-G", "Ninja",
        "-DCMAKE_MAKE_PROGRAM=$ninja",
        "-DCMAKE_TOOLCHAIN_FILE=$ndk\build\cmake\android.toolchain.cmake",
        "-DANDROID_USE_LEGACY_TOOLCHAIN_FILE=OFF",
        "-DANDROID_ABI=$abi",
        "-DANDROID_PLATFORM=android-21",
        "-DANDROID_STL=c++_static",
        "-DCMAKE_BUILD_TYPE=Release",
        "-DCMAKE_POLICY_VERSION_MINIMUM=3.5",
        "-DSDL_SHARED=ON", "-DSDL_STATIC=OFF", "-DSDL_TEST=OFF",
        "-DCMAKE_SHARED_LINKER_FLAGS=-Wl,-z,max-page-size=16384 -Wl,-z,common-page-size=16384 -lc++_static -lc++abi"
    )
    & cmake @cfg 2>&1 | Select-Object -Last 20
    Write-Output "configure exit: $LASTEXITCODE"
    if ($LASTEXITCODE -ne 0) { continue }

    Write-Output "############ BUILD $abi ############"
    & cmake --build $bld --target SDL2 2>&1 | Select-Object -Last 12
    Write-Output "build exit: $LASTEXITCODE"
}

Write-Output ""
Write-Output "=== produced .so ==="
Get-ChildItem $scratch -Recurse -Filter "libSDL2.so" -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -like "*build-*" } |
    ForEach-Object { "{0}  ({1} bytes)" -f $_.FullName, $_.Length }
