#!/bin/bash
# Build libarcarm64.so and libsdl-arcarm64.so for Android ARM64
# Usage: NDK_PATH=/path/to/ndk ./build.sh

set -e
NDK="${NDK_PATH:-D:/android-ndk-r27d}"
TOOLCHAIN="$NDK/toolchains/llvm/prebuilt/windows-x86_64"
CLANG="${TOOLCHAIN}/bin/aarch64-linux-android21-clang++"
CLANG_C="${TOOLCHAIN}/bin/aarch64-linux-android21-clang"
STRIP="${TOOLCHAIN}/bin/llvm-strip"
SYSROOT="${TOOLCHAIN}/sysroot"
BUILD_DIR="$(dirname "$0")"
OUT_DIR="$BUILD_DIR/out"

echo "=== NDK: $NDK ==="
echo "=== Toolchain: $TOOLCHAIN ==="
[ -f "$CLANG" ] || { echo "ERROR: clang++ not found at $CLANG"; exit 1; }

mkdir -p "$OUT_DIR"

# Common flags
CFLAGS="-fPIC -O2 -flto -DANDROID -D__ANDROID_API__=21"
LDFLAGS="-shared -flto -Wl,--gc-sections -Wl,--strip-all"
INCLUDES="-I${BUILD_DIR}/jni_arc -I${BUILD_DIR}/jni_sdl -I${BUILD_DIR}/jni_headers"
INCLUDES="$INCLUDES -I${SYSROOT}/usr/include"
INCLUDES="$INCLUDES -I${SYSROOT}/usr/include/aarch64-linux-android"

# =================== libgl_hook.so ===================
echo ""
echo "=== Building libgl_hook.so ==="

echo "  CC stubs/gl_shader_hook.cpp"
"$CLANG" -c "$BUILD_DIR/stubs/gl_shader_hook.cpp" -o "$BUILD_DIR/build/gl_shader_hook.o" $CFLAGS -std=c++17 $INCLUDES
echo "  LD libgl_hook.so"
"$CLANG" $LDFLAGS "$BUILD_DIR/build/gl_shader_hook.o" -o "$OUT_DIR/libgl_hook.so" -ldl
"$STRIP" --strip-unneeded "$OUT_DIR/libgl_hook.so" 2>/dev/null || true
echo "  -> $OUT_DIR/libgl_hook.so ($(wc -c < "$OUT_DIR/libgl_hook.so") bytes)"

# =================== libarcarm64.so ===================
echo ""
echo "=== Building libarcarm64.so ==="

SOLOUD="$BUILD_DIR/soloud_src"
INCLUDES_ARC="$INCLUDES -I${SOLOUD}/include -I${SOLOUD}/src/audiosource/wav -I${BUILD_DIR}/jni_arc"
INCLUDES_ARC="$INCLUDES_ARC -I${SOLOUD}/src/backend/opensles"

# Soloud sources (all C++)
SOLOUD_SRCS=$(find "$SOLOUD/src/core" "$SOLOUD/src/audiosource/wav" "$SOLOUD/src/filter" "$SOLOUD/src/backend/opensles" -maxdepth 1 -name '*.cpp' 2>/dev/null)
SOLOUD_CSRCS=$(find "$SOLOUD/src/audiosource/wav" -name '*.c' 2>/dev/null)

# JNI arc sources
JNI_ARC_SRCS=$(find "$BUILD_DIR/jni_arc" -name '*.cpp')

# Compile each source to .o
OBJS=""
for src in $JNI_ARC_SRCS $SOLOUD_SRCS; do
    obj="$BUILD_DIR/build/$(basename "$src" .cpp).o"
    mkdir -p "$(dirname "$obj")"
    echo "  CC $src"
    "$CLANG" -c "$src" -o "$obj" $CFLAGS -std=c++17 $INCLUDES_ARC -DWITH_OPENSLES -DSOLOUD_MAX_VOICE_COUNT=100
    OBJS="$OBJS $obj"
done
for src in $SOLOUD_CSRCS; do
    obj="$BUILD_DIR/build/$(basename "$src" .c).o"
    echo "  CC $src"
    "$CLANG_C" -c "$src" -o "$obj" $CFLAGS $INCLUDES_ARC
    OBJS="$OBJS $obj"
done

