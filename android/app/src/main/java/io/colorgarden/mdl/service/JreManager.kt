package io.colorgarden.mdl.service

import android.content.Context
import android.util.Log
import java.io.File
import java.io.FileOutputStream
import java.util.zip.ZipFile

object JreManager {
    private const val TAG = "JreManager"

    fun getJreDir(context: Context, version: Int = 25): String =
        "${context.filesDir.absolutePath}/runtimes/jre-$version"

    fun isInstalled(context: Context, version: Int = 25): Boolean {
        val jre = File(getJreDir(context, version))
        if (!jre.isDirectory) return false
        val jliPaths = if (version <= 8) arrayOf(
            "lib/aarch64/jli/libjli.so", "lib/arm/jli/libjli.so"
        ) else arrayOf("lib/libjli.so")
        return jliPaths.any { File(jre, it).exists() }
    }

    /** Detect JRE version from installed directory */
    fun detectVersion(context: Context): Int? {
        for (v in listOf(25, 21, 17, 8)) {
            if (isInstalled(context, v)) return v
        }
        return null
    }

    fun getJavaHome(context: Context, version: Int = 25): String = getJreDir(context, version)

    /**
     * Unpack .pack files to .jar + postPrepare (matching ZL2's RuntimesManager).
     */
    fun prepareJre(context: Context): Boolean {
        val jreDir = File(getJreDir(context))
        val nativeLibDir = context.applicationInfo.nativeLibraryDir

        // Step 1: unpack200
        val packFiles = jreDir.walkTopDown().filter { it.extension == "pack" }.toList()
        if (packFiles.isEmpty()) {
            Log.i(TAG, "No .pack files to unpack")
            return true // nothing to do, but not an error
        }
        Log.i(TAG, "Unpacking ${packFiles.size} .pack files...")

        // Copy libunpack200.so to cache dir and make executable
        val workDir = context.cacheDir
        val unpackBin = File(workDir, "libunpack200.so")
        try {
            File(nativeLibDir, "libunpack200.so").copyTo(unpackBin, overwrite = true)
            unpackBin.setExecutable(true, false)
        } catch (e: Exception) {
            Log.e(TAG, "Failed to copy libunpack200.so", e)
            return false
        }

        for (packFile in packFiles) {
            val destPath = packFile.absolutePath.removeSuffix(".pack")
            if (File(destPath).exists() && File(destPath).length() > 0) continue
            try {
                // Use linker64 to execute the shared library (SELinux W^X workaround)
                val p = ProcessBuilder()
                    .command("/system/bin/linker64", unpackBin.absolutePath, "-r", packFile.absolutePath, destPath)
                    .start()
                val exitCode = p.waitFor()
                if (exitCode != 0) {
                    val err = p.errorStream.bufferedReader().readText()
                    Log.e(TAG, "unpack200 failed for ${packFile.name}: exit=$exitCode err=$err")
                } else {
                    Log.d(TAG, "Unpacked: ${packFile.name}")
                }
            } catch (e: Exception) {
                Log.e(TAG, "unpack200 error for ${packFile.name}", e)
            }
        }
        // Verify rt.jar was created
        if (!File(jreDir, "lib/rt.jar").exists()) {
            Log.e(TAG, "unpack200: rt.jar still missing after unpack attempt")
            return false
        }
        Log.i(TAG, "unpack200 done, rt.jar exists")

        // Step 2: postPrepare (matching ZL2's RuntimesManager.postPrepare)
        val arch = "aarch64"
        val libDir = File(jreDir, "lib/$arch")
        // Rename libfreetype.so.6 -> libfreetype.so (if exists)
        val ft6 = File(libDir, "libfreetype.so.6")
        val ftOut = File(libDir, "libfreetype.so")
        if (ft6.exists() && (!ftOut.exists() || ft6.length() != ftOut.length())) {
            ft6.renameTo(ftOut)
        }
        // Copy libawt_xawt.so from native lib dir to JRE
        val localXawt = File(nativeLibDir, "libawt_xawt.so")
        val targetXawt = File(libDir, "libawt_xawt.so")
        if (localXawt.exists() && !targetXawt.exists()) {
            localXawt.copyTo(targetXawt)
        }
        Log.i(TAG, "postPrepare done")
        return true
    }

    fun installFromAsset(context: Context): Boolean {
        val jreDir = getJreDir(context)
        val parentDir = File(jreDir).parentFile ?: return false
        if (!parentDir.exists()) parentDir.mkdirs()

        val zipFile = File(parentDir, "jre-8.zip")
        try {
            context.assets.open("runtimes/jre-8.zip").use { input ->
                FileOutputStream(zipFile).use { output -> input.copyTo(output) }
            }
        } catch (e: Exception) {
            Log.e(TAG, "Failed to copy JRE asset", e)
            return false
        }

        try {
            ZipFile(zipFile).use { zip ->
                val entries = zip.entries()
                while (entries.hasMoreElements()) {
                    val entry = entries.nextElement()
                    val target = File(parentDir, entry.name)
                    if (entry.isDirectory) target.mkdirs()
                    else {
                        target.parentFile?.mkdirs()
                        zip.getInputStream(entry).use { zi ->
                            FileOutputStream(target).use { fo -> zi.copyTo(fo) }
                        }
                    }
                }
            }
            zipFile.delete()
            prepareJre(context)
            Log.i(TAG, "JRE installed to $jreDir")
            return isInstalled(context)
        } catch (e: Exception) {
            Log.e(TAG, "Failed to extract JRE", e)
            return false
        }
    }
}
