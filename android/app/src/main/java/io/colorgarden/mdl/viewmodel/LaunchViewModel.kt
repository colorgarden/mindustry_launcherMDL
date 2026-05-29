package io.colorgarden.mdl.viewmodel

import android.content.Intent
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import io.colorgarden.mdl.AppContainer
import io.colorgarden.mdl.data.model.GameInstanceInfo
import io.colorgarden.mdl.data.service.L
import io.colorgarden.mdl.service.JreManager
import io.colorgarden.mdl.service.PojavRuntime
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.drop
import kotlinx.coroutines.launch

class LaunchViewModel(private val c: AppContainer) : ViewModel() {
    private val _instances = MutableStateFlow<List<GameInstanceInfo>>(emptyList())
    val instances: StateFlow<List<GameInstanceInfo>> = _instances

    private val _currentName = MutableStateFlow(L.get("launch.no_version"))
    val currentName: StateFlow<String> = _currentName

    private val _statusText = MutableStateFlow("")
    val statusText: StateFlow<String> = _statusText

    init {
        refresh()
        viewModelScope.launch {
            c.refreshInstances.drop(1).collect { refresh() }
        }
    }

    fun refresh() {
        val list = c.versionService.getAllInstalledInstances()
        _instances.value = list
        _currentName.value = c.versionService.currentInstance?.name ?: L.get("launch.no_version")
        _statusText.value = "${list.size} ${L.get("launch.status_installed")}"
    }

    fun selectInstance(instance: GameInstanceInfo) {
        c.versionService.currentInstance = instance
        c.versionService.loadVersionConfig(instance.fullPath)
        _currentName.value = instance.name
        _statusText.value = instance.fullPath
    }

    fun launch() {
        val instance = c.versionService.currentInstance
        if (instance == null) {
            _statusText.value = L.get("status.select_version_first")
            return
        }

        // Check JRE (auto-detect version)
        val jreVer = JreManager.detectVersion(c.context)
        if (jreVer == null) {
            _statusText.value = "JRE not installed. Place jre-25 or jre-8 at ${c.context.filesDir}/runtimes/"
            return
        }
        if (!JreManager.isInstalled(c.context, jreVer)) {
            _statusText.value = "JRE $jreVer incomplete"
            return
        }

        val versionDir = java.io.File(instance.fullPath)
        val jarFiles = versionDir.listFiles()?.filter {
            it.extension.equals("jar", ignoreCase = true)
        } ?: emptyList()
        if (jarFiles.isEmpty()) {
            _statusText.value = L.get("status.core_missing")
            return
        }
        val gameJar = jarFiles.first().absolutePath

        // LWJGL JAR 从 assets 复制到内部存储（首次）
        val runtimesDir = java.io.File(c.context.filesDir, "runtimes")
        val lwjglDir = java.io.File(runtimesDir, "components/lwjgl3")
        val lwjglJar = java.io.File(lwjglDir, "lwjgl-glfw-classes.jar")
        if (!lwjglJar.exists()) {
            try {
                lwjglDir.mkdirs()
                c.context.assets.open("components/lwjgl3/lwjgl-glfw-classes.jar").use { input ->
                    java.io.FileOutputStream(lwjglJar).use { output ->
                        input.copyTo(output)
                    }
                }
            } catch (e: Exception) {
                _statusText.value = "Failed to extract LWJGL: ${e.message}"
                return
            }
        }

        val intent = Intent(c.context, PojavRuntime::class.java).apply {
            putExtra("game_jar", gameJar)
            putExtra("lwjgl_jar", lwjglJar.absolutePath)
            putExtra("jre_home", JreManager.getJavaHome(c.context))
            putExtra("game_dir", instance.fullPath)
            addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
        }
        c.context.startActivity(intent)
        c.versionService.runningInstancePaths.add(instance.fullPath)
        _statusText.value = "Running: ${instance.name}"
    }

    fun versionSettings() {
        if (c.versionService.currentInstance == null)
            _statusText.value = L.get("status.select_version_first")
    }

    fun openFolder() {
        val path = c.versionService.currentInstance?.fullPath ?: return
        _statusText.value = "Path: $path"
    }

    fun importJar() {
        _statusText.value = "Import: select JAR"
    }
}
