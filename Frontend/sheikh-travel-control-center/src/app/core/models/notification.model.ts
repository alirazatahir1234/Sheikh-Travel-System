export enum NotificationType {
  BookingCreated  = 1,
  TripDelayed     = 2,
  VehicleOffline  = 3,
  PaymentReceived = 4
}

export const NotificationTypeLabels: Record<NotificationType, string> = {
  [NotificationType.BookingCreated]:  'New Booking',
  [NotificationType.TripDelayed]:     'Trip Delayed',
  [NotificationType.VehicleOffline]:  'Vehicle Offline',
  [NotificationType.PaymentReceived]: 'Payment Received'
};

export const NotificationTypeIcons: Record<NotificationType, string> = {
  [NotificationType.BookingCreated]:  'confirmation_number',
  [NotificationType.TripDelayed]:     'schedule',
  [NotificationType.VehicleOffline]:  'directions_bus',
  [NotificationType.PaymentReceived]: 'payments'
};

export const NotificationTypeColors: Record<NotificationType, string> = {
  [NotificationType.BookingCreated]:  '#3B82F6',
  [NotificationType.TripDelayed]:     '#F59E0B',
  [NotificationType.VehicleOffline]:  '#EF4444',
  [NotificationType.PaymentReceived]: '#10B981'
};

export interface Notification {
  id: number;
  userId?: number | null;
  title: string;
  message: string;
  type: NotificationType;
  isRead: boolean;
  referenceId?: number | null;
  createdAt: string;
}
