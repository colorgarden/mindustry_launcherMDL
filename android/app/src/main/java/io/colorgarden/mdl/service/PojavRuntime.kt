package io.colorgarden.mdl.service

import android.app.Activity
import android.os.Bundle
import android.view.KeyEvent
import android.view.MotionEvent
import android.view.SurfaceHolder
import android.view.SurfaceView
import android.view.WindowManager
import com.movtery.zalithlauncher.bridge.ZLBridge
import kotlinx.coroutines.*
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
        window.addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON)
        surfaceView = SurfaceView(this).apply {
            holder.addCallback(object : SurfaceHolder.Callback {
                override fun surfaceCreated(h: SurfaceHolder) {
                    dlog("surfaceCreated")
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
        // Match ZL2's approach: launch JVM from lifecycleScope.launch(Dispatchers.Default)
        // The Launcher.launchJvm() internally sets up everything on the calling thread
        gameJob = gameScope.launch(Dispatchers.Default) {
            try {
                dlog("MindustryJVM: launching via ZL2-aligned flow")
                MindustryLauncher.launch(application, gameJar, lwjglJar, gameDir)
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
