package io.colorgarden.mdl.data.model

import com.google.gson.annotations.SerializedName

// ===== Config =====
data class AppConfig(
    @SerializedName("ManagedFolders") val managedFolders: MutableList<String> = mutableListOf(),
    @SerializedName("Language") var language: String = "",
    @SerializedName("GlobalRamMB") var globalRamMB: Int = 512,
    @SerializedName("ProxyNodeIndex") var proxyNodeIndex: Int = 1,
    @SerializedName("PlayerNickname") var playerNickname: String = "",
    @SerializedName("UseIsolation") var useIsolation: Boolean = false,
    @SerializedName("DarkMode") var darkMode: Int = 0  // 0=auto, 1=light, 2=dark
)

data class VersionConfig(
    @SerializedName("CustomRamMB") var customRamMB: Int = 512,
    @SerializedName("UseIsolation") var useIsolation: Boolean = false,
    @SerializedName("ExtraJvmArgs") var extraJvmArgs: String = "",
    @SerializedName("CustomJavaPath") var customJavaPath: String = "",
    @SerializedName("InstanceName") var instanceName: String = ""
)

data class GameInstanceInfo(
    val name: String = "",
    val fullPath: String = ""
)

// ===== GitHub Releases =====
data class GitHubRelease(
    @SerializedName("tag_name") val tagName: String = "",
    @SerializedName("name") val name: String = "",
    @SerializedName("assets") val assets: List<GitHubAsset>? = null,
    @SerializedName("prerelease") val prerelease: Boolean = false,
    @SerializedName("body") val body: String? = null,
    @SerializedName("html_url") val htmlUrl: String? = null
)

data class GitHubAsset(
    @SerializedName("name") val name: String = "",
    @SerializedName("browser_download_url") val browserDownloadUrl: String = "",
    @SerializedName("size") val size: Long = 0,
    @SerializedName("content_type") val contentType: String = ""
)

// ===== Mod =====
data class ModRegistryEntry(
    @SerializedName("name") val name: String = "",
    @SerializedName("displayName") val displayName: String = "",
    @SerializedName("author") val author: String = "",
    @SerializedName("description") val description: String? = null,
    @SerializedName("repo") val repo: String = "",
    @SerializedName("stars") val stars: Int = 0,
    @SerializedName("lastUpdated") val lastUpdated: String? = null
) {
    val authorFormatted: String get() = if (author.isEmpty()) "Unknown" else "By $author"
    val starsFormatted: String get() = "$stars ★"
}

data class ModInfo(
    var fileName: String = "",
    var fullPath: String = "",
    var fileSize: String = "",
    var displayName: String = "",
    var author: String = "",
    var description: String = "",
    var version: String = "",
    var iconPngBytes: ByteArray? = null
)

// ===== Schematic =====
data class SchematicEntry(
    val realName: String,
    val description: String,
    val fileName: String,
    val zipEntryFullName: String
) {
    val uiName: String get() = realName.ifEmpty { fileName }
    val uiDescription: String get() = description.ifEmpty { "(No description)" }
}

// ===== Save metadata =====
data class MindustrySaveMetadata(
    var version: String = "",
    var mapName: String = "",
    var author: String = "",
    var description: String = "",
    var wave: String = "",
    var playTime: String = ""
)

// ===== Settings =====
data class SettingItem(
    val key: String,
    val type: Byte,
    var originalValue: Any? = null,
    var displayValue: String = ""
)
