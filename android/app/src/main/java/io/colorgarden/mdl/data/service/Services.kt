package io.colorgarden.mdl.data.service

import android.content.Context
import android.util.Log
import com.google.gson.Gson
import com.google.gson.reflect.TypeToken
import io.colorgarden.mdl.data.model.*
import kotlinx.coroutines.*
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import okhttp3.*
import java.io.*
import java.nio.ByteBuffer
import java.nio.ByteOrder
import java.util.concurrent.TimeUnit
import java.util.regex.Pattern
import java.util.zip.DeflaterInputStream
import java.util.zip.ZipEntry
import java.util.zip.ZipFile
import java.util.zip.ZipInputStream

// ===== L (Localization) =====
object L {
    private var strings: MutableMap<String, String> = mutableMapOf()
    var currentLang: String = "zh-CN"
        private set
    var langDirPath: String = ""
    var onLanguageChanged: (() -> Unit)? = null
    private val _langVersion = MutableStateFlow(0L)
    val langVersion: StateFlow<Long> = _langVersion

    fun get(key: String): String = strings[key] ?: "[[$key]]"

    fun t(key: String, vararg args: Any): String {
        val template = get(key)
        return try { String.format(template, *args) } catch (_: Exception) { template }
    }

    fun loadLanguage(langCode: String) {
        var code = langCode
        var path = java.io.File(langDirPath, "$code.json")

        if (!path.exists()) {
            code = "zh-CN"
            path = java.io.File(langDirPath, "zh-CN.json")
        }

        if (path.exists()) {
            try {
                val json = path.readText()
                val type = object : TypeToken<Map<String, String>>() {}.type
                strings = Gson().fromJson(json, type) ?: mutableMapOf()
            } catch (e: Exception) {
                strings = mutableMapOf()
            }
        }

        currentLang = code
        _langVersion.value++
        onLanguageChanged?.invoke()
    }

    fun autoDetect(): String {
        val locale = java.util.Locale.getDefault()
        return when {
            locale.language.startsWith("zh") -> "zh-CN"
            else -> "zh-CN" // 默认中文
        }
    }
}

// ===== ConfigService =====
class ConfigService(private val context: Context) {
    private var config: AppConfig = AppConfig()
    private val configFile: java.io.File
        get() = java.io.File(context.filesDir, "launcher_config.json")
    private val gson = Gson()

    val defaultMdlDir: String by lazy {
        val extDir = android.os.Environment.getExternalStorageDirectory().absolutePath
        java.io.File(extDir, "MDL").also { it.mkdirs() }.absolutePath
    }

    fun getConfig(): AppConfig {
        if (config.managedFolders.isEmpty()) {
            config.managedFolders.add(defaultMdlDir)
            saveConfig()
        }
        return config
    }

    fun loadConfig() {
        if (configFile.exists()) {
            try {
                config = gson.fromJson(configFile.readText(), AppConfig::class.java) ?: AppConfig()
            } catch (e: Exception) {
                Log.e("MDL", "Failed to load config: ${e.message}")
            }
        }
    }

    fun saveConfig() {
        try {
            configFile.parentFile?.mkdirs()
            configFile.writeText(gson.toJson(config))
        } catch (e: Exception) {
            Log.e("MDL", "Failed to save config: ${e.message}")
        }
    }

    fun getEffectiveLanguage(selectedTag: String): String {
        return if (selectedTag == "auto") L.autoDetect() else selectedTag
    }
}

// ===== VersionManagementService =====
class VersionManagementService(private val config: ConfigService) {
    var currentInstance: GameInstanceInfo? = null
    val runningInstancePaths: MutableSet<String> = mutableSetOf()
    var currentVersionConfig: VersionConfig = VersionConfig()

    fun getAllInstalledInstances(): List<GameInstanceInfo> {
        val all = mutableListOf<GameInstanceInfo>()
        for (root in config.getConfig().managedFolders) {
            all.addAll(getInstancesInFolder(root))
        }
        return all
    }

    fun loadVersionConfig(instancePath: String) {
        val configPath = java.io.File(instancePath, "mdl_instance_config.json")
        if (configPath.exists()) {
            try {
                currentVersionConfig = Gson().fromJson(configPath.readText(), VersionConfig::class.java) ?: VersionConfig()
            } catch (e: Exception) {
                currentVersionConfig = VersionConfig()
            }
        } else {
            currentVersionConfig = VersionConfig()
            currentVersionConfig.customRamMB = config.getConfig().globalRamMB
        }
    }

    fun isInstanceRunning(instancePath: String): Boolean = runningInstancePaths.contains(instancePath)

