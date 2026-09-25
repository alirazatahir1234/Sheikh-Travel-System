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
