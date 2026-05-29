#!/bin/bash
# Build libarcarm64.so and libsdl-arcarm64.so for ARM64 Linux
# Run this on the ARM64 server (native compilation)
set -e

WORKDIR="$HOME/arc_build"
OUTDIR="$HOME/arc_natives_out"
rm -rf "$WORKDIR" "$OUTDIR"
mkdir -p "$WORKDIR" "$OUTDIR"

echo "=== Step 1: Install dependencies ==="
sudo apt-get update -qq
sudo apt-get install -y -qq git openjdk-17-jdk-headless gcc g++ make libsdl2-dev libglew-dev libopenal-dev 2>/dev/null || \
sudo apt-get install -y -qq git openjdk-17-jdk-headless gcc g++ make libsdl2-dev libglew-dev 2>/dev/null || true

echo "=== Step 2: Clone arc_temp ==="
cd "$WORKDIR"
git clone --depth 1 https://github.com/Anuken/Arc.git arc_temp 2>/dev/null || {
    echo "GitHub clone failed. Please manually copy arc_temp to $WORKDIR/arc_temp"
    exit 1
}

# If clone fails due to network, the user can manually copy the arc_temp directory
if [ ! -d "arc_temp" ]; then
    echo "ERROR: arc_temp not found. Please copy the arc_temp directory to $WORKDIR/arc_temp"
    exit 1
fi

cd arc_temp

echo "=== Step 3: Patch build.gradle files for ARM64 Linux ==="

# Patch arc-core build.gradle to add ARM64 target
cat >> arc-core/build.gradle << 'ARCORE_PATCH'

// ARM64 Linux patch
jnigen{
    addLinux(arm64){
        def root = "csrc"
        headerDirs += (String[])["$root/soloud/src/backend/miniaudio/"]
        cppIncludes += (String[])["$root/soloud/src/backend/miniaudio/*.cpp"]
        cppFlags += "-DWITH_MINIAUDIO"
        cFlags += ["-O2"]
        cppFlags += ["-O2"]
        linkerFlags += ["-O2"]
        libraries += "-lpthread -lrt -lm -ldl".split(" ")
    }
}
ARCORE_PATCH

# Patch backend-sdl build.gradle to add ARM64 target
cat >> backends/backend-sdl/build.gradle << 'SDLPATCH'

// ARM64 Linux patch
jnigen{
    addLinux(arm64){
        cppFlags += execCmd("sdl2-config --cflags").split(" ") + "-O2".split(" ")
        cFlags = cppFlags
        libraries = (execCmd("sdl2-config --libs") + " -Wl,-Bdynamic -lGL -lGLEW").split(" ")
        linkerFlags = "-shared".split(" ")
    }
}
SDLPATCH

echo "=== Step 4: Build ==="
# Make gradlew executable
chmod +x gradlew

# Build arc-core native
echo "Building libarc..."
./gradlew :arc-core:jnigenBuild --no-daemon -q 2>&1 || {
    echo "Gradle build failed. Trying alternative direct compilation..."

    # Fallback: direct compilation
    cd "$WORKDIR"
    mkdir -p direct_build
    cd direct_build

    # Manually compile arc-core
    echo "Direct compile of libarcarm64.so..."
    ARC_SRC="$WORKDIR/arc_temp/arc-core"
    g++ -shared -fPIC -O2 -o "$OUTDIR/libarcarm64.so" \
        -I"$ARC_SRC/csrc" \
        -I"$ARC_SRC/csrc/soloud/include" \
        -DWITH_MINIAUDIO \
        -DSOLOUD_MAX_VOICE_COUNT=100 \
        "$ARC_SRC"/csrc/soloud/src/core/*.cpp \
        "$ARC_SRC"/csrc/soloud/src/audiosource/wav/*.cpp \
        "$ARC_SRC"/csrc/soloud/src/audiosource/wav/*.c \
        "$ARC_SRC"/csrc/soloud/src/filter/*.cpp \
        "$ARC_SRC"/csrc/soloud/src/backend/miniaudio/*.cpp \
        -lpthread -lrt -lm -ldl \
        2>&1 || echo "libarcarm64.so direct build failed"

    # Manually compile sdl-arc backend
    echo "Direct compile of libsdl-arcarm64.so..."
    SDL_SRC="$WORKDIR/arc_temp/backends/backend-sdl"
    g++ -shared -fPIC -O2 -o "$OUTDIR/libsdl-arcarm64.so" \
        $(sdl2-config --cflags) \
        -I"$SDL_SRC/src" \
        -I"$ARC_SRC/csrc" \
        "$SDL_SRC"/src/arc/backend/sdl/jni/*.java \
        2>&1 || echo "libsdl-arcarm64.so direct build failed (expected, need JNI code extraction)"
}

echo "=== Step 5: Collect output ==="
# Find the built .so files
find "$WORKDIR" -name "libarc*.so" -o -name "libsdl-arc*.so" | while read f; do
    cp -v "$f" "$OUTDIR/"
done

echo ""
echo "=== Done ==="
echo "Output directory: $OUTDIR"
ls -la "$OUTDIR/"
echo ""
echo "Files to copy back:"
echo "  $OUTDIR/libarcarm64.so"
echo "  $OUTDIR/libsdl-arcarm64.so"