    companion object {
        fun getInstancesInFolder(root: String): List<GameInstanceInfo> {
            val list = mutableListOf<GameInstanceInfo>()
            val vDir = java.io.File(root, "Versions")
            if (!vDir.exists() || !vDir.isDirectory) return list

            vDir.listFiles()?.filter { it.isDirectory }?.forEach { dir ->
                val hasJar = dir.listFiles()?.any {
                    it.isFile && it.extension.equals("jar", ignoreCase = true)
                } ?: false
                if (hasJar) {
                    list.add(GameInstanceInfo(name = dir.name, fullPath = dir.absolutePath))
                }
            }
            return list
        }

        fun saveVersionConfigToFile(instancePath: String, config: VersionConfig) {
            val configPath = java.io.File(instancePath, "mdl_instance_config.json")
            try {
                configPath.writeText(Gson().toJson(config))
            } catch (e: Exception) {
                Log.e("MDL", "Failed to save version config: ${e.message}")
            }
        }
    }
}

// ===== RemoteDownloadService =====
class RemoteDownloadService(private val config: ConfigService) {
    var currentDownloadRepo: String = "Anuken/Mindustry"

    private val client = OkHttpClient.Builder()
        .connectTimeout(45, TimeUnit.SECONDS)
        .readTimeout(45, TimeUnit.SECONDS)
        .build()

    suspend fun fetchFilteredReleases(): List<GitHubRelease> = withContext(Dispatchers.IO) {
        val apiUrl = UrlHelper.format("https://api.github.com/repos/$currentDownloadRepo/releases", isApi = true)
        val request = Request.Builder().url(apiUrl)
            .header("User-Agent", "MDL-Mobile/0.3")
            .build()

        val json = client.newCall(request).execute().use { response ->
            if (!response.isSuccessful) throw IOException("HTTP ${response.code}")
            response.body?.string() ?: throw IOException("Empty body")
        }

        val type = object : TypeToken<List<GitHubRelease>>() {}.type
        val rels: List<GitHubRelease> = Gson().fromJson(json, type) ?: emptyList()

        rels.filter { release ->
            release.assets?.any { asset ->
                asset.name.endsWith(".jar", ignoreCase = true)
                        && !asset.name.contains("server", ignoreCase = true)
                        && !asset.name.contains("android", ignoreCase = true)
                        && !asset.name.contains("dependencies", ignoreCase = true)
                        && !asset.name.contains("javadoc", ignoreCase = true)
                        && !asset.name.contains("sources", ignoreCase = true)
            } ?: false
        }
    }

    fun getDownloadFolderName(tagName: String, managedFolder: String): String {
        var suffix = ""
        when {
            currentDownloadRepo.contains("TinyLake", ignoreCase = true) -> suffix = L.get("download.suffix_x")
            currentDownloadRepo.contains("antigrief", ignoreCase = true) -> suffix = L.get("download.suffix_foo")
        }

        var folder = java.io.File(managedFolder, "Versions").resolve(tagName + suffix).absolutePath
        var c = 1
        val baseFolder = folder
        while (java.io.File(folder).exists()) {
            folder = "$baseFolder-${c++}"
        }
        return folder
    }

    suspend fun downloadFile(url: String, destPath: String, onProgress: ((Double) -> Unit)? = null) {
        withContext(Dispatchers.IO) {
            val request = Request.Builder().url(url).build()
            client.newCall(request).execute().use { response ->
                if (!response.isSuccessful) throw IOException("HTTP ${response.code}")
                val body = response.body ?: throw IOException("Empty body")
                val total = body.contentLength()
                val input = body.byteStream()
                val output = java.io.File(destPath).outputStream()

                val buf = ByteArray(8192)
                var read = 0L
                var n: Int
                while (input.read(buf).also { n = it } != -1) {
                    output.write(buf, 0, n)
                    read += n
                    if (total != -1L) {
                        onProgress?.invoke(read.toDouble() / total * 100)
                    }
                }
                output.close()
                input.close()
            }
        }
    }

