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
import io.colorgarden.mdl.data.service.L
import io.colorgarden.mdl.data.model.SchematicEntry
import io.colorgarden.mdl.data.service.SchematicService
import kotlinx.coroutines.launch

@Composable
fun SchematicsScreen(container: AppContainer) {
    val langVer by L.langVersion.collectAsState()
    val schematics = remember { mutableStateListOf<SchematicEntry>() }
    val isLoading = remember { mutableStateOf(false) }
    val statusText = remember { mutableStateOf("") }
    val scope = rememberCoroutineScope()

    fun fetchSchematics() {
        scope.launch {
            isLoading.value = true
            statusText.value = L.get("schematics.fetching_zip")
            try {
                val cacheDir = java.io.File(container.configService.getConfig().managedFolders.firstOrNull() ?: "", "cache").absolutePath
                val zipPath = container.schematicService.getCacheZipPath(cacheDir)
                if (!java.io.File(zipPath).exists()) {
                    container.schematicService.downloadRepoZip(zipPath)
                }
                val list = SchematicService.parseSchematicsFromZip(zipPath)
                schematics.clear()
                schematics.addAll(list)
                statusText.value = "${list.size} schematic(s)"
            } catch (e: Exception) {
                statusText.value = L.t("schematics.fetch_error", e.message ?: "")
            }
            isLoading.value = false
        }
    }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(horizontal = 16.dp, vertical = 12.dp)
    ) {
        // Source selector
        var sourceExpanded by remember { mutableStateOf(false) }
        val sourceLabel = container.schematicService.currentRepo.let {
            if (it.contains("MinRi")) L.get("schematics.source_minri") else L.get("schematics.source_designit")
        }

        Card(
            modifier = Modifier.fillMaxWidth(),
            colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
        ) {
            Column(
                modifier = Modifier.padding(16.dp),
                horizontalAlignment = Alignment.CenterHorizontally
            ) {
                Text(
                    L.get("schematics.source_title"),
                    fontSize = 18.sp,
                    fontWeight = FontWeight.Bold,
                    color = MaterialTheme.colorScheme.onSurface
                )
                Spacer(modifier = Modifier.height(8.dp))
                Button(onClick = { fetchSchematics() }) {
                    Text(L.get("schematics.fetch"))
                }
                if (isLoading.value) {
                    Spacer(modifier = Modifier.height(8.dp))
                    LinearProgressIndicator(modifier = Modifier.fillMaxWidth())
                }
            }
        }

        Spacer(modifier = Modifier.height(12.dp))

        // Schematic list
        LazyColumn(
            verticalArrangement = Arrangement.spacedBy(8.dp)
        ) {
            if (schematics.isEmpty() && !isLoading.value) {
                item {
                    Text(
                        text = statusText.value.ifEmpty { L.get("schematics.fetch") },
                        fontSize = 14.sp,
                        color = MaterialTheme.colorScheme.outline,
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(24.dp),
                        textAlign = TextAlign.Center
                    )
                }
            } else {
                items(schematics.size) { index ->
                    val s = schematics[index]
                    Card(
                        modifier = Modifier.fillMaxWidth(),
                        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
                    ) {
                        Column(modifier = Modifier.padding(12.dp)) {
                            Text(
                                s.uiName,
                                fontSize = 15.sp,
                                fontWeight = FontWeight.Bold,
                                color = MaterialTheme.colorScheme.onSurface
                            )
                            Text(
                                s.uiDescription,
                                fontSize = 12.sp,
                                color = MaterialTheme.colorScheme.outline,
                                maxLines = 2
                            )
                        }
                    }
                }
            }

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
}
