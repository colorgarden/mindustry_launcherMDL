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
import com.google.gson.Gson
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

    private var surfaceView: SurfaceView? = null
    private var overlayView: android.view.View? = null
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
        try { System.loadLibrary("preload_touch") } catch (_: Exception) {}
        try { System.loadLibrary("pojavexec") } catch (_: Exception) {}
        try { System.loadLibrary("pojavexec_awt") } catch (_: Exception) {}
        try { System.loadLibrary("exithook") } catch (_: Exception) {}
        try { System.loadLibrary("mdl_window") } catch (_: Exception) {}
        try { System.loadLibrary("sdl-arcarm64") } catch (_: Exception) {}
        dlog("=== PojavRuntime onCreate START ===")
        val gameJar = intent.getStringExtra("game_jar") ?: run { finish(); return }
        val lwjglJar = intent.getStringExtra("lwjgl_jar") ?: run { finish(); return }
        val gameDir = intent.getStringExtra("game_dir") ?: run { finish(); return }
        dlog("gameJar=$gameJar")

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
        if (displayH > displayW) { val tmp = displayW; displayW = displayH; displayH = tmp }
        if (displayW % 2 != 0) displayW--
        if (displayH % 2 != 0) displayH--
        dlog("Display resolution (landscape): ${displayW}x${displayH}")

        java.io.File("/sdcard/MDL").mkdirs()
        java.io.File("/sdcard/MDL/display_config.txt").writeText("width=$displayW\nheight=$displayH\n")
        loadConfig()

        window.addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON)
        surfaceView = SurfaceView(this).apply {
            holder.addCallback(object : SurfaceHolder.Callback {
                override fun surfaceCreated(h: SurfaceHolder) {
                    dlog("surfaceCreated")
                    h.setFixedSize(displayW, displayH)
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
        }

        // Overlay for joystick visual indicator + toggle button
        val overlay = object : android.view.View(this) {
            private val paint = android.graphics.Paint().apply { isAntiAlias = true; style = android.graphics.Paint.Style.FILL }
            private val sp = android.graphics.Paint().apply { isAntiAlias = true; style = android.graphics.Paint.Style.STROKE; strokeWidth = 3f; color = 0xAAFFFFFF.toInt() }
            private val tp = android.graphics.Paint().apply { isAntiAlias = true; textSize = 24f; color = 0xFFFFFFFF.toInt(); textAlign = android.graphics.Paint.Align.CENTER }
            override fun onDraw(canvas: android.graphics.Canvas) {
                super.onDraw(canvas)
                val w = width.toFloat(); val h = height.toFloat()
                // Toggle button
                paint.color = if (joystickEnabled) 0x8800AA00.toInt() else 0x88AA0000.toInt()
                canvas.drawRoundRect(w - 90f, h - 90f, w - 10f, h - 10f, 12f, 12f, paint)
                canvas.drawRoundRect(w - 90f, h - 90f, w - 10f, h - 10f, 12f, 12f, sp)
                canvas.drawText(if (joystickEnabled) "ON" else "OFF", w - 50f, h - 35f, tp)
                // Joystick area (fixed circle, lower-left)
                if (joystickEnabled) {
                    val jcx = 350f; val jcy = h - 400f  // fixed joystick center
                    val cvp = 240f  // joystick circle radius
                    paint.color = 0x22666666.toInt()
                    canvas.drawCircle(jcx, jcy, cvp, paint)
                    sp.color = if (joystickPointerId >= 0) 0xCC00AAFF.toInt() else 0x88FFFFFF.toInt()
                    canvas.drawCircle(jcx, jcy, cvp, sp)
                }
                // Joystick finger indicator
                if (joystickPointerId >= 0) {
                    val fx = joystickCurX * w / displayW
                    val fy = joystickCurY * h / displayH
                    paint.color = 0x6600AAFF.toInt()
                    canvas.drawCircle(fx, fy, 20f, paint)
                }
                // Custom buttons
                for ((i, btn) in this@PojavRuntime.config.buttons.withIndex()) {
                    val bx = btn.x * w; val by = btn.y * h
                    val bw = btn.width * w; val bh = btn.height * h
                    val isRmbToggle = btn.action is ControlAction.MouseRight
                    val isBtnActive = if (isRmbToggle) rightClickMode else (controlButtonDown == i)
                    val baseColor = if (isRmbToggle && rightClickMode) 0xCCFF4444.toInt() else btn.color
                    paint.color = if (isBtnActive) (baseColor or 0x88000000.toInt()) else baseColor
                    canvas.drawRoundRect(bx, by, bx + bw, by + bh, 10f, 10f, paint)
                    canvas.drawRoundRect(bx, by, bx + bw, by + bh, 10f, 10f, sp)
                    tp.textSize = bh * 0.4f
                    canvas.drawText(btn.label, bx + bw / 2, by + bh * 0.65f, tp)
                }
            }
        }
        overlay.setOnTouchListener { _, e -> handleTouch(e); true }
        overlayView = overlay

        val frame = android.widget.FrameLayout(this)
        frame.addView(surfaceView)
        frame.addView(overlay)
        setContentView(frame)
        dlog("=== PojavRuntime onCreate DONE ===")
    }

    private fun startGame(gameJar: String, lwjglJar: String, gameDir: String) {
        gameJob = gameScope.launch(Dispatchers.Default) {
            try {
                dlog("MindustryJVM: launching")
                MindustryLauncher.launch(application, gameJar, lwjglJar, gameDir, displayW, displayH)
                dlog("MindustryJVM: Game exited")
            } catch (e: Exception) {
                dlog("MindustryJVM: CRASH - ${e.javaClass.name}: ${e.message}")
            }
            withContext(Dispatchers.Main) { finish() }
        }
    }

    private var touchCount = 0
    private var stackModeSet = false
    private val touchFile = File("/sdcard/sdl_touch.dat")

    private var joystickPointerId = -1
    private var joystickCenterX = 0f
    private var joystickCenterY = 0f
    private var joystickCurX = 0f
    private var joystickCurY = 0f
    private var joystickActiveKeys = mutableSetOf<Int>()
    private val joystickDeadZone = 15f
    private var joystickEnabled = true
    private var toggleBtnDown = false
    private var rightClickMode = false
    private var touchDownActive = false  // track if touch button is currently pressed
    private var config: ControlButtonConfig = ControlButtonConfig()
    private var controlButtonDown: Int = -1  // which custom button is pressed, -1=none

    private val keysFile = File("/sdcard/sdl_keys.dat")
    private val keyEventBuf = mutableListOf<Byte>()
    private val configFile = File("/sdcard/MDL/controls.json")
    private val gson = Gson()

    private fun loadConfig() {
        try {
            if (!configFile.exists()) saveDefaultConfig()
            config = gson.fromJson(configFile.readText(), ControlButtonConfig::class.java)
            dlog("Loaded config: ${config.buttons.size} buttons")
        } catch (e: Exception) {
            dlog("Config load failed: ${e.message}, using default")
            saveDefaultConfig()
        }
    }

    private fun saveDefaultConfig() {
        val defaults = ControlButtonConfig()
        try { configFile.writeText(gson.toJson(defaults)) } catch (_: Exception) {}
        config = defaults
    }

    private val buttonFile = File("/sdcard/sdl_button.dat")

    private fun executeButtonAction(btn: ControlButton, down: Boolean) {
        try {
            when (btn.action) {
                is ControlAction.MouseLeft, is ControlAction.MouseRight -> {
                    val cx = ((btn.x + btn.width / 2) * displayW).toInt().coerceIn(0, displayW - 1)
                    val cy = ((btn.y + btn.height / 2) * displayH).toInt().coerceIn(0, displayH - 1)
                    val button = if (btn.action is ControlAction.MouseRight) 3.toByte() else 1.toByte()
                    // Format: action(1) + x(2 big-endian) + y(2 big-endian) + button(1) = 6 bytes
                    buttonFile.appendBytes(byteArrayOf(
                        (if (down) 0 else 1).toByte(),
                        ((cx shr 8) and 0xFF).toByte(), (cx and 0xFF).toByte(),
                        ((cy shr 8) and 0xFF).toByte(), (cy and 0xFF).toByte(),
                        button
                    ))
                }
                is ControlAction.KeyPress -> {
                    val kp = btn.action as ControlAction.KeyPress
                    if (down) keysFile.appendBytes(byteArrayOf(0, kp.sdlScancode.toByte()))
                    else keysFile.appendBytes(byteArrayOf(1, kp.sdlScancode.toByte()))
                }
            }
        } catch (_: Exception) {}
    }
    // SDL scancodes: W=26, A=4, S=22, D=7
    private fun flushKeyBuf() {
        if (keyEventBuf.isEmpty()) return
        try { keysFile.appendBytes(keyEventBuf.toByteArray()) } catch (_: Exception) {}
        keyEventBuf.clear()
    }

    private fun sendJoystickKey(keycode: Int, down: Boolean) {
        val scancode: Byte = when (keycode) {
            87 -> 26; 83 -> 22; 65 -> 4; 68 -> 7; else -> return
        }
        keyEventBuf.add((if (down) 0 else 1).toByte())
        keyEventBuf.add(scancode)
    }

    private fun updateJoystickKeys(dx: Float, dy: Float) {
        val newKeys = mutableSetOf<Int>()
        if (dy < -joystickDeadZone) newKeys.add(87)
        if (dy > joystickDeadZone) newKeys.add(83)
        if (dx < -joystickDeadZone) newKeys.add(65)
        if (dx > joystickDeadZone) newKeys.add(68)
        for (k in joystickActiveKeys - newKeys) sendJoystickKey(k, false)
        for (k in newKeys - joystickActiveKeys) sendJoystickKey(k, true)
        joystickActiveKeys = newKeys
        flushKeyBuf()
        overlayView?.invalidate()
    }

    private fun releaseJoystickKeys() {
        for (k in joystickActiveKeys) sendJoystickKey(k, false)
        joystickActiveKeys.clear()
        joystickPointerId = -1
        flushKeyBuf()
        overlayView?.invalidate()
    }

    private fun handleTouch(e: MotionEvent) {
        val action = e.actionMasked
        val view = surfaceView ?: return
        val vw = view.width.toFloat(); val vh = view.height.toFloat()
        if (vw <= 0 || vh <= 0) return

        // === Toggle button FIRST ===
        val pi = if (action == MotionEvent.ACTION_MOVE) 0 else e.actionIndex
        val ex = e.getX(pi); val ey = e.getY(pi)
        val btnX = vw - 100f; val btnY = vh - 100f
        if (action == MotionEvent.ACTION_DOWN && ex > btnX && ey > btnY) { toggleBtnDown = true; overlayView?.invalidate(); return }
        if (toggleBtnDown && action == MotionEvent.ACTION_UP && ex > btnX && ey > btnY) {
            joystickEnabled = !joystickEnabled; toggleBtnDown = false; overlayView?.invalidate(); dlog("Joystick: $joystickEnabled")
            if (joystickEnabled) releaseJoystickKeys()
            return
        }
        if (action == MotionEvent.ACTION_UP) toggleBtnDown = false

        // === Custom buttons ===
        if (action == MotionEvent.ACTION_DOWN || action == MotionEvent.ACTION_POINTER_DOWN) {
            val idx = e.actionIndex; val bx = e.getX(idx); val by = e.getY(idx)
            for ((i, btn) in config.buttons.withIndex()) {
                if (bx >= btn.x * vw && bx <= (btn.x + btn.width) * vw && by >= btn.y * vh && by <= (btn.y + btn.height) * vh) {
                    controlButtonDown = i
                    if (btn.action !is ControlAction.MouseRight) executeButtonAction(btn, true)
                    overlayView?.invalidate(); return
                }
            }
        }
        if ((action == MotionEvent.ACTION_UP || action == MotionEvent.ACTION_POINTER_UP) && controlButtonDown >= 0) {
            val idx = e.actionIndex; val bx = e.getX(idx); val by = e.getY(idx)
            val btn = config.buttons[controlButtonDown]
            // Only process UP if it's on the same button
            if (bx >= btn.x * vw && bx <= (btn.x + btn.width) * vw && by >= btn.y * vh && by <= (btn.y + btn.height) * vh) {
                if (btn.action is ControlAction.MouseRight) {
                    rightClickMode = !rightClickMode; dlog("RightClickMode: $rightClickMode")
                } else {
                    executeButtonAction(btn, false)
                }
                controlButtonDown = -1; overlayView?.invalidate(); return
            }
        }

        // === Joystick ===
        if (joystickEnabled) {
            when (action) {
                MotionEvent.ACTION_DOWN, MotionEvent.ACTION_POINTER_DOWN -> {
                    val idx = e.actionIndex; val x = e.getX(idx); val y = e.getY(idx)
                    // Fixed joystick center (lower-left corner)
                    val jcx = 350f; val jcy = vh - 400f  // fixed joystick center
                    val dist = Math.sqrt(((x - jcx) * (x - jcx) + (y - jcy) * (y - jcy)).toDouble()).toFloat()
                    if (dist < 220f && joystickPointerId == -1) {
                        joystickPointerId = e.getPointerId(idx)
                        joystickCenterX = jcx; joystickCenterY = jcy
                        joystickCurX = x; joystickCurY = y; overlayView?.invalidate()
                    }
                }
                MotionEvent.ACTION_MOVE -> {
                    val jsIdx = e.findPointerIndex(joystickPointerId)
                    if (jsIdx >= 0) {
                        joystickCurX = e.getX(jsIdx); joystickCurY = e.getY(jsIdx)
                        updateJoystickKeys(joystickCurX - joystickCenterX, joystickCurY - joystickCenterY)
                        overlayView?.invalidate()
                    }
                }
                MotionEvent.ACTION_UP, MotionEvent.ACTION_POINTER_UP, MotionEvent.ACTION_CANCEL -> {
                    val pid = e.getPointerId(e.actionIndex)
                    if (pid == joystickPointerId) releaseJoystickKeys()
                    // Safety: if joystick finger is gone, release anyway
                    if (joystickPointerId >= 0 && e.findPointerIndex(joystickPointerId) < 0) releaseJoystickKeys()
                }
            }
        }

        // === Touch/Mouse ===
        // Skip if the only touch is being used as joystick
        if (e.pointerCount == 1 && joystickPointerId >= 0) return
        touchCount++
        val touchIdx = if (e.pointerCount > 0) { val j = e.findPointerIndex(joystickPointerId); if (j == 0 && e.pointerCount > 1) 1 else 0 } else 0
        if (touchIdx >= e.pointerCount) return
        val rawX = e.getX(touchIdx); val rawY = e.getY(touchIdx)
        val gx = (rawX / vw * displayW).coerceIn(0f, displayW.toFloat() - 1f)
        val gy = (rawY / vh * displayH).coerceIn(0f, displayH.toFloat() - 1f)
        if (touchCount <= 5) dlog("touch #$touchCount action=$action gx=$gx gy=$gy")

        val sdlAction: Byte = when (action) {
            MotionEvent.ACTION_DOWN, MotionEvent.ACTION_POINTER_DOWN -> 0
            MotionEvent.ACTION_MOVE -> 2
            MotionEvent.ACTION_UP, MotionEvent.ACTION_POINTER_UP, MotionEvent.ACTION_CANCEL -> 1
            else -> return
        }
        val ix = gx.toInt(); val iy = gy.toInt()
        // Prevent duplicate DOWNs, ensure UP matches
        try {
            if (rightClickMode) {
                touchFile.appendBytes(byteArrayOf(2, ((ix shr 8) and 0xFF).toByte(), (ix and 0xFF).toByte(),
                    ((iy shr 8) and 0xFF).toByte(), (iy and 0xFF).toByte()))
                if (sdlAction != 2.toByte()) {
                    buttonFile.appendBytes(byteArrayOf(
                        sdlAction, ((ix shr 8) and 0xFF).toByte(), (ix and 0xFF).toByte(),
                        ((iy shr 8) and 0xFF).toByte(), (iy and 0xFF).toByte(), 3
                    ))
                }
            } else {
                // MOVE first (positions cursor), then the actual event after
                touchFile.appendBytes(byteArrayOf(2, ((ix shr 8) and 0xFF).toByte(), (ix and 0xFF).toByte(),
                    ((iy shr 8) and 0xFF).toByte(), (iy and 0xFF).toByte()))
                if (sdlAction != 2.toByte()) {
                    touchFile.appendBytes(byteArrayOf(
                        sdlAction, ((ix shr 8) and 0xFF).toByte(), (ix and 0xFF).toByte(),
                        ((iy shr 8) and 0xFF).toByte(), (iy and 0xFF).toByte()
                    ))
                }
            }
        } catch (ex: Exception) { if (touchCount <= 3) dlog("SDL file write FAILED: ${ex.message}") }

        try {
            val stackOk = CallbackBridge.nativeSetInputReady(true)
            if (touchCount <= 3) dlog("CB: nativeSetInputReady returned stackQueue=$stackOk")
            if (!stackModeSet) { CallbackBridge.nativeSetUseInputStackQueue(true); stackModeSet = true; dlog("input: enabled stack queue mode") }
            when (action) {
                MotionEvent.ACTION_DOWN, MotionEvent.ACTION_POINTER_DOWN -> {
                    CallbackBridge.sendCursorPos(gx, gy); CallbackBridge.putMouseEvent(0, true)
                }
                MotionEvent.ACTION_MOVE -> CallbackBridge.sendCursorPos(gx, gy)
                MotionEvent.ACTION_UP, MotionEvent.ACTION_POINTER_UP, MotionEvent.ACTION_CANCEL -> {
                    CallbackBridge.sendCursorPos(gx, gy); CallbackBridge.putMouseEvent(0, false)
                }
            }
        } catch (ex: Exception) { dlog("CallbackBridge dispatch FAILED: ${ex.javaClass.name}: ${ex.message}") }
    }

    override fun onKeyDown(code: Int, e: KeyEvent): Boolean {
        ZLBridge.sendKey(e.unicodeChar.toChar(), lwjglKeycode(code)); return true
    }

    override fun onBackPressed() {}

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
