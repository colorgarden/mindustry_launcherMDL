package io.colorgarden.mdl.service

import android.content.Context
import android.util.Log
import java.io.File
import java.io.FileOutputStream
import java.util.zip.ZipEntry
import java.util.zip.ZipFile
import java.util.zip.ZipOutputStream

object JarPatcher {
    private const val TAG = "JarPatcher"

    /** ARM64 .so files to inject into the jar */
    private val ARM64_LIBS = listOf(
        "libsdl-arcarm64.so",
        "libarcarm64.so",
        "libarc-freetypearm64.so",
        "libsdl_new.so"
    )

    /** Byte-level Shader patch: change GLSL version for mobile GPU
     *  Original: #version 330 es → Patched: #version 300 es */
    private data class BytePatch(val offset: Int, val from: Byte, val to: Byte)
    private val SHADER_PATCHES = listOf(
        BytePatch(2331, 0x35, 0x30),  // '5' → '0' in version string
        BytePatch(8661, 0x99.toByte(), 0x9A.toByte())
    )

    /** x86/non-ARM patterns to remove from jar */
    private val REMOVE_PATTERNS = listOf(
        ".dylib",           // macOS libs
        "libsdl-arc64.so",  // x86-64 SDL
        "libarc64.so",
        "libarc64.dylib",
        "libarc-freetype64",
        "libarc-filedialogs",
        "libarcarm64.dylib",
        "libsdl-arc64.dylib",
        "libsdl-arcarm64.dylib"
    )

    /**
     * Patch a downloaded Mindustry jar for ARM64 Android.
     * @param context for accessing bundled patch assets
     * @param jarPath path to the downloaded jar file
     */
    fun patchJar(context: Context, jarPath: String): Boolean {
        Log.i(TAG, "Patching jar: $jarPath")
        val jarFile = File(jarPath)
        if (!jarFile.exists()) { Log.e(TAG, "Jar not found"); return false }

        val patchedJar = File(jarFile.parent, "Mindustry_patched.jar")
        var success = false

        try {
            ZipFile(jarFile).use { source ->
                ZipOutputStream(FileOutputStream(patchedJar)).use { zos ->
                    // Copy entries, skipping non-ARM libs
                    val entries = source.entries()
                    val copied = mutableSetOf<String>()

                    while (entries.hasMoreElements()) {
                        val entry = entries.nextElement()
                        val name = entry.name

                        // Skip non-ARM native libs
                        if (REMOVE_PATTERNS.any { name.contains(it) }) {
                            Log.d(TAG, "Removing: $name")
                            continue
                        }

                        // Process Shader.class with byte-level patches
                        if (name == "arc/graphics/gl/Shader.class") {
                            zos.putNextEntry(ZipEntry(name))
                            val bytes = source.getInputStream(entry).use { it.readBytes() }
                            for (p in SHADER_PATCHES) {
                                if (p.offset < bytes.size && bytes[p.offset] == p.from) {
                                    bytes[p.offset] = p.to
                                    Log.d(TAG, "Shader patch @${p.offset}: ${p.from.toString(16)}→${p.to.toString(16)}")
                                }
                            }
                            zos.write(bytes)
                        // Replace ARM64 libs with our prebuilt versions
                        } else if (ARM64_LIBS.contains(name)) {
                            val patchEntry = ZipEntry(name)
                            zos.putNextEntry(patchEntry)
                            val patchBytes = loadPatchAsset(context, name)
                            if (patchBytes != null) {
                                zos.write(patchBytes)
                                Log.d(TAG, "Replaced: $name (${patchBytes.size} bytes)")
                            } else {
                                source.getInputStream(entry).use { it.copyTo(zos) }
                                Log.d(TAG, "Kept original: $name (no patch)")
                            }
                            copied.add(name)
                        } else {
                            zos.putNextEntry(ZipEntry(name))
                            source.getInputStream(entry).use { it.copyTo(zos) }
                        }
                    }

                    // Inject any ARM64 libs not already in the jar
                    for (lib in ARM64_LIBS) {
                        if (!copied.contains(lib)) {
                            val bytes = loadPatchAsset(context, lib) ?: continue
                            zos.putNextEntry(ZipEntry(lib))
                            zos.write(bytes)
                            Log.d(TAG, "Injected: $lib (${bytes.size} bytes)")
                        }
                    }
                }
            }

            // Replace original with patched
            jarFile.delete()
            patchedJar.renameTo(jarFile)
            success = true
            Log.i(TAG, "Patch complete!")
        } catch (e: Exception) {
            Log.e(TAG, "Patch failed", e)
            patchedJar.delete()
        }

        return success
    }

    private fun loadPatchAsset(context: Context, name: String): ByteArray? {
        return try {
            context.assets.open("components/patch/$name").use { it.readBytes() }
        } catch (e: Exception) {
            null
        }
    }
}
