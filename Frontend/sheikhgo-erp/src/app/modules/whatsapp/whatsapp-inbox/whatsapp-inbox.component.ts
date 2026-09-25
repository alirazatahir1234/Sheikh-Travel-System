import { Component, OnDestroy, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { Subject, debounceTime, distinctUntilChanged, takeUntil } from 'rxjs';
import { WhatsAppInboxService } from '../../../core/services/whatsapp-inbox.service';
import { WhatsAppRealtimeEvent, WhatsAppRealtimeService } from '../../../core/services/whatsapp-realtime.service';
import { UserService } from '../../../core/services/user.service';
import {
  WhatsAppAccount,
  WhatsAppContact,
  WhatsAppConversation,
  WhatsAppConversationContext,
  WhatsAppInboxFilter,
  WhatsAppMessage,
  WhatsAppTemplate,
  WhatsAppAiAssistResult
} from '../../../core/models/whatsapp.model';
import { User } from '../../../core/models/user.model';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  standalone: false,
  selector: 'app-whatsapp-inbox',
  templateUrl: './whatsapp-inbox.component.html',
  styleUrls: ['./whatsapp-inbox.component.scss']
})
export class WhatsAppInboxComponent implements OnInit, OnDestroy {
  private readonly destroy$ = new Subject<void>();
  private readonly search$ = new Subject<string>();
  private windowTimer: ReturnType<typeof setInterval> | null = null;

  accounts: WhatsAppAccount[] = [];
  selectedAccountId: number | null = null;
  conversations: WhatsAppConversation[] = [];
  selected: WhatsAppConversation | null = null;
  messages: WhatsAppMessage[] = [];
  contact: WhatsAppContact | null = null;
  context: WhatsAppConversationContext | null = null;
  agents: User[] = [];
  approvedTemplates: WhatsAppTemplate[] = [];
  selectedTemplateKey = '';
  templateParamValues: string[] = [];
  templatePreview = '';
  draft = '';
  search = '';
  activeFilter: WhatsAppInboxFilter = 'all';
  loadingList = false;
  loadingThread = false;
  loadingOlder = false;
  sending = false;
  retryingId: number | null = null;
  messagePage = 1;
  messageTotal = 0;
  mobileShowThread = false;
  windowLabel = '';
  windowTone: 'open' | 'amber' | 'closed' = 'closed';
  nowMs = Date.now();

  aiAssistOpen = false;
  aiBusy = false;
  aiError: string | null = null;
  aiResult: WhatsAppAiAssistResult | null = null;
  aiSummary: string | null = null;
  aiTranslateLang = 'English';

  readonly breadcrumbs = [
    { label: 'Administration' },
    { label: 'WhatsApp' },
    { label: 'Inbox' }
  ];

  readonly filters: { id: WhatsAppInboxFilter; label: string }[] = [
    { id: 'all', label: 'All' },
    { id: 'unread', label: 'Unread' },
    { id: 'assigned_to_me', label: 'Assigned to me' },
    { id: 'unassigned', label: 'Unassigned' },
    { id: 'bot', label: 'Bot' },
    { id: 'human', label: 'Human' },
    { id: 'open', label: 'Open' },
    { id: 'pending', label: 'Pending' },
    { id: 'resolved', label: 'Resolved' }
  ];

  constructor(
    private inbox: WhatsAppInboxService,
    private realtime: WhatsAppRealtimeService,
    private users: UserService,
    private toast: UiToastService,
    private auth: AuthService,
    private router: Router
  ) {}

  get canReply(): boolean {
    return this.auth.hasPermission('WhatsApp.Reply');
  }

  get canAiAssist(): boolean {
    return this.auth.hasPermission('WhatsApp.AiAssist');
  }

  get canManage(): boolean {
    return this.auth.hasPermission('WhatsApp.Manage');
  }

  get hasOlderMessages(): boolean {
    return this.messages.length < this.messageTotal;
  }

  get selectedTemplate(): WhatsAppTemplate | null {
    if (!this.selectedTemplateKey) return null;
    return this.approvedTemplates.find(t => this.templateKey(t) === this.selectedTemplateKey) ?? null;
  }

