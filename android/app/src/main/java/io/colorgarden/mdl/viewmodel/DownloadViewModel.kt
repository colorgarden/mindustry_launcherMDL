package io.colorgarden.mdl.viewmodel

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import io.colorgarden.mdl.AppContainer
import io.colorgarden.mdl.data.model.GameInstanceInfo
import io.colorgarden.mdl.data.model.GitHubAsset
import io.colorgarden.mdl.data.model.GitHubRelease
import io.colorgarden.mdl.data.service.RemoteDownloadService
import io.colorgarden.mdl.data.service.L

import kotlinx.coroutines.*
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow

class DownloadViewModel(private val c: AppContainer) : ViewModel() {
    private val _releases = MutableStateFlow<List<GitHubRelease>>(emptyList())
    val releases: StateFlow<List<GitHubRelease>> = _releases

    private val _isLoading = MutableStateFlow(false)
    val isLoading: StateFlow<Boolean> = _isLoading

    private val _statusText = MutableStateFlow("")
    val statusText: StateFlow<String> = _statusText

    private val _downloadProgress = MutableStateFlow(0.0)
    val downloadProgress: StateFlow<Double> = _downloadProgress

    private val _showProgress = MutableStateFlow(false)
    val showProgress: StateFlow<Boolean> = _showProgress

    private val _repo = MutableStateFlow("Anuken/Mindustry")
    val repo: StateFlow<String> = _repo

    private var fetchJob: Job? = null
    private var downloadJob: Job? = null

    fun setRepo(r: String) {
        _repo.value = r
        c.downloadService.currentDownloadRepo = r
    }

    fun fetchReleases() {
        fetchJob?.cancel()
        _releases.value = emptyList()
        fetchJob = viewModelScope.launch(Dispatchers.IO) {
            _isLoading.value = true
            _statusText.value = L.get("download.fetching")
            try {
                val list = c.downloadService.fetchFilteredReleases()
                if (isActive) {
                    _releases.value = list
                    _statusText.value = "${list.size} releases"
                }
            } catch (e: CancellationException) {
                // 被新请求取消，不更新状态
            } catch (e: Exception) {
                if (isActive) {
                    _statusText.value = L.t("download.fetch_timeout", e.message ?: "")
                }
            }
            if (isActive) {
                _isLoading.value = false
            }
        }
    }

    fun downloadRelease(release: GitHubRelease) {
        downloadJob?.cancel()
        downloadJob = viewModelScope.launch(Dispatchers.IO) {
            try {
                val assets = RemoteDownloadService.filterClientAssets(release)
                val best = RemoteDownloadService.selectBestAsset(assets, _repo.value)
                    ?: return@launch

                val managedFolder = c.configService.getConfig().managedFolders.firstOrNull()
                    ?: return@launch

                val folderName = c.downloadService.getDownloadFolderName(release.tagName, managedFolder)
                java.io.File(folderName).mkdirs()
                val destPath = java.io.File(folderName, best.name).absolutePath

                _showProgress.value = true
                _statusText.value = L.get("download.preparing")

                c.downloadService.downloadFile(
                    best.browserDownloadUrl,
                    destPath,
                    onProgress = { _downloadProgress.value = it }
                )

                // 统一重命名为 Mindustry.jar
                val expectedJar = java.io.File(folderName, "Mindustry.jar")
                val downloadedFile = java.io.File(destPath)
                if (!downloadedFile.name.equals("Mindustry.jar", ignoreCase = true)) {
                    downloadedFile.renameTo(expectedJar)
                }
                val jarFile = if (expectedJar.exists()) expectedJar else downloadedFile

                if (isActive) {
                    _statusText.value = L.get("download.success")
                    c.triggerRefresh()
                }
            } catch (e: CancellationException) {
                // 被取消
            } catch (e: Exception) {
                if (isActive) {
                    _statusText.value = L.t("download.fail", e.message ?: "")
                }
            }
            if (isActive) {
                _showProgress.value = false
                _downloadProgress.value = 0.0
            }
        }
    }

    override fun onCleared() {
        super.onCleared()
        fetchJob?.cancel()
        downloadJob?.cancel()
    }
}
