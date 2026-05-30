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

    private val ARM64_LIBS = listOf("libsdl-arcarm64.so", "libarcarm64.so", "libarc-freetypearm64.so")
    private val SKIP_IF_PRESENT = mapOf("libsdl-arcarm64.so" to "libSDL2.so")
    private val REMOVE = listOf(".dylib", "libsdl-arc64.so", "libarc64.so", "libarc64.dylib", "libarc-freetype64", "libarc-filedialogs", "libsdl-arc64.dylib", "libsdl-arcarm64.dylib")
    private val SHADER = listOf("#version 150" to "#version 300")

    fun patchJar(context: Context, jarPath: String): Boolean {
        val jarFile = File(jarPath)
        if (!jarFile.exists()) return false

        val original = mutableSetOf<String>()
        try { ZipFile(jarFile).use { z -> val e = z.entries(); while (e.hasMoreElements()) original.add(e.nextElement().name) } } catch (e: Exception) { return false }

        val patched = File(jarFile.parent, "Mindustry_patched.jar")
        try {
            ZipFile(jarFile).use { src ->
                ZipOutputStream(FileOutputStream(patched)).use { dst ->
                    val entries = src.entries()
                    while (entries.hasMoreElements()) {
                        val e = entries.nextElement(); val n = e.name
                        if (REMOVE.any { n.contains(it) }) continue
                        if (n == "arc/graphics/gl/Shader.class") {
                            dst.putNextEntry(ZipEntry(n))
                            val bytes = src.getInputStream(e).use { it.readBytes() }
                            for ((from, to) in SHADER) {
                                val fb = from.toByteArray(Charsets.UTF_8)
                                val tb = to.toByteArray(Charsets.UTF_8)
                                var i = 0
                                while (i <= bytes.size - fb.size) {
                                    if (bytes.sliceArray(i until i + fb.size).contentEquals(fb)) { tb.copyInto(bytes, i); i += tb.size } else i++
                                }
                            }
                            dst.write(bytes)
                        } else { dst.putNextEntry(ZipEntry(n)); src.getInputStream(e).use { it.copyTo(dst) } }
                    }
                    for (lib in ARM64_LIBS) {
                        if (original.contains(lib)) continue
                        val alt = SKIP_IF_PRESENT[lib]
                        if (alt != null && original.contains(alt)) continue
                        val bytes = context.assets.open("components/patch/$lib").use { it.readBytes() }
                        dst.putNextEntry(ZipEntry(lib)); dst.write(bytes)
                    }
                }
            }
            jarFile.delete(); patched.renameTo(jarFile)
            return true
        } catch (e: Exception) { patched.delete(); return false }
    }
}
