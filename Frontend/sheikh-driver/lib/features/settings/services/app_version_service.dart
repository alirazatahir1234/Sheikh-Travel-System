import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:package_info_plus/package_info_plus.dart';
import '../../../core/api/api_endpoints.dart';
import '../../../core/config/app_config.dart';

class _AppVersionInfo {
  const _AppVersionInfo({
    required this.minVersion,
    required this.latestVersion,
    required this.forceUpdate,
  });
  final String minVersion;
  final String latestVersion;
  final bool forceUpdate;
}

class AppVersionService {
  static String? _cachedVersion;

  /// Fallback when package_info is unavailable (tests).
  static String get currentVersion =>
      _cachedVersion ?? AppConfig.appVersion;

  static Future<String> displayVersion() async {
    try {
      final info = await PackageInfo.fromPlatform();
      _cachedVersion = '${info.version}+${info.buildNumber}';
      return _cachedVersion!;
    } catch (_) {
      return AppConfig.appVersion;
    }
  }

  static Future<void> checkAndPrompt(BuildContext context, Dio dio) async {
    try {
      final version = await displayVersion();
      final semantic = version.split('+').first;
      final res = await dio.get<Map<String, dynamic>>(ApiEndpoints.appVersion);
      final data = res.data;
      if (data == null) return;

      final info = _AppVersionInfo(
        minVersion: data['minVersion'] as String? ?? '1.0.0',
        latestVersion: data['latestVersion'] as String? ?? '1.0.0',
        forceUpdate: data['forceUpdate'] as bool? ?? false,
      );

      if (!context.mounted) return;

      final outdated = isOutdated(semantic, info.minVersion);
      if (outdated || info.forceUpdate) {
        await _showForceUpdateDialog(context, info.latestVersion);
        return;
      }

      final updateAvailable = isOutdated(semantic, info.latestVersion);
      if (updateAvailable) {
        _showUpdateSnackBar(context, info.latestVersion);
      }
    } catch (_) {
      // Silent — don't block the app if version check fails
    }
  }

  static Future<void> _showForceUpdateDialog(
      BuildContext context, String newVersion) {
    return showDialog(
      context: context,
      barrierDismissible: false,
      builder: (_) => AlertDialog(
        title: const Text('Update Required'),
        content: Text(
          'Version $newVersion is required to continue using SheikhGo Fleet. Please update the app from your app store.',
        ),
        actions: [
          FilledButton(
            onPressed: () {},
            child: const Text('Update Now'),
          ),
        ],
      ),
    );
  }

  static void _showUpdateSnackBar(BuildContext context, String version) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text('Version $version is available'),
        action: SnackBarAction(label: 'Update', onPressed: () {}),
      ),
    );
  }

  /// Returns true if [current] version is older than [minimum].
  static bool isOutdated(String current, String minimum) {
    final c = _parse(current);
    final m = _parse(minimum);
    for (var i = 0; i < 3; i++) {
      if (c[i] < m[i]) return true;
      if (c[i] > m[i]) return false;
    }
    return false;
  }

  static List<int> _parse(String v) {
    final parts = v.split('.');
    return List.generate(
        3, (i) => int.tryParse(i < parts.length ? parts[i] : '0') ?? 0);
  }
}
