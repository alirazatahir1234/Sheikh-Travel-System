abstract class ApiEndpoints {
  // Auth
  static const driverLogin = '/driver-app/auth/login';
  static const refreshToken = '/auth/refresh-token';
  static const logout = '/auth/logout';

  // Driver profile & dashboard
  static const driverProfile = '/driver-app/profile';
  static const driverDashboard = '/driver-app/dashboard';

  // Trips
  static const trips = '/driver-app/trips';
  static String startTrip(int id) => '/driver-app/trips/$id/start';
  static String completeTrip(int id) => '/driver-app/trips/$id/complete';
  static String rejectTrip(int id) => '/driver-app/trips/$id/reject';
  static const tripLocation = '/driver-app/trips/location';
  static const tripLocationBatch = '/driver-app/location/batch';

  // Attendance
  static const attendanceCheckIn = '/driver-app/attendance/check-in';
  static const attendanceCheckOut = '/driver-app/attendance/check-out';
  static const attendanceHistory = '/driver-app/attendance/history';

  // Fuel
  static const fuelReceipts = '/driver-app/fuel-receipts';

  // Notifications
  static const notifications = '/driver-app/notifications';

  // Timeline
  static const timeline = '/driver-app/timeline';
  static const locationHistory = '/driver-app/location/history';

  // Settings
  static const changePassword = '/users/change-password';
  static const appVersion = '/driver-app/app-version';

  // SOS
  static const sos = '/driver-app/sos';

  // GPS commands (device-level, anonymous)
  static String pendingCommands(String uniqueId) => '/gps/commands/pending?uniqueId=$uniqueId';
  static String completeCommand(int id) => '/gps/commands/$id/complete';
}
