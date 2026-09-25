export type WhatsAppInboxFilter =
  | 'all'
  | 'unread'
  | 'assigned_to_me'
  | 'unassigned'
  | 'bot'
  | 'human'
  | 'open'
  | 'pending'
  | 'resolved';

export interface WhatsAppAccount {
  id: number;
  code: string;
  displayName: string;
  e164Phone: string;
  purpose: string;
  phoneNumberId?: string | null;
  isActive: boolean;
  hasAccessToken: boolean;
  country?: string | null;
  countryCode?: string | null;
  status?: string;
  isDefault?: boolean;
  lastHealthCheckedAtUtc?: string | null;
  lastHealthStatus?: string | null;
  lastHealthMessage?: string | null;
}

export interface WhatsAppAccountHealthResult {
  accountId: number;
  status: string;
  message?: string | null;
  checkedAtUtc: string;
}

export interface SendWhatsAppOutboundResult {
  messageId: number;
  metaMessageId?: string | null;
  status: string;
}

export interface WhatsAppConversation {
  id: number;
  accountId: number;
  accountCode: string;
  contactId: number;
  contactPhone: string;
  contactName?: string | null;
  customerId?: number | null;
  status: string;
  lastMessageAt?: string | null;
  unreadCount: number;
  lastMessagePreview?: string | null;
  assignedUserId?: number | null;
  assignedUserName?: string | null;
  leadId?: number | null;
  leadStatus?: string | null;
  customerCompany?: string | null;
  isBotEnabled?: boolean;
  currentBotState?: string | null;
  country?: string | null;
  lastIncomingMessageAt?: string | null;
  lastOutgoingMessageAt?: string | null;
  windowExpiresAt?: string | null;
  isWithinMessagingWindow?: boolean;
}

export interface WhatsAppTemplate {
  id: number;
  whatsAppAccountId: number;
  accountCode: string;
  name: string;
  language: string;
  category: string;
  status: string;
  metaTemplateId?: string | null;
  bodyPreview?: string | null;
}

export interface WhatsAppMessage {
  id: number;
  conversationId: number;
  direction: 'Inbound' | 'Outbound' | string;
  metaMessageId?: string | null;
  type: string;
  body?: string | null;
  mediaId?: string | null;
  status: string;
  createdAtUtc: string;
  attemptCount?: number;
  errorMessage?: string | null;
  errorCode?: string | null;
  automationEventId?: number | null;
  bookingId?: number | null;
}

export interface WhatsAppConversationContext {
  conversationId: number;
  contactPhone: string;
  contactName?: string | null;
  customerId?: number | null;
  customerName?: string | null;
  customerCompany?: string | null;
  leadId?: number | null;
  leadStatus?: string | null;
  activeBookings: WhatsAppContextBooking[];
  lastCompletedTrip?: WhatsAppContextTrip | null;
  unpaidInvoiceTotal: number;
}

export interface WhatsAppContextBooking {
  id: number;
  bookingNumber: string;
  status: string;
  travelDate?: string | null;
}

export interface WhatsAppContextTrip {
  id: number;
  tripNumber?: string | null;
  status: string;
  completedAt?: string | null;
}

export type WhatsAppAiConfidence = 'High' | 'Medium' | 'Low' | string;

export interface WhatsAppAiAssistResult {
  suggestion: string;
  confidence: WhatsAppAiConfidence;
  reviewRecommended: boolean;
  provider: string;
  model?: string | null;
  durationMs: number;
  unavailableFacts: string[];
  operation: string;
  confidenceReason?: string | null;
}

export type WhatsAppAiTransformAction =
  | 'Regenerate'
  | 'Shorter'
  | 'Professional'
  | 'Translate'
  | string;

export interface WhatsAppWebhookLog {
  id: number;
  receivedAtUtc: string;
  signatureValid: boolean;
  processingStatus: string;
  error?: string | null;
  attempts: number;
  processedAtUtc?: string | null;
  phoneNumberId?: string | null;
  tenantId?: number | null;
  payloadPreview?: string | null;
}

export interface WhatsAppContact {
  id: number;
  accountId: number;
  waId: string;
  phoneE164: string;
  profileName?: string | null;
  customerId?: number | null;
  suggestedCustomerName?: string | null;
}

export interface WhatsAppAutomationRule {
  id: number;
  tenantId: number;
  eventType: string;
  isEnabled: boolean;
  accountId?: number | null;
  templateName?: string | null;
  offsetMinutes: number;
  notifyBooker: boolean;
  isUrgent: boolean;
}

export interface WhatsAppAutomationTimelineItem {
  id: number;
  eventType: string;
  status: number;
  skipReason?: string | null;
  dueAt: string;
  processedAt?: string | null;
  recipientPhone?: string | null;
  messageId?: number | null;
  messageStatus?: string | null;
  messageCreatedAt?: string | null;
}

export interface WhatsAppAutomationPreview {
  eventType: string;
  templateName: string;
  language: string;
  recipientPhone: string;
  bodyParameters: string[];
  bodyPreview?: string | null;
  trackingUrl?: string | null;
}