  get windowWindowOpen(): boolean {
    return this.windowTone !== 'closed';
  }

  ngOnInit(): void {
    this.search$
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(() => this.refreshConversations());

    this.loadAccounts();
    this.loadAgents();
    void this.realtime.start();
    this.realtime.events.pipe(takeUntil(this.destroy$)).subscribe(ev => this.onRealtime(ev));
    this.windowTimer = setInterval(() => this.refreshWindowBadge(), 1000);
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
    if (this.windowTimer) clearInterval(this.windowTimer);
    void this.realtime.stop();
  }

  onSearchInput(value: string): void {
    this.search = value;
    this.search$.next(value.trim());
  }

  setFilter(filter: WhatsAppInboxFilter): void {
    this.activeFilter = filter;
    this.refreshConversations();
  }

  loadAccounts(): void {
    this.inbox.getAccounts().subscribe({
      next: accounts => {
        this.accounts = accounts ?? [];
        this.refreshConversations();
      },
      error: () => this.toast.error('Unable to load WhatsApp accounts.')
    });
  }

  loadAgents(): void {
    if (!this.canReply) return;
    this.users.getAll(1, 100).subscribe({
      next: page => (this.agents = (page?.items ?? []).filter(u => u.isActive !== false)),
      error: () => (this.agents = [])
    });
  }

  selectAccount(id: number | null): void {
    this.selectedAccountId = id;
    this.selected = null;
    this.messages = [];
    this.contact = null;
    this.context = null;
    this.resetAiAssist();
    this.mobileShowThread = false;
    this.refreshConversations();
  }

  refreshConversations(showSpinner = true): void {
    if (showSpinner) this.loadingList = true;
    this.inbox
      .getConversations({
        accountId: this.selectedAccountId,
        search: this.search,
        filter: this.activeFilter,
        page: 1,
        pageSize: 40
      })
      .subscribe({
        next: page => {
          this.conversations = page?.items ?? [];
          this.loadingList = false;
          if (this.selected) {
            const fresh = this.conversations.find(c => c.id === this.selected!.id);
            if (fresh) {
              this.selected = { ...fresh };
              this.refreshWindowBadge();
            }
          }
        },
        error: () => {
          this.loadingList = false;
          this.toast.error('Unable to load conversations.');
        }
      });
  }

  openConversation(conv: WhatsAppConversation, markRead = true): void {
    this.selected = conv;
    this.mobileShowThread = true;
    this.messagePage = 1;
    this.loadingThread = true;
    this.selectedTemplateKey = '';
    this.templateParamValues = [];
    this.templatePreview = '';
    this.resetAiAssist(false);
    this.refreshWindowBadge();
    this.loadApprovedTemplates(conv.accountId);
    this.loadContext(conv.id);
    this.inbox.getMessages(conv.id, 1, 50).subscribe({
      next: page => {
        this.messages = page?.items ?? [];
        this.messageTotal = page?.totalCount ?? this.messages.length;
        this.loadingThread = false;
      },
      error: () => {
        this.loadingThread = false;
        this.toast.error('Unable to load messages.');
      }
    });
    this.inbox.getContact(conv.contactId).subscribe({
      next: c => (this.contact = c),
      error: () => (this.contact = null)
    });
    if (markRead && conv.unreadCount > 0) {
      this.markReadExplicit(false);
    }
  }

  loadContext(conversationId: number): void {
    this.inbox.getConversationContext(conversationId).subscribe({
      next: ctx => (this.context = ctx),
      error: () => (this.context = null)
    });
  }

  loadApprovedTemplates(accountId: number): void {
    this.inbox.getTemplates(accountId, 'Approved').subscribe({
      next: rows => (this.approvedTemplates = rows ?? []),
      error: () => (this.approvedTemplates = [])
    });
  }

  templateKey(t: WhatsAppTemplate): string {
    return `${t.name}::${t.language}`;
  }

