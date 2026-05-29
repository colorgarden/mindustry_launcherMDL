package io.colorgarden.mdl.ui.screens

import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import io.colorgarden.mdl.AppContainer
import io.colorgarden.mdl.data.service.L as MDLLang
import io.colorgarden.mdl.data.model.ModRegistryEntry
import io.colorgarden.mdl.data.service.ModService
import kotlinx.coroutines.launch

@Composable
fun ModsScreen(container: AppContainer) {
    val langVer by MDLLang.langVersion.collectAsState()
    val mods = remember { mutableStateListOf<ModRegistryEntry>() }
    val isLoading = remember { mutableStateOf(false) }
    val statusText = remember { mutableStateOf("") }
    val scope = rememberCoroutineScope()

    fun refreshMods() {
        scope.launch {
            isLoading.value = true
            statusText.value = MDLLang.get("mods.fetching")
            try {
                val list = container.modService.fetchModRegistry()
                mods.clear()
                mods.addAll(list)
                statusText.value = "${mods.size} mod(s)"
            } catch (e: Exception) {
                statusText.value = MDLLang.t("mods.fetch_error", e.message ?: "")
            }
            isLoading.value = false
        }
    }

    LaunchedEffect(Unit) { refreshMods() }

    LazyColumn(
        modifier = Modifier
            .fillMaxSize()
            .padding(horizontal = 16.dp, vertical = 12.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp)
    ) {
        // Header
        item {
            Card(
                modifier = Modifier.fillMaxWidth(),
                colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
            ) {
                Column(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(16.dp),
                    horizontalAlignment = Alignment.CenterHorizontally
                ) {
                    Text(
                        MDLLang.get("mods.browser_title"),
                        fontSize = 18.sp,
                        fontWeight = FontWeight.Bold,
                        color = MaterialTheme.colorScheme.onSurface
                    )
                    Spacer(modifier = Modifier.height(10.dp))
                    Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                        Button(onClick = { refreshMods() }) {
                            Text(MDLLang.get("mods.refresh"))
                        }
                    }
                    if (isLoading.value) {
                        Spacer(modifier = Modifier.height(8.dp))
                        LinearProgressIndicator(modifier = Modifier.fillMaxWidth())
                    }
                }
            }
        }

        // Mod list
        if (mods.isEmpty() && !isLoading.value) {
            item {
                Text(
                    text = statusText.value.ifEmpty { MDLLang.get("mods.fetching") },
                    fontSize = 14.sp,
                    color = MaterialTheme.colorScheme.outline,
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(24.dp),
                    textAlign = TextAlign.Center
                )
            }
        } else {
            items(mods.size) { index ->
                val mod = mods[index]
                Card(
                    modifier = Modifier.fillMaxWidth(),
                    colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
                ) {
                    Column(modifier = Modifier.padding(12.dp)) {
                        Text(
                            mod.displayName.ifEmpty { mod.name },
                            fontSize = 15.sp,
                            fontWeight = FontWeight.Bold,
                            color = MaterialTheme.colorScheme.onSurface
                        )
                        Text(
                            mod.authorFormatted,
                            fontSize = 12.sp,
                            color = MaterialTheme.colorScheme.outline
                        )
                        Text(
                            "${MDLLang.get("mods.install_select_version")} ${mod.starsFormatted}",
                            fontSize = 12.sp,
                            color = MaterialTheme.colorScheme.outline
                        )
                    }
                }
            }
        }

        // Status
        item {
            Text(
                text = statusText.value,
                fontSize = 12.sp,
                color = MaterialTheme.colorScheme.outline,
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(bottom = 16.dp),
                textAlign = TextAlign.Center
            )
        }
    }
}
