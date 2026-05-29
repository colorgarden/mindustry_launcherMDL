package io.colorgarden.mdl.service

import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.content.Intent
import android.net.VpnService
import android.os.ParcelFileDescriptor
import io.colorgarden.mdl.MainActivity

class EasyTierVpnService : VpnService() {
    private var tunFd: ParcelFileDescriptor? = null

    override fun onCreate() {
        super.onCreate()
        instance = this
        createNotificationChannel()
        startForeground(NOTIFY_ID, buildNotification("EasyTier VPN starting..."))
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        if (intent?.action == "STOP") {
            stop()
            return START_NOT_STICKY
        }

        val prefix = intent?.getStringExtra("vpn_prefix") ?: "10.144.144.0"
        val mtu = intent?.getIntExtra("vpn_mtu", 1400) ?: 1400

        try {
            tunFd?.close()
            tunFd = Builder()
                .setSession("MDL EasyTier VPN")
                .addAddress(prefix, 24)
                .addRoute("0.0.0.0", 0)
                .addDnsServer("223.5.5.5")
                .addDnsServer("8.8.8.8")
                .setMtu(mtu)
                .addDisallowedApplication(packageName)
                .setBlocking(true)
                .establish()
                ?: run {
                    EasyTierManager.onVpnStartFailed("Failed to establish VPN interface")
                    return START_NOT_STICKY
                }

            val fd = tunFd!!.fd
            EasyTierJni.load()
            if (EasyTierJni.isAvailable) {
                EasyTierJni.setTunFd(fd)
                EasyTierManager.onTunReady()
            } else {
                EasyTierManager.onVpnStartFailed("EasyTier native library not available")
                stop()
                return START_NOT_STICKY
            }
        } catch (e: Exception) {
            EasyTierManager.onVpnStartFailed(e.message ?: "VPN start failed")
            return START_NOT_STICKY
        }

        updateNotification("EasyTier VPN active")

        return START_STICKY
    }

    fun updateNotification(text: String) {
        val nm = getSystemService(NOTIFICATION_SERVICE) as NotificationManager
        nm.notify(NOTIFY_ID, buildNotification(text))
    }

    fun stop() {
        tunFd?.close()
        tunFd = null
        EasyTierJni.stopNetworkInstance()
        stopForeground(STOP_FOREGROUND_REMOVE)
        stopSelf()
        instance = null
    }

    override fun onDestroy() {
        tunFd?.close()
        tunFd = null
        instance = null
        super.onDestroy()
    }

    private fun buildNotification(text: String): Notification {
        val intent = Intent(this, MainActivity::class.java)
        val pi = PendingIntent.getActivity(this, 0, intent, PendingIntent.FLAG_IMMUTABLE)
        return Notification.Builder(this, CHANNEL_ID)
            .setContentTitle("MDL EasyTier")
            .setContentText(text)
            .setSmallIcon(android.R.drawable.ic_menu_share)
            .setContentIntent(pi)
            .setOngoing(true)
            .build()
    }

    private fun createNotificationChannel() {
        val channel = NotificationChannel(
            CHANNEL_ID,
            "EasyTier VPN",
            NotificationManager.IMPORTANCE_LOW
        )
        val nm = getSystemService(NOTIFICATION_SERVICE) as NotificationManager
        nm.createNotificationChannel(channel)
    }

    companion object {
        private const val CHANNEL_ID = "easytier_vpn"
        private const val NOTIFY_ID = 3001
        var instance: EasyTierVpnService? = null
            private set
    }
}