  onTemplateSelected(): void {
    const t = this.selectedTemplate;
    const placeholders = this.extractPlaceholders(t?.bodyPreview);
    this.templateParamValues = placeholders.map(() => '');
    this.updateTemplatePreview();
  }

  updateTemplatePreview(): void {
    const t = this.selectedTemplate;
    if (!t?.bodyPreview) {
      this.templatePreview = '';
      return;
    }
    let preview = t.bodyPreview;
    this.templateParamValues.forEach((v, i) => {
      preview = preview.replace(new RegExp(`\\{\\{${i + 1}\\}\\}`, 'g'), v || `{{${i + 1}}}`);
    });
    this.templatePreview = preview;
  }

  extractPlaceholders(body?: string | null): number[] {
    if (!body) return [];
    const found = new Set<number>();
    const re = /\{\{(\d+)\}\}/g;
    let m: RegExpExecArray | null;
    while ((m = re.exec(body)) !== null) {
      found.add(Number(m[1]));
    }
    return Array.from(found).sort((a, b) => a - b);
  }

  markReadExplicit(showToast = true): void {
    if (!this.selected) return;
    const id = this.selected.id;
    this.inbox.markRead(id).subscribe({
      next: () => {
        if (this.selected?.id === id) this.selected.unreadCount = 0;
        const row = this.conversations.find(c => c.id === id);
        if (row) row.unreadCount = 0;
        if (showToast) this.toast.success('Marked as read.');
      },
      error: err => {
        if (showToast) this.toast.error(err?.error?.message || 'Mark read failed.');
      }
    });
  }

  backToList(): void {
    this.mobileShowThread = false;
  }

  loadOlder(): void {
    if (!this.selected || this.loadingOlder || !this.hasOlderMessages) return;
    this.loadingOlder = true;
    const nextPage = this.messagePage + 1;
    this.inbox.getMessages(this.selected.id, nextPage, 50).subscribe({
      next: page => {
        const older = page?.items ?? [];
        this.messages = [...older, ...this.messages];
        this.messagePage = nextPage;
        this.messageTotal = page?.totalCount ?? this.messageTotal;
        this.loadingOlder = false;
      },
      error: () => {
        this.loadingOlder = false;
        this.toast.error('Unable to load older messages.');
      }
    });
  }

  send(): void {
    if (!this.selected || !this.draft.trim() || !this.canReply || this.sending) return;
    if (!this.isWindowOpen) {
      this.toast.error('Messaging window closed. Send an approved template instead.');
      return;
    }
    const body = this.draft.trim();
    this.sending = true;
    this.inbox.sendMessage(this.selected.id, body).subscribe({
      next: () => {
        this.draft = '';
        this.sending = false;
        this.openConversation(this.selected!, false);
        this.refreshConversations(false);
      },
      error: err => {
        this.sending = false;
        const code = err?.error?.code || err?.error?.Code;
        if (err?.status === 409 || code === 'WINDOW_CLOSED') {
          this.toast.error('Messaging window closed. Send an approved template instead.');
          this.refreshWindowBadge();
          return;
        }
        this.toast.error(err?.error?.message || 'Send failed.');
      }
    });
  }

  sendTemplate(): void {
    if (!this.selected || !this.canReply || this.sending || !this.selectedTemplateKey) return;
    const t = this.selectedTemplate;
    if (!t) return;
    const placeholders = this.extractPlaceholders(t.bodyPreview);
    if (placeholders.some((_, i) => !this.templateParamValues[i]?.trim())) {
      this.toast.error('Fill all template variables before sending.');
      return;
    }
    this.sending = true;
    this.inbox
      .sendTemplate({
        conversationId: this.selected.id,
        whatsAppAccountId: this.selected.accountId,
        templateName: t.name,
        language: t.language || 'en',
        bodyParameters: this.templateParamValues
      })
      .subscribe({
        next: () => {
          this.sending = false;
          this.selectedTemplateKey = '';
          this.templateParamValues = [];
          this.templatePreview = '';
          this.toast.success('Template sent.');
          this.openConversation(this.selected!, false);
          this.refreshConversations(false);
        },
        error: err => {
          this.sending = false;
          this.toast.error(err?.error?.message || 'Template send failed.');
        }
      });
  }

