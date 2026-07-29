class GpsOperatorSummary {
  const GpsOperatorSummary({
    required this.totalVehicles,
    required this.online,
    required this.offline,
    required this.moving,
    required this.idle,
    required this.parked,
    required this.neverSeen,
    required this.sos,
    required this.alertsToday,
    required this.trackerHealthy,
    required this.trackerOffline,
    required this.weakGsm,
    required this.lowBattery,
    required this.noGpsSignal,
    required this.ignitionOn,
    required this.overspeedAlertsToday,
    required this.sosAlertsToday,
    required this.geofenceAlertsToday,
    required this.offlineAlertsToday,
    required this.powerCutAlertsToday,
    required this.todaysTrips,
    this.todayDistanceKm,
  });

  final int totalVehicles;
  final int online;
  final int offline;
  final int moving;
  final int idle;
  final int parked;
  final int neverSeen;
  final int sos;
  final int alertsToday;
  final int trackerHealthy;
  final int trackerOffline;
  final int weakGsm;
  final int lowBattery;
  final int noGpsSignal;
  final int ignitionOn;
  final int overspeedAlertsToday;
  final int sosAlertsToday;
  final int geofenceAlertsToday;
  final int offlineAlertsToday;
  final int powerCutAlertsToday;
  final int todaysTrips;
  final double? todayDistanceKm;

  static const empty = GpsOperatorSummary(
    totalVehicles: 0,
    online: 0,
    offline: 0,
    moving: 0,
    idle: 0,
    parked: 0,
    neverSeen: 0,
    sos: 0,
    alertsToday: 0,
    trackerHealthy: 0,
    trackerOffline: 0,
    weakGsm: 0,
    lowBattery: 0,
    noGpsSignal: 0,
    ignitionOn: 0,
    overspeedAlertsToday: 0,
    sosAlertsToday: 0,
    geofenceAlertsToday: 0,
    offlineAlertsToday: 0,
    powerCutAlertsToday: 0,
    todaysTrips: 0,
  );

  factory GpsOperatorSummary.fromJson(Map<String, dynamic> json) {
    final fleet = json['fleet'] ?? json['Fleet'];
    final f = fleet is Map ? Map<String, dynamic>.from(fleet) : json;
    return GpsOperatorSummary(
      totalVehicles: _i(f, 'totalVehicles'),
      online: _i(f, 'online'),
      offline: _i(f, 'offline'),
      moving: _i(f, 'moving'),
      idle: _i(f, 'idle'),
      parked: _i(f, 'parked'),
      neverSeen: _i(f, 'neverSeen'),
      sos: _i(f, 'sos'),
      alertsToday: _i(f, 'alertsToday'),
      trackerHealthy: _i(json, 'trackerHealthy'),
      trackerOffline: _i(json, 'trackerOffline'),
      weakGsm: _i(json, 'weakGsm'),
      lowBattery: _i(json, 'lowBattery'),
      noGpsSignal: _i(json, 'noGpsSignal'),
      ignitionOn: _i(json, 'ignitionOn'),
      overspeedAlertsToday: _i(json, 'overspeedAlertsToday'),
      sosAlertsToday: _i(json, 'sosAlertsToday'),
      geofenceAlertsToday: _i(json, 'geofenceAlertsToday'),
      offlineAlertsToday: _i(json, 'offlineAlertsToday'),
      powerCutAlertsToday: _i(json, 'powerCutAlertsToday'),
      todaysTrips: _i(json, 'todaysTrips'),
      todayDistanceKm: (json['todayDistanceKm'] as num?)?.toDouble(),
    );
  }

  static int _i(Map<String, dynamic> j, String key) =>
      j[key] as int? ?? j[_cap(key)] as int? ?? 0;

  static String _cap(String s) =>
      s.isEmpty ? s : '${s[0].toUpperCase()}${s.substring(1)}';
}
