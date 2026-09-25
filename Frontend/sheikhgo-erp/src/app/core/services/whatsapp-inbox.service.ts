import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../models/common.model';
import {
  SendWhatsAppOutboundResult,
  WhatsAppAccount,
  WhatsAppAccountHealthResult,
  WhatsAppAutomationPreview,
  WhatsAppAutomationRule,
  WhatsAppAutomationTimelineItem,
  WhatsAppContact,
  WhatsAppConversation,
  WhatsAppConversationContext,
  WhatsAppInboxFilter,
  WhatsAppMessage,
  WhatsAppTemplate,
  WhatsAppWebhookLog,
  WhatsAppAiAssistResult,
  WhatsAppAiTransformAction
} from '../models/whatsapp.model';

@Injectable({ providedIn: 'root' })
export class WhatsAppInboxService {
  private readonly base = `${environment.apiUrl}/whatsapp`;

  constructor(private http: HttpClient) {}

  getAccounts(includeInactive = false): Observable<WhatsAppAccount[]> {
    let params = new HttpParams();
    if (includeInactive) {
      params = params.set('includeInactive', 'true');
    }
    return this.http.get<WhatsAppAccount[]>(`${this.base}/accounts`, { params });
  }

  setAccountActive(accountId: number, isActive: boolean): Observable<WhatsAppAccount> {
    return this.http.patch<WhatsAppAccount>(`${this.base}/accounts/${accountId}/active`, { isActive });
  }

  checkAccountHealth(accountId: number): Observable<WhatsAppAccountHealthResult> {
    return this.http.post<WhatsAppAccountHealthResult>(`${this.base}/accounts/${accountId}/health`, {});
  }

  sendOutbound(body: {
    whatsAppAccountId?: number | null;
    conversationId?: number | null;
    recipientPhoneNumber?: string | null;
    messageType?: string;
    text: string;
  }): Observable<SendWhatsAppOutboundResult> {
    return this.http.post<SendWhatsAppOutboundResult>(`${this.base}/send`, {
      whatsAppAccountId: body.whatsAppAccountId ?? null,
      conversationId: body.conversationId ?? null,
      recipientPhoneNumber: body.recipientPhoneNumber ?? null,
      messageType: body.messageType ?? 'text',
      text: body.text
    });
  }

  getConversations(opts: {
    accountId?: number | null;
    search?: string;
    filter?: WhatsAppInboxFilter | string | null;
    page?: number;
    pageSize?: number;
  } = {}): Observable<PagedResult<WhatsAppConversation>> {
    let params = new HttpParams()
      .set('page', String(opts.page ?? 1))
      .set('pageSize', String(opts.pageSize ?? 40));
    if (opts.accountId != null) params = params.set('accountId', String(opts.accountId));
    if (opts.search?.trim()) params = params.set('search', opts.search.trim());
    if (opts.filter && opts.filter !== 'all') params = params.set('filter', opts.filter);
    return this.http.get<PagedResult<WhatsAppConversation>>(`${this.base}/conversations`, { params });
  }

  setAssignment(conversationId: number, assignedUserId: number | null): Observable<WhatsAppConversation> {
    return this.http.patch<WhatsAppConversation>(
      `${this.base}/conversations/${conversationId}/assignment`,
      { assignedUserId }
    );
  }

  setStatus(conversationId: number, status: string): Observable<WhatsAppConversation> {
    return this.http.patch<WhatsAppConversation>(
      `${this.base}/conversations/${conversationId}/status`,
      { status }
    );
  }

  setBot(
    conversationId: number,
    isBotEnabled: boolean,
    currentBotState?: string | null
  ): Observable<WhatsAppConversation> {
    return this.http.patch<WhatsAppConversation>(`${this.base}/conversations/${conversationId}/bot`, {
      isBotEnabled,
      currentBotState: currentBotState ?? null
    });
  }

  getMessages(conversationId: number, page = 1, pageSize = 50): Observable<PagedResult<WhatsAppMessage>> {
    const params = new HttpParams().set('page', String(page)).set('pageSize', String(pageSize));
    return this.http.get<PagedResult<WhatsAppMessage>>(
      `${this.base}/conversations/${conversationId}/messages`,
      { params }
    );
  }

  sendMessage(conversationId: number, body: string): Observable<{ messageId: number; metaMessageId?: string; status: string }> {
    return this.http.post<{ messageId: number; metaMessageId?: string; status: string }>(
      `${this.base}/conversations/${conversationId}/messages`,
      { body }
    );
  }

  getTemplates(accountId?: number | null, status?: string | null): Observable<WhatsAppTemplate[]> {
    let params = new HttpParams();
    if (accountId != null) params = params.set('accountId', String(accountId));
    if (status?.trim()) params = params.set('status', status.trim());
    return this.http.get<WhatsAppTemplate[]>(`${this.base}/templates`, { params });
  }

  setTemplateStatus(
    id: number,
    status: string,
    metaTemplateId?: string | null
  ): Observable<WhatsAppTemplate> {
    return this.http.patch<WhatsAppTemplate>(`${this.base}/templates/${id}/status`, {
      status,
      metaTemplateId: metaTemplateId ?? null
    });
  }

  sendTemplate(body: {
    conversationId?: number | null;
    whatsAppAccountId?: number | null;
    recipientPhoneNumber?: string | null;
    templateName: string;
    language?: string;
    bodyParameters?: string[] | null;
  }): Observable<SendWhatsAppOutboundResult> {
    return this.http.post<SendWhatsAppOutboundResult>(`${this.base}/send-template`, {
      conversationId: body.conversationId ?? null,
      whatsAppAccountId: body.whatsAppAccountId ?? null,
      recipientPhoneNumber: body.recipientPhoneNumber ?? null,
      templateName: body.templateName,
      language: body.language ?? 'en',
      bodyParameters: body.bodyParameters ?? null
    });
  }