    companion object {
        fun selectBestAsset(candidates: List<GitHubAsset>?, repo: String): GitHubAsset? {
            if (candidates.isNullOrEmpty()) return null

            if (repo.contains("antigrief", ignoreCase = true)) {
                return selectFooAsset(candidates)
            }

            candidates.find { it.name.equals("Mindustry.jar", ignoreCase = true) }?.let { return it }
            candidates.find {
                it.name.contains("desktop", ignoreCase = true)
                        || it.name.contains("Desktop")
                        || it.name.contains("client", ignoreCase = true)
                        || it.name.contains("windows", ignoreCase = true)
            }?.let { return it }

            val nonMod = candidates.filter {
                !it.name.contains("mod", ignoreCase = true)
                        && !it.name.contains("addon", ignoreCase = true)
                        && !it.name.contains("plugin", ignoreCase = true)
            }
            return nonMod.firstOrNull() ?: candidates.firstOrNull()
        }

        private fun selectFooAsset(candidates: List<GitHubAsset>): GitHubAsset? {
            candidates.find {
                (it.name.contains("desktop", ignoreCase = true) || it.name.contains("client", ignoreCase = true))
                        && !it.name.contains("audio", ignoreCase = true)
                        && !it.name.contains("voice", ignoreCase = true)
            }?.let { return it }

            return candidates.find {
                !it.name.contains("audio", ignoreCase = true) && !it.name.contains("voice", ignoreCase = true)
            }
        }

        fun filterClientAssets(release: GitHubRelease): List<GitHubAsset> {
            return release.assets?.filter {
                it.name.endsWith(".jar", ignoreCase = true)
                        && !it.name.contains("server", ignoreCase = true)
                        && !it.name.contains("android", ignoreCase = true)
                        && !it.name.contains("dependencies", ignoreCase = true)
                        && !it.name.contains("javadoc", ignoreCase = true)
                        && !it.name.contains("sources", ignoreCase = true)
            } ?: emptyList()
        }
    }
}

// ===== ModService =====
class ModService {
    var allOnlineMods: List<ModRegistryEntry> = emptyList()
    var selectedModToInstall: ModRegistryEntry? = null

    private val client = OkHttpClient.Builder()
        .connectTimeout(30, TimeUnit.SECONDS)
        .readTimeout(30, TimeUnit.SECONDS)
        .build()

    suspend fun fetchModRegistry(): List<ModRegistryEntry> = withContext(Dispatchers.IO) {
        // jsDelivr CDN — 国内比 raw.githubusercontent.com 代理更稳定
        val url = "https://cdn.jsdelivr.net/gh/Anuken/MindustryMods@master/mods.json"
        val request = Request.Builder().url(url).build()

        val json = client.newCall(request).execute().use { response ->
            if (!response.isSuccessful) throw IOException("HTTP ${response.code}")
            response.body?.string() ?: throw IOException("Empty body")
        }

        val type = object : TypeToken<List<ModRegistryEntry>>() {}.type
        val list: List<ModRegistryEntry> = Gson().fromJson(json, type) ?: emptyList()
        allOnlineMods = list.sortedByDescending { it.stars }
        allOnlineMods
    }

    suspend fun fetchModReleases(repo: String): List<GitHubRelease> = withContext(Dispatchers.IO) {
        val apiUrl = UrlHelper.format("https://api.github.com/repos/$repo/releases", isApi = true)
        val request = Request.Builder().url(apiUrl)
            .header("User-Agent", "MDL-Mobile/0.3")
            .build()

        val json = client.newCall(request).execute().use { response ->
            if (!response.isSuccessful) throw IOException("HTTP ${response.code}")
            response.body?.string() ?: throw IOException("Empty body")
        }

        val type = object : TypeToken<List<GitHubRelease>>() {}.type
        Gson().fromJson(json, type) ?: emptyList()
    }

    companion object {
        fun scanMods(modsDir: String): List<ModInfo> {
            val list = mutableListOf<ModInfo>()
            val dir = java.io.File(modsDir)
            if (!dir.exists() || !dir.isDirectory) return list

            dir.listFiles()?.filter {
                it.extension.equals("jar", ignoreCase = true) || it.extension.equals("zip", ignoreCase = true)
            }?.forEach { file ->
                val info = ModInfo(
                    fileName = file.name,
                    fullPath = file.absolutePath,
                    fileSize = "%.2f KB".format(file.length() / 1024.0)
                )
                parseModArchive(info)
                list.add(info)
            }
            return list
        }

        fun parseModArchive(info: ModInfo) {
            try {
                ZipFile(info.fullPath).use { zip ->
                    // Extract icon
                    zip.getEntry("icon.png")?.let { iconEntry ->
                        zip.getInputStream(iconEntry).use { input ->
                            val baos = ByteArrayOutputStream()
                            input.copyTo(baos)
                            info.iconPngBytes = baos.toByteArray()
                        }
                    }

                    // Parse mod.json or mod.hjson
                    val metaEntry = zip.getEntry("mod.json") ?: zip.getEntry("mod.hjson")
                    metaEntry?.let {
                        zip.getInputStream(it).use { input ->
                            val content = input.bufferedReader().readText()
                            try {
                                val json = Gson().fromJson(content, Map::class.java)
                                info.displayName = stripColors((json["displayName"] as? String) ?: (json["name"] as? String) ?: "")
                                info.author = stripColors((json["author"] as? String) ?: "")
                                info.description = stripColors((json["description"] as? String) ?: "")
                                info.version = stripColors((json["version"] as? String) ?: "")
                            } catch (_: Exception) {
                                info.displayName = stripColors(extractHjsonValue(content, "displayName") ?: extractHjsonValue(content, "name") ?: "")
                                info.author = stripColors(extractHjsonValue(content, "author") ?: "")
                                info.description = stripColors(extractHjsonValue(content, "description") ?: "").replace("\\n", "\n")
                                info.version = stripColors(extractHjsonValue(content, "version") ?: "")
                            }
                        }
                    }
                }
            } catch (e: Exception) {
                Log.e("MDL", "Failed to parse mod archive: ${e.message}")
            }
        }

        fun extractHjsonValue(content: String, key: String): String? {
            val pattern = Pattern.compile("\"?$key\"?\\s*:\\s*([^\"\\r\\n]+|\"([^\"]*)\")", Pattern.CASE_INSENSITIVE)
            val matcher = pattern.matcher(content)
            if (matcher.find()) {
                return (matcher.group(2) ?: matcher.group(1))?.trimEnd(',')?.trim()
            }
            return null
        }

        fun stripColors(input: String): String {
            if (input.isEmpty()) return ""
            return input.replace(Regex("\\[.*?\\]"), "")
        }
    }
}

