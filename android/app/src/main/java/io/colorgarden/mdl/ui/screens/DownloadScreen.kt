package io.colorgarden.mdl.ui.screens

import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.itemsIndexed
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.lifecycle.viewmodel.compose.viewModel
import io.colorgarden.mdl.AppContainer
import io.colorgarden.mdl.data.model.GitHubRelease
import io.colorgarden.mdl.data.service.L
import io.colorgarden.mdl.viewmodel.DownloadViewModel

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun DownloadScreen(container: AppContainer) {
    val vm: DownloadViewModel = viewModel { DownloadViewModel(container) }
    val releases by vm.releases.collectAsState()
    val isLoading by vm.isLoading.collectAsState()
    val statusText by vm.statusText.collectAsState()
    val downloadProgress by vm.downloadProgress.collectAsState()
    val showProgress by vm.showProgress.collectAsState()
    val repo by vm.repo.collectAsState()
    val langVer by L.langVersion.collectAsState()

    LaunchedEffect(Unit) { vm.fetchReleases() }

    LazyColumn(
        modifier = Modifier
            .fillMaxSize()
            .padding(horizontal = 16.dp, vertical = 12.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp)
    ) {
        // Source selector
        item {
            Card(
                modifier = Modifier.fillMaxWidth(),
                colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
            ) {
                Column(
                    modifier = Modifier.padding(16.dp),
                    horizontalAlignment = Alignment.CenterHorizontally
                ) {
                    Text(
                        L.get("download.source_title"),
                        fontSize = 18.sp,
                        fontWeight = FontWeight.Bold,
                        color = MaterialTheme.colorScheme.onSurface
                    )
                    Spacer(modifier = Modifier.height(12.dp))

                    val sources = listOf(
                        "Anuken/Mindustry" to L.get("download.official"),
                        "TinyLake/MindustryX" to L.get("download.x_client"),
                        "mindustry-antigrief/mindustry-client-v8-builds" to L.get("download.foo_client")
                    )
                    var expanded by remember { mutableStateOf(false) }
                    val currentLabel = sources.find { it.first == repo }?.second ?: repo

                    ExposedDropdownMenuBox(
                        expanded = expanded,
                        onExpandedChange = { expanded = it }
                    ) {
                        OutlinedTextField(
                            value = currentLabel,
                            onValueChange = {},
                            readOnly = true,
                            modifier = Modifier.menuAnchor(MenuAnchorType.PrimaryNotEditable, enabled = true).fillMaxWidth(),
                            trailingIcon = { ExposedDropdownMenuDefaults.TrailingIcon(expanded = expanded) }
                        )
                        ExposedDropdownMenu(
                            expanded = expanded,
                            onDismissRequest = { expanded = false }
                        ) {
                            sources.forEach { (r, label) ->
                                DropdownMenuItem(
                                    text = { Text(label) },
                                    onClick = {
                                        vm.setRepo(r)
                                        expanded = false
                                        vm.fetchReleases()
                                    }
                                )
                            }
                        }
                    }

                    if (!isLoading) {
                        Spacer(modifier = Modifier.height(8.dp))
                        OutlinedButton(onClick = { vm.fetchReleases() }) {
                            Text("🔄 " + L.get("mods.refresh"))
                        }
                    }
                }
            }
        }

        // Progress bar
        if (showProgress) {
            item {
                Card(
                    modifier = Modifier.fillMaxWidth(),
                    colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
                ) {
                    Column(modifier = Modifier.padding(16.dp)) {
                        Text(
                            L.get("download.preparing"),
                            fontSize = 14.sp,
                            fontWeight = FontWeight.Bold
                        )
                        Spacer(modifier = Modifier.height(8.dp))
                        LinearProgressIndicator(
                            progress = { (downloadProgress / 100f).toFloat() },
                            modifier = Modifier.fillMaxWidth()
                        )
                        Text(
                            "%.1f%%".format(downloadProgress),
                            fontSize = 12.sp,
                            modifier = Modifier.fillMaxWidth(),
                            textAlign = TextAlign.Center
                        )
                    }
                }
            }
        }

        // Loading indicator
        if (isLoading) {
            item {
                LinearProgressIndicator(modifier = Modifier.fillMaxWidth())
            }
        }

        // Release list
        itemsIndexed(releases) { _, release ->
            Card(
                modifier = Modifier.fillMaxWidth(),
                colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
            ) {
                Column(modifier = Modifier.padding(12.dp)) {
                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.SpaceBetween,
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Text(
                            release.tagName,
                            fontSize = 16.sp,
                            fontWeight = FontWeight.Bold,
                            color = MaterialTheme.colorScheme.onSurface,
                            modifier = Modifier.weight(1f)
                        )
                        if (release.prerelease) {
                            AssistChip(onClick = {}, label = { Text("Pre", fontSize = 10.sp) })
                        }
                    }

                    release.name.takeIf { it.isNotEmpty() && it != release.tagName }?.let {
                        Text(it, fontSize = 13.sp, color = MaterialTheme.colorScheme.outline)
                    }

                    Spacer(modifier = Modifier.height(8.dp))
                    Button(
                        onClick = { vm.downloadRelease(release) },
                        modifier = Modifier.fillMaxWidth()
                    ) {
                        Text(L.get("download.download"))
                    }
                }
            }
        }

        // Status
        item {
            Text(
                text = statusText,
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
