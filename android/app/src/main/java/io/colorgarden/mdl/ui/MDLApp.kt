package io.colorgarden.mdl.ui

import androidx.compose.animation.*
import androidx.compose.foundation.layout.*
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.Settings
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import io.colorgarden.mdl.MDLApplication
import io.colorgarden.mdl.data.service.L
import io.colorgarden.mdl.ui.screens.*
import io.colorgarden.mdl.ui.theme.MDLTheme

enum class Page { LAUNCH, DOWNLOAD, MODS, SCHEMATICS, MULTIPLAYER, SETTINGS }

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun MDLApp(app: MDLApplication) {
    // Theme mode: 0=auto, 1=light, 2=dark
    val darkMode = app.container.configService.getConfig().darkMode
    val isDark = when (darkMode) {
        1 -> false
        2 -> true
        else -> androidx.compose.foundation.isSystemInDarkTheme()
    }

    MDLTheme(darkTheme = isDark) {
        var currentPage by remember { mutableStateOf(Page.LAUNCH) }
        val langVer by L.langVersion.collectAsState()

        Scaffold(
            containerColor = MaterialTheme.colorScheme.background,
            topBar = {
                TopAppBar(
                    title = { Text(L.get("app.title")) },
                    navigationIcon = {
                        if (currentPage != Page.LAUNCH) {
                            IconButton(onClick = { currentPage = Page.LAUNCH }) {
                                Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Back")
                            }
                        }
                    },
                    actions = {
                        if (currentPage == Page.LAUNCH) {
                            IconButton(onClick = { currentPage = Page.SETTINGS }) {
                                Icon(Icons.Filled.Settings, contentDescription = "Settings")
                            }
                        }
                    },
                    colors = TopAppBarDefaults.topAppBarColors(
                        containerColor = MaterialTheme.colorScheme.primary,
                        titleContentColor = MaterialTheme.colorScheme.onPrimary,
                        navigationIconContentColor = MaterialTheme.colorScheme.onPrimary,
                        actionIconContentColor = MaterialTheme.colorScheme.onPrimary,
                    )
                )
            }
        ) { innerPadding ->
            Box(modifier = Modifier.padding(innerPadding)) {
                when (currentPage) {
                    Page.LAUNCH -> LaunchScreen(
                        container = app.container,
                        onNavigate = { currentPage = it }
                    )
                    Page.DOWNLOAD -> DownloadScreen(app.container)
                    Page.MODS -> ModsScreen(app.container)
                    Page.SCHEMATICS -> SchematicsScreen(app.container)
                    Page.MULTIPLAYER -> MultiplayerScreen(app.container)
                    Page.SETTINGS -> SettingsScreen(app.container)
                }
            }
        }
    }
}
