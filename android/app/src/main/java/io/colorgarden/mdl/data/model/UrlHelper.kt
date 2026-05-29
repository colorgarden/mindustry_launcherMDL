package io.colorgarden.mdl.data.model

object UrlHelper {
    var proxyIndex: Int = 1

    /**
     * Format URL for network access.
     * API calls (api.github.com) go direct — usually accessible in China.
     * Download/raw URLs are proxied for speed in China.
     */
    fun format(url: String, isApi: Boolean = false): String {
        // API calls go direct: api.github.com is usually accessible
        if (isApi) return url

        return when (proxyIndex) {
            0 -> url
            1 -> "https://ghfast.top/$url"
            2 -> "https://gh-proxy.com/$url"
            3 -> when {
                url.startsWith("https://github.com") -> url.replace("https://github.com", "https://kkgithub.com")
                url.startsWith("https://raw.githubusercontent.com") -> url.replace("https://raw.githubusercontent.com", "https://raw.kkgithub.com")
                else -> "https://kkgithub.com/$url"
            }
            4 -> if (url.startsWith("https://raw.githubusercontent.com/"))
                url.replace("https://raw.githubusercontent.com/", "https://cdn.jsdelivr.net/gh/")
                    .replace("/master/", "@master/").replace("/main/", "@main/")
            else url
            5 -> "https://gh.llkk.cc/$url"
            else -> url
        }
    }
}
