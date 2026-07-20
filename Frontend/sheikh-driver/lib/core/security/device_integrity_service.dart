import 'dart:io';
import 'package:crypto/crypto.dart';
import 'package:device_info_plus/device_info_plus.dart';
import 'package:flutter/foundation.dart';
import 'package:package_info_plus/package_info_plus.dart';
import '../config/app_config.dart';

class DeviceIntegrityReport {
  const DeviceIntegrityReport({
    required this.isEmulator,
    required this.isRooted,
    required this.isJailbroken,
    required this.isTampered,
    required this.pinningConfigured,
    required this.deviceId,
    required this.platform,
    required this.model,
    required this.osVersion,
    required this.appVersion,
    required this.packageName,
    required this.installerStore,
    this.issues = const [],
  });

  final bool isEmulator;
  final bool isRooted;
  final bool isJailbroken;
  final bool isTampered;
  final bool pinningConfigured;
  final String deviceId;
  final String platform;
  final String model;
  final String osVersion;
  final String appVersion;
  final String packageName;
  final String? installerStore;
  final List<String> issues;

  bool get isCompromised => isRooted || isJailbroken;
  bool get hasBlockingIssue {
    if (AppConfig.blockCompromisedDevices && isCompromised) return true;
    if (AppConfig.blockEmulators && isEmulator) return true;
    if (!kDebugMode && AppConfig.isProd && isTampered) return true;
    return false;
  }

  Map<String, dynamic> toRegistrationPayload() => {
        'deviceId': deviceId,
        'platform': platform,
        'model': model,
        'osVersion': osVersion,
        'appVersion': appVersion,
        'packageName': packageName,
        'installerStore': installerStore,
        'isEmulator': isEmulator,
        'isRooted': isRooted,
        'isJailbroken': isJailbroken,
        'isTampered': isTampered,
        'pinningConfigured': pinningConfigured,
      };
}

class DeviceIntegrityService {
  DeviceIntegrityService._();
  static final instance = DeviceIntegrityService._();

  DeviceIntegrityReport? _cached;

  Future<DeviceIntegrityReport> evaluate({bool force = false}) async {
    if (_cached != null && !force) return _cached!;

    final info = DeviceInfoPlugin();
    final package = await PackageInfo.fromPlatform();

    var isEmulator = false;
    var isRooted = false;
    var isJailbroken = false;
    var deviceId = 'unknown';
    var platform = Platform.operatingSystem;
    var model = 'unknown';
    var osVersion = 'unknown';
    String? installer;

    if (Platform.isAndroid) {
      final a = await info.androidInfo;
      isEmulator = !a.isPhysicalDevice || _androidLooksLikeEmulator(a);
      isRooted = await _androidRootIndicators();
      deviceId = a.id;
      platform = 'android';
      model = '${a.manufacturer} ${a.model}'.trim();
      osVersion = 'Android ${a.version.release} (SDK ${a.version.sdkInt})';
    } else if (Platform.isIOS) {
      final i = await info.iosInfo;
      isEmulator = !i.isPhysicalDevice;
      isJailbroken = await _iosJailbreakIndicators();
      deviceId = i.identifierForVendor ?? i.name;
      platform = 'ios';
      model = i.utsname.machine;
      osVersion = '${i.systemName} ${i.systemVersion}';
    }

    final packageName = package.packageName;
    final issues = <String>[];
    var isTampered = false;
    installer = package.installerStore;

    if (packageName != AppConfig.expectedPackageId) {
      isTampered = true;
      issues.add('Unexpected package id: $packageName');
    }

    // Empty / unexpected signing metadata in release is suspicious.
    if (!kDebugMode &&
        AppConfig.isProd &&
        (package.buildSignature.isEmpty) &&
        Platform.isAndroid) {
      // Some stores omit signature — treat as soft signal only when sideloaded.
      if (installer != null &&
          installer.isNotEmpty &&
          !installer.contains('vending') &&
          !installer.contains('amazon') &&
          !installer.contains('huawei')) {
        isTampered = true;
        issues.add('Unrecognized installer: $installer');
      }
    }

    if (Platform.isAndroid && installer != null && installer.isNotEmpty) {
      final storeOk = installer.contains('vending') ||
          installer.contains('amazon') ||
          installer.contains('huawei') ||
          installer.contains('packageinstaller') ||
          kDebugMode ||
          !AppConfig.isProd;
      if (!storeOk) {
        isTampered = true;
        if (!issues.any((e) => e.contains('installer'))) {
          issues.add('Unrecognized installer: $installer');
        }
      }
    }

    if (isEmulator) issues.add('Running on emulator/simulator');
    if (isRooted) issues.add('Root indicators detected');
    if (isJailbroken) issues.add('Jailbreak indicators detected');

    final pinningConfigured = AppConfig.certFingerprints.isNotEmpty;

    _cached = DeviceIntegrityReport(
      isEmulator: isEmulator,
      isRooted: isRooted,
      isJailbroken: isJailbroken,
      isTampered: isTampered,
      pinningConfigured: pinningConfigured,
      deviceId: deviceId,
      platform: platform,
      model: model,
      osVersion: osVersion,
      appVersion: '${package.version}+${package.buildNumber}',
      packageName: packageName,
      installerStore: installer,
      issues: issues,
    );
    return _cached!;
  }

