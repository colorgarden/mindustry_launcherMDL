package io.colorgarden.mdl.service

import android.app.Activity
import android.content.Context
import android.os.Build
import android.os.Bundle
import android.util.DisplayMetrics
import android.view.KeyEvent
import android.view.MotionEvent
import android.view.SurfaceHolder
import android.view.SurfaceView
import android.view.WindowManager
import com.movtery.zalithlauncher.bridge.ZLBridge
import kotlinx.coroutines.*
import org.lwjgl.glfw.CallbackBridge
import java.io.File

class PojavRuntime : Activity() {
    companion object {
        private const val TAG = "PojavRuntime"
        @JvmStatic external fun nSetSurface(surface: android.view.Surface?)
        @JvmStatic external fun nGetWindowPtr(): Long
    }

    private val touchFile = File("/sdcard/sdl_touch.dat")

    private var surfaceView: SurfaceView? = null
    private var gameJob: Job? = null
    private val debugLog = File("/sdcard/mdl_crash.log")
    private val gameScope = CoroutineScope(Dispatchers.Default + SupervisorJob())
    private var displayW = 1920
    private var displayH = 1080

    private fun dlog(msg: String) {
        android.util.Log.e(TAG, msg)
        try { debugLog.appendText("${System.currentTimeMillis()} $msg\n") } catch (_: Exception) {}
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        // Load native libraries in :game process (ZLBridge static block may not run here)
        try { System.loadLibrary("pojavexec") } catch (_: Exception) {}
        try { System.loadLibrary("pojavexec_awt") } catch (_: Exception) {}
        try { System.loadLibrary("exithook") } catch (_: Exception) {}
        try { System.loadLibrary("mdl_window") } catch (_: Exception) {}
        dlog("=== PojavRuntime onCreate START ===")
        val gameJar = intent.getStringExtra("game_jar") ?: run { finish(); return }
        val lwjglJar = intent.getStringExtra("lwjgl_jar") ?: run { finish(); return }
        val gameDir = intent.getStringExtra("game_dir") ?: run { finish(); return }
        dlog("gameJar=$gameJar")

        // Read actual display resolution (ZL2-aligned: getRealMetrics / currentWindowMetrics)
        val wm = getSystemService(Context.WINDOW_SERVICE) as WindowManager
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R) {
            val rect = wm.currentWindowMetrics.bounds
            displayW = rect.width(); displayH = rect.height()
        } else {
            val dm = DisplayMetrics()
            @Suppress("DEPRECATION")
            wm.defaultDisplay.getRealMetrics(dm)
            displayW = dm.widthPixels; displayH = dm.heightPixels
        }
        // Force landscape: ensure width >= height (phone screens report portrait by default)
        if (displayH > displayW) {
            val tmp = displayW; displayW = displayH; displayH = tmp
        }
        // Ensure even dimensions (ZL2 getDisplayFriendlyRes)
        if (displayW % 2 != 0) displayW--
        if (displayH % 2 != 0) displayH--
        dlog("Display resolution (landscape): ${displayW}x${displayH}")

        // Write actual display config for sdl2_shim's SDL_CreateWindow to read
        java.io.File("/sdcard/MDL").mkdirs()
        java.io.File("/sdcard/MDL/display_config.txt").writeText("width=$displayW\nheight=$displayH\n")
        dlog("Wrote display_config: ${displayW}x${displayH}")

