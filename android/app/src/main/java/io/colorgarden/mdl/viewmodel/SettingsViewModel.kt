package io.colorgarden.mdl.viewmodel

import androidx.lifecycle.ViewModel
import io.colorgarden.mdl.AppContainer
import io.colorgarden.mdl.data.service.L
import io.colorgarden.mdl.data.model.AppConfig
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow

class SettingsViewModel(private val c: AppContainer) : ViewModel() {
    private val _config = MutableStateFlow(c.configService.getConfig())
    val config: StateFlow<AppConfig> = _config

    private val _statusText = MutableStateFlow("")
    val statusText: StateFlow<String> = _statusText

    private val _proxyIndex = MutableStateFlow(c.configService.getConfig().proxyNodeIndex)
    val proxyIndex: StateFlow<Int> = _proxyIndex

    private val _language = MutableStateFlow(c.configService.getConfig().language)
    val language: StateFlow<String> = _language

    private val _darkMode = MutableStateFlow(c.configService.getConfig().darkMode)
    val darkMode: StateFlow<Int> = _darkMode

    fun setDarkMode(mode: Int) {
        _darkMode.value = mode
        c.configService.getConfig().darkMode = mode
        c.configService.saveConfig()
    }

    fun setProxyIndex(index: Int) {
        _proxyIndex.value = index
        c.configService.getConfig().proxyNodeIndex = index
        c.configService.saveConfig()
        _statusText.value = "${L.get("settings.proxy_label")}: ${index}"
    }

    fun setLanguage(langCode: String) {
        _language.value = langCode
        c.configService.getConfig().language = langCode
        c.configService.saveConfig()
        val effective = c.configService.getEffectiveLanguage(langCode)
        L.loadLanguage(effective)
        _statusText.value = "Language: $langCode"
    }

    fun setNickname(nickname: String) {
        c.configService.getConfig().playerNickname = nickname
        c.configService.saveConfig()
        _config.value = c.configService.getConfig()
    }

    fun setGlobalRam(mb: Int) {
        c.configService.getConfig().globalRamMB = mb
        c.configService.saveConfig()
        _config.value = c.configService.getConfig()
    }

    fun setUseIsolation(value: Boolean) {
        c.configService.getConfig().useIsolation = value
        c.configService.saveConfig()
        _config.value = c.configService.getConfig()
    }

    fun addManagedFolder(path: String) {
        val folders = c.configService.getConfig().managedFolders
        if (!folders.contains(path)) {
            folders.add(path)
            c.configService.saveConfig()
            _config.value = c.configService.getConfig()
        }
    }

    fun removeManagedFolder(path: String) {
        c.configService.getConfig().managedFolders.remove(path)
        c.configService.saveConfig()
        _config.value = c.configService.getConfig()
    }

    fun saveConfig() {
        c.configService.saveConfig()
        _config.value = c.configService.getConfig()
        _statusText.value = L.get("dialog.success")
    }
}
