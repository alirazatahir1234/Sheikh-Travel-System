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
  static String acceptTrip(int id) => '/driver-app/trips/$id/accept';
  static String arrivedTrip(int id) => '/driver-app/trips/$id/arrived';
  static String onboardTrip(int id) => '/driver-app/trips/$id/onboard';
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
  static const fuelReceiptsScan = '/driver-app/fuel-receipts/scan';

  // Notifications
  static const notifications = '/driver-app/notifications';
  static const notificationsUnreadCount = '/driver-app/notifications/unread-count';
  static const notificationsRead = '/driver-app/notifications/read';
  static const notificationsArchive = '/driver-app/notifications/archive';
  static const notificationsRestore = '/driver-app/notifications/restore';
  static String notificationById(int id) => '/driver-app/notifications/$id';
  static const deviceToken = '/ai/device-tokens';
  static const mobileHeartbeat = '/ai/presence/mobile-heartbeat';

  // Timeline
  static const timeline = '/driver-app/timeline';
  static const locationHistory = '/driver-app/location/history';

  // Settings
  static const changePassword = '/users/change-password';
  static const appVersion = '/driver-app/app-version';

  // SOS
  static const sos = '/driver-app/sos';

  // Inspections
  static const inspectionTemplate = '/driver-app/inspection/template';
  static const inspectionVehicles = '/driver-app/inspection/vehicles';
  static const inspectionHistory = '/driver-app/inspection/history';
  static const inspectionSubmit = '/driver-app/inspection';

  // Documents
  static const documents = '/driver-app/documents';
  static const documentsUpload = '/driver-app/documents/upload';

  // Earnings
  static const earnings = '/driver-app/earnings';

  // Device registration / security
  static const deviceRegister = '/driver-app/devices/register';

  // GPS commands (device-level, anonymous)
  static String pendingCommands(String uniqueId) => '/gps/commands/pending?uniqueId=$uniqueId';
  static String completeCommand(int id) => '/gps/commands/$id/complete';

  /// ERP GPS Tracking module — same endpoint used by live map ETA.
  static const gpsEta = '/gps/eta';
}
