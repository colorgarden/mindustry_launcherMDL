package com.movtery.zalithlauncher.context

import android.content.Context

object ContextsKt {
    private var ctx: Context? = null

    @JvmStatic
    fun getGlobalContext(): Context = ctx!!

    fun init(context: Context) {
        ctx = context.applicationContext
    }
}