  markRead(conversationId: number): Observable<unknown> {
    return this.http.post(`${this.base}/conversations/${conversationId}/read`, {});
  }

  getContact(contactId: number): Observable<WhatsAppContact> {
    return this.http.get<WhatsAppContact>(`${this.base}/contacts/${contactId}`);
  }

  linkCustomer(contactId: number, customerId: number | null): Observable<WhatsAppContact> {
    return this.http.post<WhatsAppContact>(`${this.base}/contacts/${contactId}/link-customer`, {
      customerId
    });
  }

  autoLink(contactId: number): Observable<WhatsAppContact> {
    return this.http.post<WhatsAppContact>(`${this.base}/contacts/${contactId}/auto-link`, {});
  }

  mediaUrl(mediaId: string, accountId: number): string {
    return `${this.base}/media?mediaId=${encodeURIComponent(mediaId)}&accountId=${accountId}`;
  }

  getConversationContext(conversationId: number): Observable<WhatsAppConversationContext> {
    return this.http.get<WhatsAppConversationContext>(
      `${this.base}/conversations/${conversationId}/context`
    );
  }

  createLead(conversationId: number): Observable<WhatsAppConversationContext> {
    return this.http.post<WhatsAppConversationContext>(
      `${this.base}/conversations/${conversationId}/create-lead`,
      {}
    );
  }

  retryMessage(messageId: number): Observable<SendWhatsAppOutboundResult> {
    return this.http.post<SendWhatsAppOutboundResult>(`${this.base}/messages/${messageId}/retry`, {});
  }

  syncTemplates(accountId: number): Observable<{ accountId: number; synced: number }> {
    return this.http.post<{ accountId: number; synced: number }>(
      `${this.base}/accounts/${accountId}/templates/sync`,
      {}
    );
  }

  getWebhookLogs(opts: {
    page?: number;
    pageSize?: number;
    status?: string | null;
  } = {}): Observable<{ items: WhatsAppWebhookLog[]; total: number; page: number; pageSize: number }> {
    let params = new HttpParams()
      .set('page', String(opts.page ?? 1))
      .set('pageSize', String(opts.pageSize ?? 30));
    if (opts.status?.trim()) params = params.set('status', opts.status.trim());
    return this.http.get<{ items: WhatsAppWebhookLog[]; total: number; page: number; pageSize: number }>(
      `${this.base}/webhook-logs`,
      { params }
    );
  }

  requeueWebhookLog(id: number): Observable<{ requeued: boolean; webhookLogId: number }> {
    return this.http.post<{ requeued: boolean; webhookLogId: number }>(
      `${this.base}/webhook-logs/${id}/requeue`,
      {}
    );
  }

  getAutomationRules(): Observable<WhatsAppAutomationRule[]> {
    return this.http.get<WhatsAppAutomationRule[]>(`${this.base}/automation-rules`);
  }

  upsertAutomationRule(
    eventType: string,
    body: {
      isEnabled: boolean;
      accountId?: number | null;
      templateName?: string | null;
      offsetMinutes: number;
      notifyBooker: boolean;
      isUrgent: boolean;
    }
  ): Observable<WhatsAppAutomationRule> {
    return this.http.put<WhatsAppAutomationRule>(`${this.base}/automation-rules/${encodeURIComponent(eventType)}`, body);
  }

  previewAutomation(eventType: string, bookingId: number): Observable<WhatsAppAutomationPreview> {
    const params = new HttpParams().set('bookingId', String(bookingId));
    return this.http.get<WhatsAppAutomationPreview>(
      `${this.base}/automation-rules/${encodeURIComponent(eventType)}/preview`,
      { params }
    );
  }

  getBookingWhatsAppTimeline(bookingId: number): Observable<WhatsAppAutomationTimelineItem[]> {
    return this.http.get<WhatsAppAutomationTimelineItem[]>(
      `${environment.apiUrl}/bookings/${bookingId}/whatsapp-timeline`
    );
  }

  resendAutomationEvent(eventId: number): Observable<{ eventId: number }> {
    return this.http.post<{ eventId: number }>(`${this.base}/automation-events/${eventId}/resend`, {});
  }

  recordConsent(waId: string, note?: string | null): Observable<unknown> {
    return this.http.post(`${this.base}/consents`, { waId, note: note ?? null });
  }

  optOutConsent(waId: string): Observable<unknown> {
    return this.http.delete(`${this.base}/consents/${encodeURIComponent(waId)}`);
  }

  aiSuggest(
    conversationId: number,
    body: { priorSuggestion?: string | null; instruction?: string | null } = {}
  ): Observable<WhatsAppAiAssistResult> {
    return this.http.post<WhatsAppAiAssistResult>(`${this.base}/conversations/${conversationId}/ai/suggest`, {
      priorSuggestion: body.priorSuggestion ?? null,
      instruction: body.instruction ?? null
    });
  }

  aiTransform(
    conversationId: number,
    body: {
      action: WhatsAppAiTransformAction;
      text?: string | null;
      targetLanguage?: string | null;
      priorSuggestion?: string | null;
    }
  ): Observable<WhatsAppAiAssistResult> {
    return this.http.post<WhatsAppAiAssistResult>(`${this.base}/conversations/${conversationId}/ai/transform`, {
      action: body.action,
      text: body.text ?? null,
      targetLanguage: body.targetLanguage ?? null,
      priorSuggestion: body.priorSuggestion ?? null
    });
  }

  aiSummary(conversationId: number): Observable<WhatsAppAiAssistResult> {
    return this.http.post<WhatsAppAiAssistResult>(
      `${this.base}/conversations/${conversationId}/ai/summary`,
      {}
    );
  }
}
