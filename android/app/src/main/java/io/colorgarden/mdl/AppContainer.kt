package io.colorgarden.mdl

import android.content.Context
import io.colorgarden.mdl.data.model.UrlHelper
import io.colorgarden.mdl.data.service.*
import kotlinx.coroutines.flow.MutableStateFlow

class AppContainer(val context: Context) {
    val configService = ConfigService(context)
    val versionService = VersionManagementService(configService)
    val downloadService = RemoteDownloadService(configService)
    val modService = ModService()
    val schematicService = SchematicService()

    /** 下载完成或 onResume 时 +1，LaunchViewModel 据此刷新版本列表 */
    val refreshInstances = MutableStateFlow(0L)

    fun triggerRefresh() {
        refreshInstances.value++
    }

    fun initialize() {
        configService.loadConfig()

        // Init language
        L.langDirPath = java.io.File(context.filesDir, "Lang").absolutePath
        java.io.File(L.langDirPath).mkdirs()

        // Always overwrite to ensure new keys are available after updates
        val langMappings = mapOf("zh_cn" to "zh-CN", "en_us" to "en-US")
        for ((resName, langCode) in langMappings) {
            try {
                val resId = context.resources.getIdentifier(resName, "raw", context.packageName)
                if (resId != 0) {
                    val destFile = java.io.File(L.langDirPath, "$langCode.json")
                    context.resources.openRawResource(resId).use { input ->
                        destFile.outputStream().use { output ->
                            input.copyTo(output)
                        }
                    }
                }
            } catch (_: Exception) {}
        }

        // Load language
        val langCode = configService.getConfig().language
        val effective = if (langCode.isEmpty()) L.autoDetect() else langCode
        L.loadLanguage(effective)

        UrlHelper.proxyIndex = configService.getConfig().proxyNodeIndex
    }
}
