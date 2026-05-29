package io.colorgarden.mdl.service

object EasyTierJni {
    private var loaded = false

    fun load() {
        if (!loaded) {
            try {
                System.loadLibrary("easytier_android_jni")
                loaded = true
            } catch (e: UnsatisfiedLinkError) {
                loaded = false
            }
        }
    }

    val isAvailable: Boolean get() = loaded

    external fun runNetworkInstance(configJson: String): Int
    external fun setTunFd(fd: Int)
    external fun collectNetworkInfos(): String
    external fun stopNetworkInstance()
    external fun getPeersJson(): String
}
