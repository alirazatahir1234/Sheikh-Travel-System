export enum NotificationType {
  BookingCreated = 1,
  TripDelayed = 2,
  VehicleOffline = 3,
  PaymentReceived = 4,
  EngineCommandSent = 5,
  Sos = 6
}

export type NotificationChannel = 'InApp' | 'Email' | 'Sms' | 'Push' | 'Browser' | 'WhatsApp';

export const NotificationTypeLabels: Record<number, string> = {
  [NotificationType.BookingCreated]: 'New Booking',
  [NotificationType.TripDelayed]: 'Trip Delayed',
  [NotificationType.VehicleOffline]: 'Vehicle Offline',
  [NotificationType.PaymentReceived]: 'Payment Received',
  [NotificationType.EngineCommandSent]: 'Engine Command',
  [NotificationType.Sos]: 'SOS Alert'
};

export const NotificationTypeIcons: Record<number, string> = {
  [NotificationType.BookingCreated]: 'confirmation_number',
  [NotificationType.TripDelayed]: 'schedule',
  [NotificationType.VehicleOffline]: 'directions_bus',
  [NotificationType.PaymentReceived]: 'payments',
  [NotificationType.EngineCommandSent]: 'power_settings_new',
  [NotificationType.Sos]: 'sos'
};

export const NotificationTypeColors: Record<number, string> = {
  [NotificationType.BookingCreated]: '#3B82F6',
  [NotificationType.TripDelayed]: '#F59E0B',
  [NotificationType.VehicleOffline]: '#EF4444',
  [NotificationType.PaymentReceived]: '#10B981',
  [NotificationType.EngineCommandSent]: '#8B5CF6',
  [NotificationType.Sos]: '#DC2626'
};

export const NotificationPriorityLabels: Record<number, string> = {
  1: 'Low',
  2: 'Normal',
  3: 'High',
  4: 'Critical'
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
  priority?: number;
  channel?: NotificationChannel | string;
  recipientType?: string | null;
  isSent?: boolean;
  sentDate?: string | null;
  templateKey?: string | null;
}

export interface NotificationStats {
  unread: number;
  total: number;
  email: number;
  sms: number;
  push: number;
  browser: number;
  whatsApp: number;
  failed: number;
}

export interface NotificationTemplate {
  id: number;
  templateKey: string;
  templateName: string;
  subject: string;
  body: string;
  channel: string;
  isActive: boolean;
}

export interface NotificationDeliveryLog {
  id: number;
  notificationId: number;
  channel: string;
  status: string;
  response?: string | null;
  createdAt: string;
}

export interface NotificationFilter {
  page?: number;
  pageSize?: number;
  unreadOnly?: boolean;
  isSent?: boolean;
  channel?: string;
  priority?: number;
  search?: string;
  fromDate?: string;
  toDate?: string;
}
