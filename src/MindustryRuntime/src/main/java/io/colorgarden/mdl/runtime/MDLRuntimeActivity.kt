package io.colorgarden.mdl.runtime

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.content.IntentFilter
import android.os.Bundle
import android.util.Log
import android.view.KeyEvent
import android.view.View
import android.widget.FrameLayout
import android.widget.TextView
import androidx.localbroadcastmanager.content.LocalBroadcastManager
import arc.ApplicationListener
import arc.Core
import arc.backend.android.AndroidApplication
import arc.backend.android.AndroidApplicationConfiguration
import java.io.File

/**
 * Android Activity that hosts a Mindustry game instance.
 *
 * Architecture:
 * - Extends Arc's [AndroidApplication] for EGL/GLES rendering + touch input
 * - Loads version-specific core DEX via [VersionLoader]
 * - Reflectively instantiates the game's ApplicationListener
 * - Crashing / exiting sends broadcast back to MAUI launcher
 *
 * Intent extras expected from MAUI:
 * - "version_path": absolute path to the version directory
 * - "version_name": display name (used in crash reports)
 * - "isolated": boolean, whether data isolation is enabled
 */
class MDLRuntimeActivity : AndroidApplication() {

    companion object {
        private const val TAG = "MDL_Runtime"
        const val ACTION_GAME_EXITED = "io.colorgarden.mdl.GAME_EXITED"
        const val EXTRA_VERSION_PATH = "version_path"
        const val EXTRA_VERSION_NAME = "version_name"
        const val EXTRA_ISOLATED = "isolated"
    }

    private lateinit var versionName: String
    private lateinit var versionPath: String
    private var isolated = true
    private var crashed = false

    // Error overlay for crash display before broadcast
    private var errorView: TextView? = null

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

        // Extract version info from Intent
        versionName = intent.getStringExtra(EXTRA_VERSION_NAME) ?: "Unknown"
        versionPath = intent.getStringExtra(EXTRA_VERSION_PATH) ?: ""
        isolated = intent.getBooleanExtra(EXTRA_ISOLATED, true)

        if (versionPath.isEmpty()) {
            reportError("version_path extra is empty — cannot launch")
            return
        }

        Log.i(TAG, "Launching: $versionName at $versionPath (isolated=$isolated)")

        // Set up uncaught exception handler
        Thread.setDefaultUncaughtExceptionHandler { thread, ex ->
            Log.e(TAG, "Uncaught exception in thread ${thread.name}", ex)
            crashed = true
            val msg = ex.message ?: "Unknown error"
            val stack = ex.stackTraceToString()
            reportError("$versionName crashed:\n$msg\n\n$stack")
        }

        // Prepare data directories
        val dataDir = VersionLoader.getDataDir(versionPath, isolated, this)
        VersionLoader.ensureDirectories(versionPath, dataDir)

        // Register broadcast receiver for MAUI → game communication
        LocalBroadcastManager.getInstance(this)
            .registerReceiver(mauiReceiver, IntentFilter("io.colorgarden.mdl.TO_GAME"))
    }

    /**
     * Called by Arc framework after GLSurfaceView is ready.
     * Override to inject version-specific game code.
     */
    override fun initialize(): View? {
        // Try to load version-specific game entry point
        val classLoader = VersionLoader.loadVersion(this, versionPath)
        if (classLoader == null) {
            runOnUiThread {
                reportError("Failed to load version: $versionName\ncore.dex not found or corrupted")
            }
            return null
        }

        // Set the version ClassLoader as context ClassLoader for reflection
        Thread.currentThread().contextClassLoader = classLoader

        // Attempt to find and instantiate the game ApplicationListener
        val listener = tryCreateListener(classLoader)
        if (listener != null) {
            addListener(listener)
            Log.i(TAG, "Game ApplicationListener successfully created for $versionName")
        } else {
            runOnUiThread {
                reportError(
                    "Could not find game entry point for $versionName.\n" +
                    "The version may not be compiled for Android."
                )
            }
        }

        return super.initialize()
    }

    /**
     * Try multiple known class names for the ApplicationListener.
     * Different Mindustry versions may use different entry points.
     */
    private fun tryCreateListener(classLoader: ClassLoader): ApplicationListener? {
        val candidates = listOf(
            "mindustry.ClientLauncher",
            "mindustry.core.ClientLauncher",
            "mindustry.android.AndroidLauncher",
        )

        for (fqcn in candidates) {
            try {
                val clazz = classLoader.loadClass(fqcn)
                // Check if it implements ApplicationListener
                if (ApplicationListener::class.java.isAssignableFrom(clazz)) {
                    val instance = clazz.getDeclaredConstructor().newInstance()
                    return instance as ApplicationListener
                }
            } catch (e: ClassNotFoundException) {
                // Expected for most candidates — try next one
            } catch (e: Exception) {
                Log.w(TAG, "Found class $fqcn but failed to instantiate: ${e.message}")
            }
        }

        Log.e(TAG, "No ApplicationListener found in version $versionName")
        return null
    }

    override fun dispose() {
        super.dispose()
        notifyLauncher()
    }

    override fun onDestroy() {
        LocalBroadcastManager.getInstance(this).unregisterReceiver(mauiReceiver)
        super.onDestroy()
        notifyLauncher()
    }

    override fun onKeyDown(keyCode: Int, event: KeyEvent): Boolean {
        // Back button → notify launcher
        if (keyCode == KeyEvent.KEYCODE_BACK) {
            notifyLauncher()
        }
        return super.onKeyDown(keyCode, event)
    }

    // ─── Error / Crash Handling ──────────────────────────

    /**
     * Show an in-activity error overlay and broadcast to MAUI.
     * The activity will finish after a short delay.
     */
    private fun reportError(message: String) {
        Log.e(TAG, message)
        try {
            // Show error text on screen
            val errorView = TextView(this).apply {
                text = "MDL - Game Error\n\n$message"
                textSize = 14f
                setBackgroundColor(0xCC000000.toInt())
                setTextColor(0xFFFFFFFF.toInt())
                setPadding(32, 32, 32, 32)
                isFocusable = true
                isClickable = true
                setOnClickListener { finish() }
            }
            addContentView(errorView, FrameLayout.LayoutParams(
                FrameLayout.LayoutParams.MATCH_PARENT,
                FrameLayout.LayoutParams.MATCH_PARENT
            ))
        } catch (_: Exception) {}

        notifyLauncher(true)
    }

    /**
     * Notify the MAUI launcher that the game has exited.
     * @param error true if the game crashed or failed to start
     */
    private fun notifyLauncher(error: Boolean = false) {
        val intent = Intent(ACTION_GAME_EXITED).apply {
            putExtra("version_path", versionPath)
            putExtra("version_name", versionName)
            putExtra("error", error || crashed)
        }
        try {
            LocalBroadcastManager.getInstance(this).sendBroadcast(intent)
        } catch (_: Exception) {}
    }

    // ─── MAUI → Game Communication ───────────────────────

    private val mauiReceiver = object : BroadcastReceiver() {
        override fun onReceive(context: Context, intent: Intent) {
            when (intent.action) {
                "io.colorgarden.mdl.TO_GAME" -> {
                    val command = intent.getStringExtra("command") ?: return
                    when (command) {
                        "exit" -> {
                            // Gracefully exit the game
                            post { exit() }
                        }
                        "pause" -> {
                            // The game should be paused (e.g., user switched away)
                            Core.app.post { Core.app.getListeners().forEach { it.pause() } }
                        }
                    }
                }
            }
        }
    }
}
