package io.colorgarden.mdl.service

import android.content.Context
import android.content.Intent
import android.util.Log
import com.google.gson.Gson
import com.google.gson.reflect.TypeToken
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow

data class EasyTierState(
    val running: Boolean = false,
    val virtualIp: String = "",
    val peers: List<EasyTierPeer> = emptyList(),
    val error: String = ""
)

data class EasyTierPeer(
    val virtualIp: String = "",
    val hostname: String = "",
    val latencyMs: Long = 0
)

data class EasyTierConfig(
    val networkName: String = "mdl-game",
    val networkSecret: String = "mdl-secret",
    val virtualIp: String = "10.144.144.1",
    val peers: List<String> = emptyList(),
    val listeners: List<String> = listOf("tcp://0.0.0.0:11010", "udp://0.0.0.0:11010")
)

object EasyTierManager {
    private val _state = MutableStateFlow(EasyTierState())
    val state: StateFlow<EasyTierState> = _state

    private var appContext: Context? = null
    private var currentConfig = EasyTierConfig()

    fun init(context: Context) {
        appContext = context.applicationContext
    }

    fun isNativeAvailable(): Boolean {
        EasyTierJni.load()
        return EasyTierJni.isAvailable
    }

    fun start(config: EasyTierConfig) {
        val ctx = appContext ?: run {
            _state.value = _state.value.copy(error = "Manager not initialized")
            return
        }

        currentConfig = config
        _state.value = _state.value.copy(error = "", virtualIp = config.virtualIp)

        if (!isNativeAvailable()) {
            startWithoutNative(config)
            return
        }

        val configJson = Gson().toJson(mapOf(
            "network_identity" to mapOf(
                "network_name" to config.networkName,
                "network_secret" to config.networkSecret
            ),
            "instance_id" to config.virtualIp,
            "listeners" to config.listeners,
            "peers" to config.peers.map { mapOf("uri" to it) }
        ))

        try {
            EasyTierJni.runNetworkInstance(configJson)
        } catch (e: Exception) {
            Log.e("MDL", "EasyTier native start failed: ${e.message}")
        }

        val intent = Intent(ctx, EasyTierVpnService::class.java).apply {
            putExtra("vpn_prefix", config.virtualIp)
        }
        ctx.startService(intent)
    }

    private fun startWithoutNative(config: EasyTierConfig) {
        _state.value = _state.value.copy(
            running = true,
            virtualIp = config.virtualIp,
            error = "EasyTier native library not loaded — VPN mode unavailable. " +
                    "Place libeasytier_android_jni.so in jniLibs/{abi}/ to enable full functionality."
        )
    }

    fun stop() {
        val ctx = appContext ?: return
        val intent = Intent(ctx, EasyTierVpnService::class.java).apply {
            action = "STOP"
        }
        ctx.startService(intent)
        EasyTierJni.stopNetworkInstance()
        _state.value = EasyTierState()
    }

    fun refreshPeers() {
        if (!isNativeAvailable()) return
        try {
            val json = EasyTierJni.getPeersJson()
            val type = object : TypeToken<List<Map<String, Any>>>() {}.type
            val list: List<Map<String, Any>> = Gson().fromJson(json, type) ?: emptyList()
            val peers = list.map {
                EasyTierPeer(
                    virtualIp = it["virtual_ip"] as? String ?: "",
                    hostname = it["hostname"] as? String ?: "",
                    latencyMs = (it["latency_ms"] as? Number)?.toLong() ?: 0L
                )
            }
            _state.value = _state.value.copy(peers = peers)
        } catch (e: Exception) {
            Log.e("MDL", "Failed to refresh peers: ${e.message}")
        }
    }

    fun onTunReady() {
        _state.value = _state.value.copy(running = true, error = "")
    }

    fun onVpnStartFailed(reason: String) {
        _state.value = _state.value.copy(running = false, error = reason)
    }
}