echo "  LD libarcarm64.so"
"$CLANG" $LDFLAGS $OBJS -o "$OUT_DIR/libarcarm64.so" -lOpenSLES -llog
"$STRIP" --strip-unneeded "$OUT_DIR/libarcarm64.so" 2>/dev/null || true
echo "  -> $OUT_DIR/libarcarm64.so ($(wc -c < "$OUT_DIR/libarcarm64.so") bytes)"

# =================== libsdl-arcarm64.so ===================
echo ""
echo "=== Building libsdl-arcarm64.so ==="

GLEW="$BUILD_DIR/glew_src"
INCLUDES_SDL="$INCLUDES -I${GLEW}"

# GLEW source
GLEW_SRC="$GLEW/glew.c"

# JNI sdl sources
JNI_SDL_SRCS=$(find "$BUILD_DIR/jni_sdl" -name '*.cpp')

# SDL2 stub sources (provide SDL functions without system SDL2)
STUB_SRCS="$BUILD_DIR/stubs/sdl2_shim.cpp"

# SDL2 include path (need SDL2 headers for JNI compilation)
SDL_INCLUDE=""
for dir in "${NDK}/sources" "${SYSROOT}/usr/include/SDL2" "${BUILD_DIR}/SDL"; do
    [ -f "$dir/SDL.h" ] && { SDL_INCLUDE="$dir"; break; }
done
if [ -z "$SDL_INCLUDE" ]; then
    echo "WARNING: SDL2 headers not found. Downloading SDL2 source..."
    SDL2_VER="2.32.8"
    if [ ! -d "$BUILD_DIR/SDL2-$SDL2_VER" ]; then
        curl -sL "https://github.com/libsdl-org/SDL/releases/download/release-${SDL2_VER}/SDL2-${SDL2_VER}.zip" -o "$BUILD_DIR/sdl2.zip"
        unzip -q "$BUILD_DIR/sdl2.zip" -d "$BUILD_DIR/"
    fi
    SDL_INCLUDE="$BUILD_DIR/SDL2-$SDL2_VER/include"
fi

INCLUDES_SDL="$INCLUDES_SDL -I${SDL_INCLUDE}"
INCLUDES_SDL="$INCLUDES_SDL -I${BUILD_DIR}/stubs"

# Compile JNI SDL sources
OBJS_SDL=""
for src in $JNI_SDL_SRCS; do
    obj="$BUILD_DIR/build/$(basename "$src" .cpp)_sdl.o"
    mkdir -p "$(dirname "$obj")"
    echo "  CC $src"
    "$CLANG" -c "$src" -o "$obj" $CFLAGS -std=c++17 $INCLUDES_SDL -DGLEW_STATIC -DGLEW_NO_GLU
    OBJS_SDL="$OBJS_SDL $obj"
done

# Compile SDL2 stub (provides SDL_Init, SDL_CreateWindow, etc.)
obj="$BUILD_DIR/build/sdl2_shim.o"
echo "  CC stubs/sdl2_shim.cpp"
"$CLANG" -c "$STUB_SRCS" -o "$obj" $CFLAGS -std=c++17 $INCLUDES_SDL
OBJS_SDL="$OBJS_SDL $obj"

# Compile GLEW
obj="$BUILD_DIR/build/glew.o"
echo "  CC glew.c"
	"$CLANG_C" -c "$GLEW_SRC" -o "$obj" $CFLAGS $INCLUDES_SDL -I${GLEW} -DGLEW_STATIC -DGLEW_NO_GLU -D__gl31_h_
OBJS_SDL="$OBJS_SDL $obj"

# Link WITHOUT -lGLESv2 -lEGL: GL/EGL symbols resolved at runtime by MobileGlues (loaded RTLD_GLOBAL)
echo "  LD libsdl-arcarm64.so (no -lGLESv2 -lEGL; GL via MobileGlues RTLD_GLOBAL)"
"$CLANG" $LDFLAGS $OBJS_SDL -o "$OUT_DIR/libsdl-arcarm64.so" \
    -ldl -llog -landroid

"$STRIP" --strip-unneeded "$OUT_DIR/libsdl-arcarm64.so" 2>/dev/null || true
echo "  -> $OUT_DIR/libsdl-arcarm64.so ($(wc -c < "$OUT_DIR/libsdl-arcarm64.so") bytes)"

echo ""
echo "=== DONE ==="
ls -la "$OUT_DIR/"