  retryMessage(m: WhatsAppMessage): void {
    if (!this.canReply || this.retryingId != null) return;
    if ((m.status || '').toLowerCase() !== 'failed') return;
    if ((m.attemptCount ?? 1) >= 3) {
      this.toast.error('Retry limit reached (3).');
      return;
    }
    this.retryingId = m.id;
    this.inbox.retryMessage(m.id).subscribe({
      next: () => {
        this.retryingId = null;
        this.toast.success('Message retried.');
        if (this.selected) this.openConversation(this.selected, false);
      },
      error: err => {
        this.retryingId = null;
        this.toast.error(err?.error?.message || 'Retry failed.');
      }
    });
  }

  onComposerKeydown(event: KeyboardEvent): void {
    if ((event.ctrlKey || event.metaKey) && event.key === 'Enter') {
      event.preventDefault();
      this.send();
    }
  }

  assignTo(userId: number | null): void {
    if (!this.selected || !this.canReply) return;
    this.inbox.setAssignment(this.selected.id, userId).subscribe({
      next: updated => {
        this.applyConversationUpdate(updated);
        this.toast.success(userId ? 'Assigned.' : 'Unassigned.');
      },
      error: err => this.toast.error(err?.error?.message || 'Assignment failed.')
    });
  }

  changeStatus(status: string): void {
    if (!this.selected || !this.canReply) return;
    this.inbox.setStatus(this.selected.id, status).subscribe({
      next: updated => {
        this.applyConversationUpdate(updated);
        this.toast.success(`Status → ${status}`);
      },
      error: err => this.toast.error(err?.error?.message || 'Status update failed.')
    });
  }

  toggleBot(): void {
    if (!this.selected || !this.canReply) return;
    const next = !this.selected.isBotEnabled;
    this.inbox.setBot(this.selected.id, next, next ? 'Active' : null).subscribe({
      next: updated => {
        this.applyConversationUpdate(updated);
        this.toast.success(next ? 'Bot enabled.' : 'Human mode.');
      },
      error: err => this.toast.error(err?.error?.message || 'Bot update failed.')
    });
  }

  autoLink(): void {
    if (!this.contact || !this.canManage) return;
    this.inbox.autoLink(this.contact.id).subscribe({
      next: c => {
        this.contact = c;
        this.toast.success('Customer linked by phone.');
        if (this.selected) this.selected.customerId = c.customerId ?? null;
        if (this.selected) this.loadContext(this.selected.id);
      },
      error: err => this.toast.error(err?.error?.message || 'No matching customer found.')
    });
  }

  createLead(): void {
    if (!this.selected || !this.canManage) return;
    this.inbox.createLead(this.selected.id).subscribe({
      next: ctx => {
        this.context = ctx;
        if (this.selected) {
          this.selected.leadId = ctx.leadId ?? null;
          this.selected.leadStatus = ctx.leadStatus ?? null;
          this.selected.customerId = ctx.customerId ?? this.selected.customerId;
        }
        this.toast.success('Lead created / linked.');
      },
      error: err => this.toast.error(err?.error?.message || 'Create lead failed.')
    });
  }

  openCustomer(): void {
    const id = this.contact?.customerId ?? this.selected?.customerId ?? this.context?.customerId;
    if (!id) return;
    void this.router.navigate(['/customers', id]);
  }

  countryLabel(c: WhatsAppConversation | null): string {
    if (!c) return '—';
    if (c.country === 'AE' || c.accountCode === 'UAE') return 'United Arab Emirates';
    if (c.country === 'PK' || c.accountCode === 'PK') return 'Pakistan';
    return c.country || '—';
  }

  relativeTime(iso?: string | null): string {
    if (!iso) return '';
    const t = new Date(iso).getTime();
    if (Number.isNaN(t)) return '';
    const diff = Date.now() - t;
    const m = Math.floor(diff / 60000);
    if (m < 1) return 'now';
    if (m < 60) return `${m}m`;
    const h = Math.floor(m / 60);
    if (h < 24) return `${h}h`;
    const d = Math.floor(h / 24);
    if (d < 7) return `${d}d`;
    return new Date(iso).toLocaleDateString();
  }

