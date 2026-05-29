package io.colorgarden.mdl.service

import android.app.Application
import android.os.Build
import android.system.Os
import com.movtery.zalithlauncher.bridge.ZLBridge
import com.movtery.zalithlauncher.bridge.ZLNativeInvoker
import com.oracle.dalvik.VMLauncher
import java.io.File

/**
 * Minimal Mindustry launcher that mirrors ZalithLauncher2's Launcher.launchJvm() flow exactly.
 */
object MindustryLauncher {

    private fun log(msg: String) {
        android.util.Log.e("MindustryLauncher", msg)
        try { java.io.File("/sdcard/mdl_crash.log").appendText("${System.currentTimeMillis()} ML: $msg\n") } catch (_: Exception) {}
    }

    fun launch(app: Application, gameJar: String, lwjglJar: String, gameDir: String, displayW: Int = 1920, displayH: Int = 1080) {
        log("launch start")
        val nativeLibDir = app.applicationInfo.nativeLibraryDir
        val jreVer = JreManager.detectVersion(app) ?: 25
        val jreHome = JreManager.getJavaHome(app, jreVer)
        val isJdk8 = jreVer <= 8
        val jreLib = if (isJdk8) "$jreHome/lib/aarch64" else jreHome + "/lib"

        log("Using JRE $jreVer at $jreHome, jreLib=$jreLib")

        // Step 1
        log("S1: setLdLibraryPath")
        val ldPaths = mutableListOf(nativeLibDir, jreLib, "/system/lib64", "/vendor/lib64")
        if (isJdk8) {
            ldPaths.add(0, "$jreLib/jli")
            ldPaths.add(1, "$jreLib/server")
        } else {
            ldPaths.add(0, "$jreHome/lib/server")
        }
        val ldPath = ldPaths.joinToString(":")
        ZLBridge.setLdLibraryPath(ldPath)
        log("S1 done")

        // Step 2: ZL2-aligned env vars with MobileGlues
        log("S2: env vars")
        mapOf(
            "JAVA_HOME" to jreHome,
            "POJAV_NATIVEDIR" to nativeLibDir,
            "HOME" to gameDir,
            "TMPDIR" to app.cacheDir.absolutePath,
            "LD_LIBRARY_PATH" to ldPath,
            "PATH" to "$jreHome/bin:${Os.getenv("PATH")}",
            "POJAV_RENDERER" to "opengles3",
            "LIBGL_ES" to "3",
            "LIBGL_GL" to "31",
            "LIBGL_MIPMAP" to "3",
            "LIBGL_NOERROR" to "1",
            "LIBGL_NOINTOVLHACK" to "1",
            "LIBGL_NORMALIZE" to "0",
            "LIBGL_USE_MC_COLOR" to "1",
            "MG_DIR_PATH" to "/sdcard/MG"
        ).forEach { (k, v) -> Os.setenv(k, v, true) }
        log("S2 done")

        // Step 3: load gl_hook BEFORE MobileGlues (intercepts glShaderSource, replaces #version)
        // Must use dlopen (RTLD_GLOBAL) so our glShaderSource symbol overrides MobileGlues'
        log("S3: dlopen gl_hook (before MobileGlues)")
        val ok = ZLBridge.dlopen("libgl_hook.so")
        log("S3 gl_hook result: $ok")

        // Step 4: dlopen engine libs (MobileGlues must load before JRE for GL interception)
        log("S4: dlopen engine libs (MobileGlues)")
        ZLBridge.dlopen("$nativeLibDir/libmobileglues.so")
        ZLBridge.dlopen("$nativeLibDir/libopenal.so")
        log("S3 engine done")

        // Step 5: dlopen JRE libs
        log("S5: dlopen JRE libs")
        val coreLibs = if (isJdk8) listOf(
            "$jreLib/jli/libjli.so", "$jreLib/server/libjvm.so",
            "$jreLib/libfreetype.so", "$jreLib/libverify.so",
            "$jreLib/libjava.so", "$jreLib/libnet.so", "$jreLib/libnio.so"
        ) else listOf(
            "$jreLib/libjli.so", "$jreLib/server/libjvm.so",
            "$jreLib/libfreetype.so", "$jreLib/libjava.so",
            "$jreLib/libnet.so", "$jreLib/libnio.so", "$jreLib/libverify.so"
        )
        coreLibs.forEach { lib -> if (File(lib).exists()) { log("dlopen $lib"); ZLBridge.dlopen(lib) } }
        File(jreLib).listFiles()?.filter { it.name.endsWith(".so") }?.forEach {
            ZLBridge.dlopen(it.absolutePath)
        }
        log("S5 done")

        // Step 6-7: setup exit hook + chdir
        log("S6: setup+exitHook+chdir")
        ZLNativeInvoker.staticLauncher = this
        ZLBridge.setupExitMethod(app.applicationContext)
        ZLBridge.initializeGameExitHook()
        ZLBridge.chdir(gameDir)
        log("S6 done")

        // Step 8
        log("S7: launchJVM")
        // Arc .so files are injected into Mindustry JAR, loaded normally via SharedLibraryLoader
        val classpath = "$lwjglJar:$gameJar"
        val args = arrayOf(
            "$jreHome/bin/java",
            "-Djava.home=$jreHome",
            "-Djava.io.tmpdir=${app.cacheDir.absolutePath}",
            "-Djna.boot.library.path=$nativeLibDir",
            "-Duser.home=$gameDir",
            "-Duser.language=en",
            "-Dorg.lwjgl.system.allocator=system",
            "-Dorg.lwjgl.freetype.libname=$nativeLibDir/libfreetype.so",
            "-Djava.awt.headless=true",
            "-Dmindustry.data=$gameDir",
            "-Dglfwstub.windowWidth=$displayW",
            "-Dglfwstub.windowHeight=$displayH",
            "-Dglfwstub.initEgl=false",
            "-Dos.name=Linux",
            "-Dos.version=Android-${Build.VERSION.RELEASE}",
            "-XX:ActiveProcessorCount=${Runtime.getRuntime().availableProcessors()}",
            "-XX:-UsePerfData",
            "-XX:-UseCompressedOops",
            "-XX:-UseCompressedClassPointers",
            "-javaagent:$gameDir/preload-agent.jar",
            "-Xms256M", "-Xmx4G",
            "-cp", classpath,
            "mindustry.desktop.DesktopLauncher",
            "-width", displayW.toString(),
            "-height", displayH.toString()
        )

        val exitCode = VMLauncher.launchJVM(args)
        android.util.Log.i("MindustryLauncher", "JVM exited with code: $exitCode")
    }
}
