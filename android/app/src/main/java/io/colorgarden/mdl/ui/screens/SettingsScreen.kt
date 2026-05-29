package io.colorgarden.mdl.ui.screens

import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.lifecycle.viewmodel.compose.viewModel
import io.colorgarden.mdl.AppContainer
import io.colorgarden.mdl.data.service.L
import io.colorgarden.mdl.viewmodel.SettingsViewModel

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun SettingsScreen(container: AppContainer) {
    val vm: SettingsViewModel = viewModel { SettingsViewModel(container) }
    val config by vm.config.collectAsState()
    val proxyIndex by vm.proxyIndex.collectAsState()
    val language by vm.language.collectAsState()
    val darkMode by vm.darkMode.collectAsState()
    val statusText by vm.statusText.collectAsState()
    val langVer by L.langVersion.collectAsState()

    LazyColumn(
        modifier = Modifier
            .fillMaxSize()
            .padding(horizontal = 16.dp, vertical = 12.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp)
    ) {
        // Download proxy node
        item {
            Card(
                modifier = Modifier.fillMaxWidth(),
                colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
            ) {
                Column(modifier = Modifier.padding(16.dp)) {
                    Text(
                        io.colorgarden.mdl.data.service.L.get("settings.proxy_label"),
                        fontSize = 15.sp,
                        fontWeight = FontWeight.Bold,
                        color = MaterialTheme.colorScheme.onSurface
                    )
                    Spacer(modifier = Modifier.height(8.dp))
                    for (i in 0..5) {
                        Row(
                            modifier = Modifier
                                .fillMaxWidth()
                                .padding(vertical = 4.dp),
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            RadioButton(
                                selected = proxyIndex == i,
                                onClick = { vm.setProxyIndex(i) }
                            )
                            Spacer(modifier = Modifier.width(8.dp))
                            Text(
                                io.colorgarden.mdl.data.service.L.get("settings.proxy_$i"),
                                fontSize = 13.sp,
                                color = MaterialTheme.colorScheme.onSurface
                            )
                        }
                    }
                }
            }
        }

        // Language
        item {
            Card(
                modifier = Modifier.fillMaxWidth(),
                colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
            ) {
                Column(modifier = Modifier.padding(16.dp)) {
                    Text(
                        io.colorgarden.mdl.data.service.L.get("settings.language_label"),
                        fontSize = 15.sp,
                        fontWeight = FontWeight.Bold,
                        color = MaterialTheme.colorScheme.onSurface
                    )
                    Spacer(modifier = Modifier.height(8.dp))

                    var expanded by remember { mutableStateOf(false) }
                    val selectedLabel = when (language) {
                        "zh-CN" -> io.colorgarden.mdl.data.service.L.get("settings.language_zh")
                        "en-US" -> io.colorgarden.mdl.data.service.L.get("settings.language_en")
                        else -> io.colorgarden.mdl.data.service.L.get("settings.language_auto")
                    }

                    ExposedDropdownMenuBox(
                        expanded = expanded,
                        onExpandedChange = { expanded = it }
                    ) {
                        OutlinedTextField(
                            value = selectedLabel,
                            onValueChange = {},
                            readOnly = true,
                            modifier = Modifier.menuAnchor(MenuAnchorType.PrimaryNotEditable, enabled = true).fillMaxWidth(),
                            trailingIcon = { ExposedDropdownMenuDefaults.TrailingIcon(expanded = expanded) }
                        )
                        ExposedDropdownMenu(
                            expanded = expanded,
                            onDismissRequest = { expanded = false }
                        ) {
                            DropdownMenuItem(
                                text = { Text(io.colorgarden.mdl.data.service.L.get("settings.language_auto")) },
                                onClick = { vm.setLanguage("auto"); expanded = false }
                            )
                            DropdownMenuItem(
                                text = { Text(io.colorgarden.mdl.data.service.L.get("settings.language_zh")) },
                                onClick = { vm.setLanguage("zh-CN"); expanded = false }
                            )
                            DropdownMenuItem(
                                text = { Text(io.colorgarden.mdl.data.service.L.get("settings.language_en")) },
                                onClick = { vm.setLanguage("en-US"); expanded = false }
                            )
                        }
                    }
                }
            }
        }

        // Dark mode
        item {
            Card(
                modifier = Modifier.fillMaxWidth(),
                colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
            ) {
                Column(modifier = Modifier.padding(16.dp)) {
                    Text(
                        L.get("settings.theme_label"),
                        fontSize = 15.sp,
                        fontWeight = FontWeight.Bold,
                        color = MaterialTheme.colorScheme.onSurface
                    )
                    Spacer(modifier = Modifier.height(8.dp))
                    val modes = listOf(
                        0 to L.get("settings.theme_auto"),
                        1 to L.get("settings.theme_light"),
                        2 to L.get("settings.theme_dark")
                    )
                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.SpaceEvenly
                    ) {
                        modes.forEach { (mode, label) ->
                            FilterChip(
                                selected = darkMode == mode,
                                onClick = { vm.setDarkMode(mode) },
                                label = { Text(label) }
                            )
                        }
                    }
                }
            }
        }

        // Player nickname
        item {
            Card(
                modifier = Modifier.fillMaxWidth(),
                colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
            ) {
                Column(modifier = Modifier.padding(16.dp)) {
                    Text(
                        io.colorgarden.mdl.data.service.L.get("settings.player_nickname"),
                        fontSize = 15.sp,
                        fontWeight = FontWeight.Bold,
                        color = MaterialTheme.colorScheme.onSurface
                    )
                    Spacer(modifier = Modifier.height(8.dp))
                    var nickname by remember(config) { mutableStateOf(config.playerNickname) }
                    OutlinedTextField(
                        value = nickname,
                        onValueChange = { nickname = it },
                        modifier = Modifier.fillMaxWidth(),
                        singleLine = true
                    )
                    Spacer(modifier = Modifier.height(8.dp))
                    Button(onClick = { vm.setNickname(nickname) }) {
                        Text(io.colorgarden.mdl.data.service.L.get("vsettings.confirm"))
                    }
                }
            }
        }

        // Global RAM
        item {
            Card(
                modifier = Modifier.fillMaxWidth(),
                colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
            ) {
                Column(modifier = Modifier.padding(16.dp)) {
                    Text(
                        io.colorgarden.mdl.data.service.L.get("settings.java_ram_label"),
                        fontSize = 15.sp,
                        fontWeight = FontWeight.Bold,
                        color = MaterialTheme.colorScheme.onSurface
                    )
                    Spacer(modifier = Modifier.height(8.dp))
                    var ramText by remember(config) { mutableStateOf(config.globalRamMB.toString()) }
                    OutlinedTextField(
                        value = ramText,
                        onValueChange = { ramText = it },
                        modifier = Modifier.fillMaxWidth(),
                        singleLine = true,
                        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                        label = { Text("MB") }
                    )
                    Spacer(modifier = Modifier.height(8.dp))
                    Button(onClick = { ramText.toIntOrNull()?.let { vm.setGlobalRam(it) } }) {
                        Text(io.colorgarden.mdl.data.service.L.get("vsettings.confirm"))
                    }
                }
            }
        }

        // Managed folders
        item {
            Card(
                modifier = Modifier.fillMaxWidth(),
                colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
            ) {
                Column(modifier = Modifier.padding(16.dp)) {
                    Text(
                        io.colorgarden.mdl.data.service.L.get("settings.managed_folders"),
                        fontSize = 15.sp,
                        fontWeight = FontWeight.Bold,
                        color = MaterialTheme.colorScheme.onSurface
                    )
                    Spacer(modifier = Modifier.height(8.dp))
                    config.managedFolders.forEach { folder ->
                        Row(
                            modifier = Modifier
                                .fillMaxWidth()
                                .padding(vertical = 4.dp),
                            horizontalArrangement = Arrangement.SpaceBetween,
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            Text(
                                folder,
                                fontSize = 12.sp,
                                color = MaterialTheme.colorScheme.outline,
                                modifier = Modifier.weight(1f)
                            )
                            TextButton(onClick = { vm.removeManagedFolder(folder) }) {
                                Text(io.colorgarden.mdl.data.service.L.get("settings.remove_folder"))
                            }
                        }
                        HorizontalDivider(thickness = 0.5.dp)
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
                textAlign = androidx.compose.ui.text.style.TextAlign.Center
            )
        }
    }
}
