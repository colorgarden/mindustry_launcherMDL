package io.colorgarden.mdl.viewmodel

import androidx.lifecycle.ViewModel
import io.colorgarden.mdl.AppContainer
import io.colorgarden.mdl.data.service.L
import io.colorgarden.mdl.service.EasyTierConfig
import io.colorgarden.mdl.service.EasyTierManager
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow

class MultiplayerViewModel(private val c: AppContainer) : ViewModel() {
    val easytierState = EasyTierManager.state

    private val _statusText = MutableStateFlow("")
    val statusText: StateFlow<String> = _statusText

    private val _nickname = MutableStateFlow("")
    val nickname: StateFlow<String> = _nickname

    private val _roomCode = MutableStateFlow("")
    val roomCode: StateFlow<String> = _roomCode

    init {
        _nickname.value = c.configService.getConfig().playerNickname
        EasyTierManager.init(c.context)
        _statusText.value = if (EasyTierManager.isNativeAvailable()) {
            L.get("multiplayer.ready")
        } else {
            L.get("multiplayer.cached_ready")
        }
    }

    fun setNickname(name: String) {
        _nickname.value = name
    }

    fun setRoomCode(code: String) {
        _roomCode.value = code.filter { it.isDigit() }.take(6)
    }

    fun createLobby() {
        val name = _nickname.value.ifEmpty {
            _statusText.value = L.get("multiplayer.name_required")
            return
        }
        // roomCode is the last 6 digits of the secrets - acts as room identifier
        val code = generateRoomCode().also { _roomCode.value = it }

        val config = EasyTierConfig(
            networkName = "mdl-$code",
            networkSecret = "mdl-secret-$code",
            virtualIp = "10.144.144.1"
        )
        _statusText.value = "Creating lobby... ($code)"
        EasyTierManager.start(config)
        c.configService.getConfig().playerNickname = name
        c.configService.saveConfig()
    }

    fun joinLobby() {
        val code = _roomCode.value
        val name = _nickname.value

        if (code.length != 6) {
            _statusText.value = L.get("multiplayer.invalid_room")
            return
        }
        if (name.isEmpty()) {
            _statusText.value = L.get("multiplayer.room_and_name_required")
            return
        }

        val config = EasyTierConfig(
            networkName = "mdl-$code",
            networkSecret = "mdl-secret-$code",
            virtualIp = "10.144.144.${(2..254).random()}"
        )
        _statusText.value = "Joining lobby $code..."
        EasyTierManager.start(config)
        c.configService.getConfig().playerNickname = name
        c.configService.saveConfig()
    }

    fun leaveLobby() {
        EasyTierManager.stop()
        _statusText.value = "Disconnected"
    }

    fun refreshPeers() {
        EasyTierManager.refreshPeers()
    }

    private fun generateRoomCode(): String {
        return (100000..999999).random().toString()
    }

    override fun onCleared() {
        super.onCleared()
    }
}
