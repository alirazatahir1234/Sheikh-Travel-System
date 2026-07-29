abstract class ApiEndpoints {
  // Auth
  static const staffLogin = '/auth/login';
  static const driverLogin = '/driver-app/auth/login';
  static const refreshToken = '/auth/refresh-token';
  static const logout = '/auth/logout';
  static const authMe = '/auth/me';
  static const companyContext = '/platform/company/context';
  static const companyContextAlias = '/tenants/me/company-context';

  // Driver profile & dashboard
  static const driverProfile = '/driver-app/profile';
  static const driverDashboard = '/driver-app/dashboard';
  static const driverStatus = '/driver-app/status';

  // Trips
  static const trips = '/driver-app/trips';
  static String startTrip(int id) => '/driver-app/trips/$id/start';
  static String acceptTrip(int id) => '/driver-app/trips/$id/accept';
  static String arrivedTrip(int id) => '/driver-app/trips/$id/arrived';
  static String onboardTrip(int id) => '/driver-app/trips/$id/onboard';
  static String completeTrip(int id) => '/driver-app/trips/$id/complete';
  static String rejectTrip(int id) => '/driver-app/trips/$id/reject';
  static String tripPaymentSummary(int id) => '/driver-app/trips/$id/payment-summary';
  static String collectTripPayment(int id) => '/driver-app/trips/$id/collect-payment';
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
  static const gpsLive = '/gps/live';
  static const gpsFleetStatusLocal = '/gps/dashboard/fleet-status-local';
  static const gpsOperatorSummary = '/gps/dashboard/operator-summary';
  static const gpsOperatorInsights = '/gps/operator/insights';
  static const gpsHistoryReplayInsights = '/gps/history/replay/insights';
  static String gpsHistory(int vehicleId) => '/gps/history/$vehicleId';
  static const gpsHistoryReplay = '/gps/history/replay';
  static const gpsLocationReverse = '/gps/location/reverse';
  static const gpsGeofences = '/gps/geofences';
  static String gpsCommandsSupported(int deviceId) =>
      '/gps/commands/supported/$deviceId';
  static const gpsCommandsSend = '/gps/commands/send';
  static String gpsCommandsByVehicle(int vehicleId) =>
      '/gps/commands/vehicle/$vehicleId';

  // Fleet ops (staff)
  static const fleetDashboard = '/Fleet/dashboard';
  static const vehicles = '/Vehicles';
  static String vehicleById(int id) => '/Vehicles/$id';
  static String vehicleGps(int id) => '/Vehicles/$id/gps';
  static String vehicleDocuments(int id) => '/Vehicles/$id/documents';
  static String vehicleMaintenance(int id) => '/Vehicles/$id/maintenance';
  static String vehicleFuel(int id) => '/Vehicles/$id/fuel';

  // Drivers (staff)
  static const drivers = '/drivers';
  static const driversStats = '/drivers/stats';
  static String driverById(int id) => '/drivers/$id';
  static String driverPatchStatus(int id) => '/drivers/$id/status';
  static String driverAssignVehicle(int id) => '/drivers/$id/assign-vehicle';
  static String driverUnassignVehicle(int id) => '/drivers/$id/unassign-vehicle';
  static String driverPerformance(int id) => '/drivers/$id/performance/summary';
  static String driverViolations(int id) => '/drivers/$id/violations';
  static String driverAttendance(int id) => '/drivers/$id/attendance';
  static String driverDocuments(int id) => '/drivers/$id/documents';
  static const driverRanking = '/gps/analytics/drivers/ranking';

  // Ops trips (staff) — distinct from driver-app /trips
  static const opsTrips = '/trips';
  static const opsTripsDashboard = '/trips/dashboard';
  static const opsTripsLive = '/trips/live';
  static String opsTripById(int id) => '/trips/$id';
  static String opsTripStatus(int id) => '/trips/$id/status';
  static String opsTripAssignDriver(int id) => '/trips/$id/assign-driver';
  static String opsTripAssignVehicle(int id) => '/trips/$id/assign-vehicle';
  static String opsTripFromBooking(int bookingId) =>
      '/trips/from-booking/$bookingId';

  // Bookings (dispatcher)
  static const bookings = '/bookings';
  static String bookingById(int id) => '/bookings/$id';
  static String bookingAssignDriver(int id) => '/bookings/$id/assign-driver';
  static String bookingAssignVehicle(int id) => '/bookings/$id/assign-vehicle';
  static String bookingStatus(int id) => '/bookings/$id/status';

  // GPS alerts
  static const gpsAlertEvents = '/gps/alerts/events';
  static const gpsAlertStats = '/gps/alerts/stats';
  static const gpsFuelAnalytics = '/gps/analytics/fuel';
  static String gpsAlertEventById(int id) => '/gps/alerts/events/$id';
  static String gpsAlertRead(int id) => '/gps/alerts/events/$id/read';
  static String gpsAlertAcknowledge(int id) => '/gps/alerts/events/$id/acknowledge';
  static String gpsAlertResolve(int id) => '/gps/alerts/events/$id/resolve';
  static String gpsAlertArchive(int id) => '/gps/alerts/events/$id/archive';

  // Staff notifications (vs driver-app proxy)
  static const staffNotifications = '/notifications';
  static const staffNotificationsUnreadCount = '/notifications/unread-count';
  static const staffNotificationsRead = '/notifications/read';
  static const staffNotificationsArchive = '/notifications/archive';
  static const staffNotificationsRestore = '/notifications/restore';
  static String staffNotificationById(int id) => '/notifications/$id';

  // Maintenance (staff)
  static const maintenanceDashboard = '/Maintenance/dashboard';
  static const maintenanceRequests = '/Maintenance/requests';
  static String maintenanceRequestById(int id) => '/Maintenance/requests/$id';
  static const maintenanceRequestStats = '/Maintenance/requests/stats';
  static const maintenanceAlerts = '/Maintenance/alerts';
  static const maintenanceComplianceSummary = '/Maintenance/compliance-summary';
  static const workOrders = '/WorkOrders';
  static String workOrderById(int id) => '/WorkOrders/$id';
  static const workOrderStats = '/WorkOrders/stats';

  // Staff fuel
  static const fuelLogs = '/FuelLogs';
  static String fuelLogById(int id) => '/FuelLogs/$id';

  // Compliance documents (staff)
  static const fleetCompliance = '/Fleet/compliance';

  // Reports
  static const fleetReports = '/fleet-reports';

  // AI Copilot (AiChatGateway + Tool Engine)
  static const aiChat = '/ai/chat';
  static const aiChatSessions = '/ai/chat/sessions';
  static String aiChatMessages(String sessionId) =>
      '/ai/chat/sessions/$sessionId/messages';
  static const aiChatProviderHealth = '/ai/chat/provider-health';
  static const aiChatTools = '/ai/chat/tools';
}
