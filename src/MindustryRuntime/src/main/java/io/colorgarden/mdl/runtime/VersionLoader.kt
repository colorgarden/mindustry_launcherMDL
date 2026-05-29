package io.colorgarden.mdl.runtime

import android.content.Context
import android.util.Log
import dalvik.system.DexClassLoader
import java.io.File

/**
 * Loads version-specific Mindustry core.jar via DexClassLoader isolation.
 * Each game version gets its own ClassLoader instance, allowing multiple
 * incompatible versions to coexist without class conflicts.
 */
object VersionLoader {

    private const val TAG = "MDL_VersionLoader"

    /** Map of version path → ClassLoader, kept alive for the process lifetime */
    private val loaders = mutableMapOf<String, DexClassLoader>()

    /**
     * Load a version's core.jar and return a ClassLoader that can access
     * the game classes for that specific version.
     *
     * DexClassLoader loads .dex/.jar from [dexPath] into [optimizedDir],
     * using [parent] as the parent ClassLoader (for shared Arc classes).
     */
    fun loadVersion(context: Context, versionPath: String): DexClassLoader? {
        // Return cached loader if already created
        loaders[versionPath]?.let { return it }

        val coreJar = File(versionPath, "core.jar")
        if (!coreJar.exists()) {
            Log.e(TAG, "core.jar not found at: ${coreJar.absolutePath}")
            return null
        }

        // Optimized DEX cache directory (Android requires this)
        val optimizedDir = File(versionPath, "dex_cache").also {
            if (!it.exists()) it.mkdirs()
        }

        // Native library dir (for bundled .so files within jars if any)
        val nativeLibDir = File(versionPath, "natives")

        try {
            // Use the app's own ClassLoader as parent — Arc framework classes
            // are loaded by the parent, version-specific game classes by this loader
            val classLoader = DexClassLoader(
                coreJar.absolutePath,
                optimizedDir.absolutePath,
                nativeLibDir.absolutePath,
                context.classLoader  // Parent: app ClassLoader (has Arc + natives)
            )

            loaders[versionPath] = classLoader
            Log.i(TAG, "Loaded version: $versionPath")
            return classLoader
        } catch (e: Exception) {
            Log.e(TAG, "Failed to create DexClassLoader for $versionPath", e)
            return null
        }
    }

    /**
     * Get the ClassLoader for an already-loaded version.
     * Returns null if the version hasn't been loaded yet.
     */
    fun getLoader(versionPath: String): DexClassLoader? = loaders[versionPath]

    /**
     * Release a version's ClassLoader (e.g., when the version is deleted).
     */
    fun unloadVersion(versionPath: String) {
        loaders.remove(versionPath)
        Log.i(TAG, "Unloaded version: $versionPath")
    }

    /**
     * Get the data directory for a version (saves, mods, settings).
     * Respects the isolation setting from version config.
     */
    fun getDataDir(versionPath: String, isolated: Boolean, context: Context): File {
        return if (isolated) {
            File(versionPath, "data")
        } else {
            // Shared Mindustry app data (Android's standard location)
            File(context.getExternalFilesDir(null)?.parentFile, "Mindustry")
        }
    }

    /**
     * Ensure all necessary subdirectories exist for a version.
     */
    fun ensureDirectories(versionPath: String, dataDir: File) {
        listOf(
            File(dataDir, "mods"),
            File(dataDir, "saves"),
            File(dataDir, "schematics"),
            File(dataDir, "exports"),
            File(dataDir, "crashes")
        ).forEach { if (!it.exists()) it.mkdirs() }
    }
}