// ===== SchematicService =====
class SchematicService {
    var currentRepo: String = "MinRi2/schematics-archives"
    var currentBranch: String = "master"
    var allOnlineSchematics: List<SchematicEntry> = emptyList()
    var selectedSchematicToInstall: SchematicEntry? = null
    var fetchJob: Job? = null

    private val client = OkHttpClient.Builder()
        .connectTimeout(60, TimeUnit.SECONDS)
        .readTimeout(120, TimeUnit.SECONDS)
        .build()

    fun getCacheZipPath(cacheDir: String): String {
        java.io.File(cacheDir).mkdirs()
        return java.io.File(cacheDir, "${currentRepo.replace("/", "_")}.zip").absolutePath
    }

    suspend fun downloadRepoZip(zipPath: String) = withContext(Dispatchers.IO) {
        // codeload.github.com 直连 — 国内通常可访问，不走代理
        val zipUrl = "https://codeload.github.com/$currentRepo/zip/refs/heads/$currentBranch"
        val request = Request.Builder().url(zipUrl).build()

        client.newCall(request).execute().use { response ->
            if (!response.isSuccessful) throw IOException("HTTP ${response.code}")
            response.body?.byteStream()?.use { input ->
                java.io.File(zipPath).outputStream().use { output ->
                    input.copyTo(output)
                }
            } ?: throw IOException("Empty body")
        }
    }

    companion object {
        fun parseSchematicsFromZip(zipPath: String): List<SchematicEntry> {
            val list = mutableListOf<SchematicEntry>()
            ZipFile(zipPath).use { zip ->
                zip.entries().asSequence().forEach { entry ->
                    if (entry.name.endsWith(".msch", ignoreCase = true)) {
                        zip.getInputStream(entry).use { input ->
                            val bytes = input.readBytes()
                            val desc = StringBuilder()
                            val realName = parseMschName(bytes, desc)
                            val displayName = realName ?: ""
                            val description = desc.toString()
                            list.add(SchematicEntry(displayName, description, java.io.File(entry.name).name, entry.name))
                        }
                    }
                }
            }
            return list
        }

        fun parseMschName(mschBytes: ByteArray, description: StringBuilder): String? {
            try {
                val buf = ByteBuffer.wrap(mschBytes)

                if (buf.get() != 'm'.code.toByte() || buf.get() != 's'.code.toByte()
                    || buf.get() != 'c'.code.toByte() || buf.get() != 'h'.code.toByte())
                    return null

                buf.get() // version
                buf.position(buf.position() + 2) // skip 2 bytes

                val deflated = ByteArrayOutputStream()
                DeflaterInputStream(ByteArrayInputStream(mschBytes, buf.position(), mschBytes.size - buf.position())).use { it.copyTo(deflated) }

                val data = ByteBuffer.wrap(deflated.toByteArray()).order(ByteOrder.BIG_ENDIAN)

                fun readShort(): Int = ((data.get().toInt() and 0xFF) shl 8) or (data.get().toInt() and 0xFF)
                fun readString(): String {
                    val len = readShort()
                    val bytes = ByteArray(len)
                    data.get(bytes)
                    return String(bytes, Charsets.UTF_8)
                }

                readShort() // width
                readShort() // height
                val tagsCount = data.get().toInt() and 0xFF
                var foundName: String? = null

                for (i in 0 until tagsCount) {
                    val key = readString()
                    val value = readString()
                    if (key == "name") foundName = stripColors(value)
                    if (key == "description") description.append(stripColors(value))
                }

                return foundName
            } catch (e: Exception) {
                return null
            }
        }

        private fun stripColors(input: String): String {
            if (input.isEmpty()) return ""
            return input.replace(Regex("\\[.*?\\]"), "")
        }
    }
}
