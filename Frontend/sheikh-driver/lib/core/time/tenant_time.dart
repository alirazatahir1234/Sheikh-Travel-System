import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';
import 'package:intl/intl.dart';
import 'package:timezone/data/latest_10y.dart' as tzdata;
import 'package:timezone/timezone.dart' as tz;

import '../api/api_endpoints.dart';

/// Platform default (Pakistan). Mirrors backend `Localization:DefaultTimeZone`; used until the
/// company's timezone has loaded from `GET /tenants/branding` (`effectiveTimeZone`).
const String kDefaultTimeZone = 'Asia/Karachi';

/// Company / branch display timezone for GPS times.
///
/// API timestamps are UTC instants. They are shown in the company's timezone — or a vehicle's
/// branch timezone (`timeZoneId` on history/replay responses) — never the phone's own timezone,
/// so a manager travelling in Dubai still sees the Pakistan fleet in Pakistan time.
class TenantTime {
  TenantTime._();

  static bool _initialized = false;
  static String _company = kDefaultTimeZone;
  static String? _screen;

  static void ensureInitialized() {
    if (_initialized) return;
    tzdata.initializeTimeZones();
    _initialized = true;
  }

  static bool isValid(String? zoneId) {
    final id = zoneId?.trim();
    if (id == null || id.isEmpty) return false;
    ensureInitialized();
    try {
      tz.getLocation(id);
      return true;
    } catch (_) {
      return false;
    }
  }

  /// Current company zone (platform default until branding has loaded).
  static String get companyTimeZone => _company;

  /// Applies the company zone; blank or unknown ids are ignored.
  static void setCompanyTimeZone(String? zoneId) {
    if (isValid(zoneId)) _company = zoneId!.trim();
  }

  /// Zone of the vehicle on screen (history / playback `timeZoneId`); null clears it.
  /// Nested playback widgets pick it up without passing a zone through every constructor.
  static void setScreenTimeZone(String? zoneId) => _screen = isValid(zoneId) ? zoneId!.trim() : null;

  @visibleForTesting
  static void resetForTest() {
    _company = kDefaultTimeZone;
    _screen = null;
  }

  /// Explicit zone when valid, else the on-screen vehicle's zone, else the company zone.
  static String resolve(String? zoneId) => isValid(zoneId) ? zoneId!.trim() : (_screen ?? _company);

  /// Wall-clock view of [instant] in the zone. Its year/month/day/hour fields are local to the zone,
  /// so `DateFormat` formats it correctly.
  static DateTime inZone(DateTime instant, [String? zoneId]) {
    ensureInitialized();
    return tz.TZDateTime.from(instant.toUtc(), tz.getLocation(resolve(zoneId)));
  }

  static String format(DateTime instant, DateFormat format, [String? zoneId]) =>
      format.format(inZone(instant, zoneId));

  /// UTC instant for a wall-clock time in the zone (e.g. a date picked by the user).
  static DateTime fromWallTime(
    int year,
    int month,
    int day, {
    int hour = 0,
    int minute = 0,
    int second = 0,
    String? zoneId,
  }) {
    ensureInitialized();
    return tz.TZDateTime(tz.getLocation(resolve(zoneId)), year, month, day, hour, minute, second)
        .toUtc();
  }

  /// [start, endExclusive) in UTC of the zone's calendar day containing [instant].
  static (DateTime, DateTime) dayBoundsUtc(DateTime instant, [String? zoneId]) {
    final local = inZone(instant, zoneId);
    final start = fromWallTime(local.year, local.month, local.day, zoneId: zoneId);
    final end = fromWallTime(local.year, local.month, local.day + 1, zoneId: zoneId);
    return (start, end);
  }

  /// Loads `effectiveTimeZone` for the signed-in company. Failures keep the current zone.
  static Future<void> loadCompanyTimeZone(Dio dio) async {
    try {
      final res = await dio.get<Map<String, dynamic>>(ApiEndpoints.tenantBranding);
      final body = res.data;
      final data = body?['data'] ?? body?['Data'] ?? body;
      if (data is Map) {
        setCompanyTimeZone(
          (data['effectiveTimeZone'] ?? data['EffectiveTimeZone'] ?? data['timeZone'] ?? data['TimeZone'])
              ?.toString(),
        );
      }
    } catch (_) {
      // Offline or older API: keep the platform default / last known zone.
    }
  }
}