  statusTick(status: string): string {
    const s = (status || '').toLowerCase();
    if (s === 'read') return 'done_all';
    if (s === 'delivered') return 'done_all';
    if (s === 'sent') return 'done';
    if (s === 'failed') return 'error';
    if (s === 'sending' || s === 'queued') return 'schedule';
    return 'schedule';
  }

  canRetry(m: WhatsAppMessage): boolean {
    return (
      m.direction === 'Outbound' &&
      (m.status || '').toLowerCase() === 'failed' &&
      (m.attemptCount ?? 1) < 3
    );
  }

  private refreshWindowBadge(): void {
    this.nowMs = Date.now();
    const conv = this.selected;
    if (!conv) {
      this.windowLabel = '';
      this.windowTone = 'closed';
      return;
    }
    const expiresMs = this.resolveExpiresMs(conv);
    if (expiresMs == null) {
      this.windowLabel = 'Window closed — template required';
      this.windowTone = 'closed';
      conv.isWithinMessagingWindow = false;
      return;
    }
    const remaining = expiresMs - this.nowMs;
    if (remaining <= 0) {
      this.windowLabel = 'Window closed — template required';
      this.windowTone = 'closed';
      conv.isWithinMessagingWindow = false;
      return;
    }
    conv.isWithinMessagingWindow = true;
    const hours = Math.floor(remaining / 3600000);
    const mins = Math.floor((remaining % 3600000) / 60000);
    const secs = Math.floor((remaining % 60000) / 1000);
    this.windowLabel = `Window ${hours}h ${mins}m ${secs}s`;
    this.windowTone = remaining < 3600000 ? 'amber' : 'open';
  }

  private resolveExpiresMs(conv: WhatsAppConversation): number | null {
    if (conv.windowExpiresAt) {
      const t = new Date(conv.windowExpiresAt).getTime();
      return Number.isNaN(t) ? null : t;
    }
    if (conv.lastIncomingMessageAt) {
      const t = new Date(conv.lastIncomingMessageAt).getTime();
      return Number.isNaN(t) ? null : t + 24 * 3600000;
    }
    return null;
  }

  toggleAiAssist(): void {
    if (!this.canAiAssist) return;
    this.aiAssistOpen = !this.aiAssistOpen;
    if (!this.aiAssistOpen) this.aiError = null;
  }

  generateAiReply(): void {
    if (!this.selected || !this.canAiAssist || this.aiBusy) return;
    this.aiBusy = true;
    this.aiError = null;
    this.inbox.aiSuggest(this.selected.id).subscribe({
      next: result => {
        this.aiBusy = false;
        this.applyAiResult(result);
      },
      error: err => this.onAiError(err)
    });
  }

  regenerateAiReply(): void {
    if (!this.selected || !this.canAiAssist || this.aiBusy) return;
    this.aiBusy = true;
    this.aiError = null;
    this.inbox
      .aiTransform(this.selected.id, {
        action: 'Regenerate',
        priorSuggestion: this.aiResult?.suggestion ?? this.draft
      })
      .subscribe({
        next: result => {
          this.aiBusy = false;
          this.applyAiResult(result);
        },
        error: err => this.onAiError(err)
      });
  }

  transformAi(action: 'Shorter' | 'Professional' | 'Translate'): void {
    if (!this.selected || !this.canAiAssist || this.aiBusy) return;
    const text = (this.aiResult?.suggestion || this.draft || '').trim();
    if (!text) {
      this.toast.warning('Generate or type a reply first.');
      return;
    }
    this.aiBusy = true;
    this.aiError = null;
    this.inbox
      .aiTransform(this.selected.id, {
        action,
        text,
        targetLanguage: action === 'Translate' ? this.aiTranslateLang : null,
        priorSuggestion: this.aiResult?.suggestion ?? null
      })
      .subscribe({
        next: result => {
          this.aiBusy = false;
          this.applyAiResult(result);
        },
        error: err => this.onAiError(err)
      });
  }