        window.addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON)
        surfaceView = SurfaceView(this).apply {
            holder.addCallback(object : SurfaceHolder.Callback {
                override fun surfaceCreated(h: SurfaceHolder) {
                    dlog("surfaceCreated")
                    // Set render buffer to match actual display (ZL2: holder.setFixedSize)
                    h.setFixedSize(displayW, displayH)
                    // Store dimensions so JVM side can read them
                    CallbackBridge.windowWidth = displayW
                    CallbackBridge.windowHeight = displayH
                    CallbackBridge.physicalWidth = displayW
                    CallbackBridge.physicalHeight = displayH
                    ZLBridge.setupBridgeWindow(h.surface)
                    nSetSurface(h.surface)
                    startGame(gameJar, lwjglJar, gameDir)
                }
                override fun surfaceChanged(h: SurfaceHolder, f: Int, w: Int, h2: Int) {
                    ZLBridge.setupBridgeWindow(h.surface)
                }
                override fun surfaceDestroyed(h: SurfaceHolder) {
                    nSetSurface(null)
                    ZLBridge.releaseBridgeWindow()
                }
            })
            setOnTouchListener { _, e -> handleTouch(e); true }
            isFocusableInTouchMode = true
            requestFocus()
        }
        setContentView(surfaceView)
        dlog("=== PojavRuntime onCreate DONE ===")
    }

    private fun startGame(gameJar: String, lwjglJar: String, gameDir: String) {
        gameJob = gameScope.launch(Dispatchers.Default) {
            try {
                dlog("MindustryJVM: launching via ZL2-aligned flow")
                // JVM args carry displayW/displayH via glfwstub.windowWidth/Height system properties.
                // GLFW static block reads them → mGLFWWindowWidth/Height = display dimensions.
                // glfwCreateWindow IGNORES passed w/h and uses mGLFWWindowWidth/Height.
                // sendUpdateWindowSize only works AFTER glfwPollEvents sets isInputReady=true,
                // so we call it here (JVM just started, will poll events soon).
                MindustryLauncher.launch(application, gameJar, lwjglJar, gameDir, displayW, displayH)
                dlog("MindustryJVM: Game exited")
            } catch (e: Exception) {
                dlog("MindustryJVM: CRASH - ${e.javaClass.name}: ${e.message}")
            }
            withContext(Dispatchers.Main) { finish() }
        }
    }

    private var touchCount = 0

    private fun handleTouch(e: MotionEvent) {
        val action = e.actionMasked
        val view = surfaceView ?: return
        val vw = view.width.toFloat()
        val vh = view.height.toFloat()
        if (vw <= 0 || vh <= 0) return
        touchCount++
        if (touchCount <= 3) dlog("touch #$touchCount action=$action x=${e.x} y=${e.y} vw=$vw vh=$vh")
        val sx = e.x.toInt().coerceIn(0, vw.toInt() - 1)
        val sy = e.y.toInt().coerceIn(0, vh.toInt() - 1)
        // Write touch event to file for libsdl-arcarm64 to read
        try {
            if (!touchFile.exists()) touchFile.createNewFile()
            touchFile.appendBytes(byteArrayOf(
                (action and 0xFF).toByte(),
                ((sx shr 8) and 0xFF).toByte(), (sx and 0xFF).toByte(),
                ((sy shr 8) and 0xFF).toByte(), (sy and 0xFF).toByte()
            ))
            if (touchCount <= 3) dlog("touch written: action=$action x=$sx y=$sy size=${touchFile.length()}")
        } catch (e: Exception) { dlog("touch write failed: $e") }
    }

    override fun onKeyDown(code: Int, e: KeyEvent): Boolean {
        ZLBridge.sendKey(e.unicodeChar.toChar(), lwjglKeycode(code))
        return true
    }

    override fun onBackPressed() {
        // Don't close — game is running
    }

    private fun lwjglKeycode(code: Int) = when (code) {
        in KeyEvent.KEYCODE_A..KeyEvent.KEYCODE_Z -> code + 32
        in KeyEvent.KEYCODE_0..KeyEvent.KEYCODE_9 -> code - 1
        KeyEvent.KEYCODE_SPACE -> 32; KeyEvent.KEYCODE_ENTER -> 257; KeyEvent.KEYCODE_DEL -> 259
        KeyEvent.KEYCODE_TAB -> 258; KeyEvent.KEYCODE_ESCAPE -> 256
        KeyEvent.KEYCODE_SHIFT_LEFT -> 340; KeyEvent.KEYCODE_SHIFT_RIGHT -> 344
        KeyEvent.KEYCODE_CTRL_LEFT -> 341; KeyEvent.KEYCODE_CTRL_RIGHT -> 345
        KeyEvent.KEYCODE_ALT_LEFT -> 342; KeyEvent.KEYCODE_ALT_RIGHT -> 346
        KeyEvent.KEYCODE_DPAD_UP -> 265; KeyEvent.KEYCODE_DPAD_DOWN -> 264
        KeyEvent.KEYCODE_DPAD_LEFT -> 263; KeyEvent.KEYCODE_DPAD_RIGHT -> 262
        else -> -1
    }

    override fun onPause() { super.onPause() }
    override fun onDestroy() {
        gameJob?.cancel(); gameScope.cancel()
        ZLBridge.releaseBridgeWindow()
        super.onDestroy()
    }
}
