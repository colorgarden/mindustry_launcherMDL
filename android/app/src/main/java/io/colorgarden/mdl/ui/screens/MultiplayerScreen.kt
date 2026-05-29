package io.colorgarden.mdl.ui.screens

import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ExitToApp
import androidx.compose.material.icons.automirrored.filled.Login
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.CheckCircle
import androidx.compose.material.icons.filled.Close
import androidx.compose.material.icons.filled.Warning
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.lifecycle.viewmodel.compose.viewModel
import io.colorgarden.mdl.AppContainer
import io.colorgarden.mdl.data.service.L
import io.colorgarden.mdl.service.EasyTierManager
import io.colorgarden.mdl.viewmodel.MultiplayerViewModel

@Composable
fun MultiplayerScreen(container: AppContainer) {
    val vm: MultiplayerViewModel = viewModel { MultiplayerViewModel(container) }
    val state by EasyTierManager.state.collectAsState()
    val statusText by vm.statusText.collectAsState()
    val nickname by vm.nickname.collectAsState()
    val roomCode by vm.roomCode.collectAsState()
    val langVer by L.langVersion.collectAsState()

    val nativeAvailable = remember { EasyTierManager.isNativeAvailable() }

    LazyColumn(
        modifier = Modifier
            .fillMaxSize()
            .padding(horizontal = 16.dp, vertical = 12.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp)
    ) {
        // Header with VPN status
        item {
            Card(
                modifier = Modifier.fillMaxWidth(),
                colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
            ) {
                Column(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(20.dp),
                    horizontalAlignment = Alignment.CenterHorizontally
                ) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Icon(
                            if (state.running) Icons.Filled.CheckCircle else Icons.Filled.Close,
                            contentDescription = null,
                            tint = if (state.running) MaterialTheme.colorScheme.primary
                                   else MaterialTheme.colorScheme.outline,
                            modifier = Modifier.size(24.dp)
                        )
                        Spacer(modifier = Modifier.width(8.dp))
                        Text(
                            L.get("multiplayer.title"),
                            fontSize = 20.sp,
                            fontWeight = FontWeight.Bold,
                            color = MaterialTheme.colorScheme.onSurface
                        )
                    }
                    if (!nativeAvailable) {
                        Spacer(modifier = Modifier.height(8.dp))
                        AssistChip(
                            onClick = {},
                            label = { Text("Native lib missing", fontSize = 10.sp) },
                            leadingIcon = {
                                Icon(Icons.Filled.Warning, null, Modifier.size(16.dp))
                            }
                        )
                    }
                }
            }
        }

        // Nickname
        item {
            Card(
                modifier = Modifier.fillMaxWidth(),
                colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
            ) {
                Column(modifier = Modifier.padding(16.dp)) {
                    Text(
                        L.get("multiplayer.nickname"),
                        fontSize = 15.sp,
                        fontWeight = FontWeight.Bold,
                        color = MaterialTheme.colorScheme.onSurface
                    )
                    Spacer(modifier = Modifier.height(8.dp))
                    OutlinedTextField(
                        value = nickname,
                        onValueChange = { vm.setNickname(it) },
                        modifier = Modifier.fillMaxWidth(),
                        singleLine = true,
                        placeholder = { Text(L.get("multiplayer.default_nickname")) }
                    )
                }
            }
        }

        // Join lobby
        item {
            Card(
                modifier = Modifier.fillMaxWidth(),
                colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
            ) {
                Column(modifier = Modifier.padding(16.dp)) {
                    Text(
                        L.get("multiplayer.join_title"),
                        fontSize = 16.sp,
                        fontWeight = FontWeight.Bold,
                        color = MaterialTheme.colorScheme.onSurface
                    )
                    Spacer(modifier = Modifier.height(8.dp))
                    OutlinedTextField(
                        value = roomCode,
                        onValueChange = { vm.setRoomCode(it) },
                        modifier = Modifier.fillMaxWidth(),
                        singleLine = true,
                        enabled = !state.running,
                        placeholder = { Text(L.get("multiplayer.room_placeholder")) }
                    )
                    Spacer(modifier = Modifier.height(8.dp))
                    Button(
                        onClick = { vm.joinLobby() },
                        modifier = Modifier.fillMaxWidth(),
                        enabled = !state.running && roomCode.length == 6
                    ) {
                        Icon(Icons.AutoMirrored.Filled.Login, null, Modifier.size(18.dp))
                        Spacer(modifier = Modifier.width(8.dp))
                        Text(L.get("multiplayer.join"))
                    }
                }
            }
        }

        // Create lobby
        item {
            Card(
                modifier = Modifier.fillMaxWidth(),
                colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
            ) {
                Column(modifier = Modifier.padding(16.dp)) {
                    Text(
                        L.get("multiplayer.create_title"),
                        fontSize = 16.sp,
                        fontWeight = FontWeight.Bold,
                        color = MaterialTheme.colorScheme.onSurface
                    )
                    Spacer(modifier = Modifier.height(8.dp))
                    Button(
                        onClick = { vm.createLobby() },
                        modifier = Modifier.fillMaxWidth(),
                        enabled = !state.running && nickname.isNotEmpty()
                    ) {
                        Icon(Icons.Filled.Add, null, Modifier.size(18.dp))
                        Spacer(modifier = Modifier.width(8.dp))
                        Text(L.get("multiplayer.create"))
                    }
                }
            }
        }

        // Leave/Disband
        if (state.running) {
            item {
                Button(
                    onClick = { vm.leaveLobby() },
                    modifier = Modifier.fillMaxWidth(),
                    colors = ButtonDefaults.buttonColors(
                        containerColor = MaterialTheme.colorScheme.error
                    )
                ) {
                    Icon(Icons.AutoMirrored.Filled.ExitToApp, null, Modifier.size(18.dp))
                    Spacer(modifier = Modifier.width(8.dp))
                    Text(L.get("multiplayer.exit"))
                }
            }
        }

        // Virtual IP status
        item {
            Card(
                modifier = Modifier.fillMaxWidth(),
                colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
            ) {
                Column(modifier = Modifier.padding(16.dp)) {
                    Text(
                        L.get("multiplayer.virtual_ip"),
                        fontSize = 14.sp,
                        color = MaterialTheme.colorScheme.outline
                    )
                    Text(
                        if (state.running) state.virtualIp
                        else L.get("multiplayer.not_connected"),
                        fontSize = 18.sp,
                        fontWeight = FontWeight.Bold,
                        color = if (state.running) MaterialTheme.colorScheme.primary
                                else MaterialTheme.colorScheme.onSurface
                    )
                }
            }
        }

        // Peer list
        if (state.running && state.peers.isNotEmpty()) {
            item {
                Card(
                    modifier = Modifier.fillMaxWidth(),
                    colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
                ) {
                    Column(modifier = Modifier.padding(16.dp)) {
                        Text(
                            L.get("multiplayer.players_title"),
                            fontSize = 15.sp,
                            fontWeight = FontWeight.Bold,
                            color = MaterialTheme.colorScheme.onSurface
                        )
                        Spacer(modifier = Modifier.height(8.dp))
                        state.peers.forEach { peer ->
                            Row(
                                modifier = Modifier
                                    .fillMaxWidth()
                                    .padding(vertical = 4.dp),
                                horizontalArrangement = Arrangement.SpaceBetween
                            ) {
                                Text(peer.virtualIp, fontSize = 14.sp)
                                Text(peer.hostname, fontSize = 12.sp,
                                    color = MaterialTheme.colorScheme.outline)
                                if (peer.latencyMs > 0) {
                                    Text("${peer.latencyMs}ms", fontSize = 12.sp,
                                        color = MaterialTheme.colorScheme.outline)
                                }
                            }
                            HorizontalDivider(thickness = 0.5.dp)
                        }
                    }
                }
            }
        }

        // Error
        if (state.error.isNotEmpty()) {
            item {
                Card(
                    modifier = Modifier.fillMaxWidth(),
                    colors = CardDefaults.cardColors(
                        containerColor = MaterialTheme.colorScheme.errorContainer
                    )
                ) {
                    Text(
                        state.error,
                        fontSize = 12.sp,
                        color = MaterialTheme.colorScheme.onErrorContainer,
                        modifier = Modifier.padding(12.dp)
                    )
                }
            }
        }

        // Status text
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
