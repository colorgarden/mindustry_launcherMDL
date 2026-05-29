package com.movtery.zalithlauncher.bridge

import android.app.Activity
import android.content.ClipData
import android.content.ClipboardManager
import android.content.Context
import android.content.Intent
import android.net.Uri
import android.os.Process
import androidx.annotation.Keep

@Keep
object ZLNativeInvoker {
    @JvmStatic
    @Volatile
    var staticLauncher: Any? = null

    @Keep @JvmStatic
    fun openLink(link: String) {
        val ctx = com.movtery.zalithlauncher.context.ContextsKt.getGlobalContext()
        if (ctx is Activity) {
            ctx.runOnUiThread {
                if (link.startsWith("http://") || link.startsWith("https://")) {
                    ctx.startActivity(Intent(Intent.ACTION_VIEW, Uri.parse(link)))
                }
            }
        }
    }

    @Keep @JvmStatic
    fun querySystemClipboard() {
        val ctx = com.movtery.zalithlauncher.context.ContextsKt.getGlobalContext()
        if (ctx is Activity) {
            ctx.runOnUiThread {
                val cm = ctx.getSystemService(Context.CLIPBOARD_SERVICE) as? ClipboardManager
                val clip = cm?.primaryClip
                if (clip != null && clip.itemCount > 0) {
                    val text = clip.getItemAt(0).text?.toString()
                    ZLBridge.clipboardReceived(text, "plain")
                } else {
                    ZLBridge.clipboardReceived(null, null)
                }
            }
        }
    }

    @Keep @JvmStatic
    fun putClipboardData(data: String, mimeType: String) {
        val ctx = com.movtery.zalithlauncher.context.ContextsKt.getGlobalContext()
        if (ctx is Activity) {
            ctx.runOnUiThread {
                val clip = when (mimeType) {
                    "text/plain" -> ClipData.newPlainText("MDL", data)
                    "text/html" -> ClipData.newHtmlText("MDL", data, data)
                    else -> null
                }
                val cm = ctx.getSystemService(Context.CLIPBOARD_SERVICE) as? ClipboardManager
                clip?.let { cm?.setPrimaryClip(it) }
            }
        }
    }

    @Keep @JvmStatic
    fun jvmExit(exitCode: Int, isSignal: Boolean) {
        staticLauncher = null
        Process.killProcess(Process.myPid())
    }
}
