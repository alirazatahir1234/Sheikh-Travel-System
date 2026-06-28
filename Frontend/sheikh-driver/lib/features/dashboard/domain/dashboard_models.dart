class DashboardSummary {
  const DashboardSummary({
    required this.assignedTripsToday,
    required this.completedToday,
    required this.clockedIn,
    this.currentVehicle,
    this.currentVehiclePlate,
    required this.earningsThisWeek,
    required this.unreadNotifications,
    required this.driverStatus,
  });

  final int assignedTripsToday;
  final int completedToday;
  final bool clockedIn;
  final String? currentVehicle;
  final String? currentVehiclePlate;
  final double earningsThisWeek;
  final int unreadNotifications;
  final String driverStatus;

  factory DashboardSummary.fromJson(Map<String, dynamic> json) {
    return DashboardSummary(
      assignedTripsToday: json['assignedTripsToday'] as int? ?? 0,
      completedToday: json['completedToday'] as int? ?? 0,
      clockedIn: json['clockedIn'] as bool? ?? false,
      currentVehicle: json['currentVehicle'] as String?,
      currentVehiclePlate: json['currentVehiclePlate'] as String?,
      earningsThisWeek: (json['earningsThisWeek'] as num?)?.toDouble() ?? 0.0,
      unreadNotifications: json['unreadNotifications'] as int? ?? 0,
      driverStatus: json['driverStatus'] as String? ?? 'Available',
    );
  }

  static DashboardSummary empty() => const DashboardSummary(
        assignedTripsToday: 0,
        completedToday: 0,
        clockedIn: false,
        earningsThisWeek: 0,
        unreadNotifications: 0,
        driverStatus: 'Available',
      );
}