  static bool _androidLooksLikeEmulator(AndroidDeviceInfo a) {
    final brand = a.brand.toLowerCase();
    final device = a.device.toLowerCase();
    final product = a.product.toLowerCase();
    final model = a.model.toLowerCase();
    final hardware = a.hardware.toLowerCase();
    final fingerprint = a.fingerprint.toLowerCase();
    return fingerprint.contains('generic') ||
        fingerprint.contains('emulator') ||
        model.contains('google_sdk') ||
        model.contains('emulator') ||
        model.contains('android sdk built for') ||
        manufacturerIsGoogleAndSdk(brand, device) ||
        product.contains('sdk') ||
        product.contains('emulator') ||
        hardware.contains('goldfish') ||
        hardware.contains('ranchu') ||
        device.startsWith('generic');
  }

  static bool manufacturerIsGoogleAndSdk(String brand, String device) =>
      brand == 'google' && device.startsWith('generic');

  static Future<bool> _androidRootIndicators() async {
    const paths = [
      '/system/app/Superuser.apk',
      '/sbin/su',
      '/system/bin/su',
      '/system/xbin/su',
      '/data/local/xbin/su',
      '/data/local/bin/su',
      '/system/sd/xbin/su',
      '/system/bin/failsafe/su',
      '/data/local/su',
      '/su/bin/su',
      '/magisk',
      '/sbin/.magisk',
    ];
    for (final p in paths) {
      try {
        if (await File(p).exists()) return true;
      } catch (_) {}
    }
    // Build tags often "test-keys" on rooted/custom ROMs.
    try {
      final a = await DeviceInfoPlugin().androidInfo;
      if (a.tags.toLowerCase().contains('test-keys')) return true;
    } catch (_) {}
    return false;
  }

  static Future<bool> _iosJailbreakIndicators() async {
    const paths = [
      '/Applications/Cydia.app',
      '/Library/MobileSubstrate/MobileSubstrate.dylib',
      '/bin/bash',
      '/usr/sbin/sshd',
      '/etc/apt',
      '/private/var/lib/apt/',
      '/private/var/lib/cydia',
      '/private/var/stash',
      '/usr/libexec/sftp-server',
      '/Applications/FakeCarrier.app',
      '/Applications/Icy.app',
      '/Applications/IntelliScreen.app',
      '/Applications/SBSettings.app',
    ];
    for (final p in paths) {
      try {
        if (await File(p).exists()) return true;
      } catch (_) {}
    }
    // Can we write outside sandbox?
    try {
      final f = File('/private/jailbreak_probe_${DateTime.now().millisecondsSinceEpoch}');
      await f.writeAsString('probe');
      await f.delete();
      return true;
    } catch (_) {}
    return false;
  }

  /// Stable hash used as a soft device fingerprint for registration.
  static String fingerprintHash(DeviceIntegrityReport r) {
    final raw =
        '${r.deviceId}|${r.platform}|${r.packageName}|${r.model}|${r.osVersion}';
    return sha256.convert(raw.codeUnits).toString();
  }
}
