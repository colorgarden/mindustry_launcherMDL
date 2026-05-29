package io.colorgarden.mdl

import android.app.Application
import android.system.Os

class MDLApplication : Application() {
    val container by lazy { AppContainer(this) }

    private val isGameProcess: Boolean
        get() {
            val name = getProcessName()
            return name != null && name.endsWith(":game")
        }

    override fun onCreate() {
        super.onCreate()
        if (isGameProcess) {
            // Game process: minimal init — don't create threads/mutexes that
            // could conflict with the embedded JVM
            return
        }
        container.initialize()
    }
}
