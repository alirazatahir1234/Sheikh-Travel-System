package com.sheikhgo.fleet

import android.app.NotificationChannel
import android.app.NotificationManager
import android.os.Build
import android.os.Bundle
import io.flutter.embedding.android.FlutterFragmentActivity

/// [FlutterFragmentActivity] is required by local_auth (BiometricPrompt).
class MainActivity : FlutterFragmentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        ensureDefaultPushChannel()
        super.onCreate(savedInstanceState)
    }

    /** Android 8+ channel referenced by FCM default_notification_channel_id. */
    private fun ensureDefaultPushChannel() {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.O) return
        val manager = getSystemService(NotificationManager::class.java) ?: return
        val id = "sheikhgo_default"
        if (manager.getNotificationChannel(id) != null) return
        val channel = NotificationChannel(
            id,
            "SheikhGo Alerts",
            NotificationManager.IMPORTANCE_HIGH,
        ).apply {
            description = "Fleet alerts, trips, and GPS command updates"
            enableVibration(true)
        }
        manager.createNotificationChannel(channel)
    }
}