  summarizeConversation(): void {
    if (!this.selected || !this.canAiAssist || this.aiBusy) return;
    this.aiBusy = true;
    this.aiError = null;
    this.inbox.aiSummary(this.selected.id).subscribe({
      next: result => {
        this.aiBusy = false;
        this.aiSummary = result.suggestion;
        this.aiResult = result;
      },
      error: err => this.onAiError(err)
    });
  }

  useAiSuggestionInDraft(): void {
    if (!this.aiResult?.suggestion) return;
    this.draft = this.aiResult.suggestion;
  }

  sendAiSuggestion(): void {
    if (!this.aiResult?.suggestion) return;
    this.draft = this.aiResult.suggestion;
    this.send();
  }

  private applyAiResult(result: WhatsAppAiAssistResult): void {
    this.aiResult = result;
    this.draft = result.suggestion;
    if (result.reviewRecommended) {
      this.toast.warning(result.confidenceReason || 'Human review recommended');
    }
  }

  private onAiError(err: unknown): void {
    this.aiBusy = false;
    const msg =
      (err as { error?: { message?: string }; message?: string })?.error?.message ||
      (err as { message?: string })?.message ||
      'AI assist failed.';
    this.aiError = msg;
    this.toast.error(msg);
  }

  private resetAiAssist(close = true): void {
    if (close) this.aiAssistOpen = false;
    this.aiBusy = false;
    this.aiError = null;
    this.aiResult = null;
    this.aiSummary = null;
  }

  private applyConversationUpdate(updated: WhatsAppConversation): void {
    this.selected = updated;
    this.refreshWindowBadge();
    const idx = this.conversations.findIndex(c => c.id === updated.id);
    if (idx >= 0) this.conversations[idx] = updated;
    else this.refreshConversations(false);
  }

  private onRealtime(ev: WhatsAppRealtimeEvent): void {
    const type = ev.type || '';
    if (type === 'whatsapp.message_status' && ev.conversationId && this.selected?.id === ev.conversationId) {
      const msg = this.messages.find(m => m.id === ev.messageId || m.metaMessageId === ev.metaMessageId);
      if (msg && ev.status) {
        // Forward-only client-side: ignore regressions if any.
        const rank = (s: string) => {
          const k = s.toLowerCase();
          if (k === 'queued' || k === 'sending') return 1;
          if (k === 'sent') return 2;
          if (k === 'delivered') return 3;
          if (k === 'read') return 4;
          if (k === 'failed') return 100;
          return 0;
        };
        if (rank(ev.status) >= rank(msg.status || '')) msg.status = ev.status;
      }
      return;
    }

    if (type === 'whatsapp.conversation_updated' || type === 'whatsapp.unread_count_changed') {
      if (ev.conversationId != null) {
        const row = this.conversations.find(c => c.id === ev.conversationId);
        if (row) {
          if (ev.unreadCount != null) row.unreadCount = ev.unreadCount;
          if (ev.status) row.status = ev.status;
          if (ev.assignedUserId !== undefined) row.assignedUserId = ev.assignedUserId ?? null;
          if (ev.assignedUserName !== undefined) row.assignedUserName = ev.assignedUserName ?? null;
          if (ev.isBotEnabled != null) row.isBotEnabled = ev.isBotEnabled;
          if (ev.currentBotState !== undefined) row.currentBotState = ev.currentBotState ?? null;
          if (this.selected?.id === ev.conversationId) {
            this.selected = { ...row };
            this.refreshWindowBadge();
          }
        } else {
          this.refreshConversations(false);
        }
      } else {
        this.refreshConversations(false);
      }
      return;
    }

    if (type === 'whatsapp.message') {
      this.refreshConversations(false);
      if (ev.conversationId && this.selected?.id === ev.conversationId) {
        this.openConversation(this.selected, false);
      }
      return;
    }

    this.refreshConversations(false);
    if (this.selected) this.openConversation(this.selected, false);
  }
}
