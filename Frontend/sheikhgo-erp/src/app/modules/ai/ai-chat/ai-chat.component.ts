import {
  AfterViewChecked,
  Component,
  ElementRef,
  EventEmitter,
  HostBinding,
  Input,
  OnDestroy,
  OnInit,
  Output,
  ViewChild
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatMenuModule } from '@angular/material/menu';
import { MatTooltipModule } from '@angular/material/tooltip';
import {
  AiAssistantQueryResponse,
  AiChatSession,
  AiChatTurnResponse,
  AiPendingAction,
  AiPlatformService
} from '../../../core/services/ai-platform.service';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { AiAssistantResultComponent } from '../shared/ai-assistant-result/ai-assistant-result.component';
import { AiAssistantOpenSoundService } from '../services/ai-assistant-open-sound.service';
import { AI_ASSISTANT_UI, polishAssistantEmojis } from '../shared/ai-assistant-ui.constants';
import { extractFollowUps } from '../shared/assistant-answer.util';

interface ChatBubble {
  role: 'user' | 'assistant' | 'system';
  content: string;
  /** Local wall-clock for mock-style bubble timestamps. */
  at?: Date;
  period?: { start: string; end: string };
  toolUsed?: string | null;
  data?: unknown;
  warnings?: string[];
  mode?: string;
  /** Local attach chips (not persisted as binary). */
  attachments?: { name: string; kind: string }[];
}

const ATTACH_ACCEPT =
  '.txt,.md,.csv,.json,.log,.tsv,.png,.jpg,.jpeg,.webp,.gif,.pdf,text/plain,text/csv,application/json,image/png,image/jpeg,image/webp,image/gif,application/pdf';
const ATTACH_MAX_FILES = 3;
const ATTACH_MAX_BYTES = 2 * 1024 * 1024;

interface SessionGroup {
  label: string;
  sessions: AiChatSession[];
}

interface AiSuggestionCard {
  text: string;
  icon: string;
  tone: 'danger' | 'success' | 'teal' | 'purple' | 'info';
}

@Component({
  selector: 'app-ai-chat',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatIconModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatMenuModule,
    MatTooltipModule,
    AiAssistantResultComponent
  ],
  templateUrl: './ai-chat.component.html',
  styleUrls: ['./ai-chat.component.scss']
})
export class AiChatComponent implements OnInit, AfterViewChecked, OnDestroy {
  readonly aiUi = AI_ASSISTANT_UI;

  /** FAB slide-over embed — tighter host sizing. */
  @Input() compact = false;
  /** Two-panel mock layout (left AI rail + right response workspace). */
  @Input() workspace = false;

  /** Close the FAB panel (dashboard header). */
  @Output() closeWorkspace = new EventEmitter<void>();
  /** Expand to full /ai page. */
  @Output() expandWorkspace = new EventEmitter<void>();

  @HostBinding('class.is-host-compact')
  get hostCompact(): boolean {
    return this.compact || this.workspace;
  }

  @ViewChild('messagesPane') messagesPane?: ElementRef<HTMLDivElement>;
  @ViewChild('messagesContent') messagesContent?: ElementRef<HTMLDivElement>;
  @ViewChild('scrollAnchor') scrollAnchor?: ElementRef<HTMLDivElement>;
  @ViewChild('composerInput') composerInput?: ElementRef<HTMLTextAreaElement>;
  @ViewChild('fileInput') fileInput?: ElementRef<HTMLInputElement>;

  /** Keep pinned to latest reply unless the user scrolls up to read history. */
  private stickToBottom = true;
  private contentObserver?: ResizeObserver;
  private observedContent: HTMLElement | null = null;

  readonly attachAccept = ATTACH_ACCEPT;

  loading = true;
  sending = false;
  draft = '';
  sessionSearch = '';
  sessionId: string | null = null;
  pendingAction: AiPendingAction | null = null;
  messages: ChatBubble[] = [];
  sessions: AiChatSession[] = [];
  /** Pending composer attachments (cleared after send). */
  pendingFiles: File[] = [];
  /** Set when sessions API returns 403 (module off / no chat permission). */
  accessDeniedMessage: string | null = null;
  /** Default to Phase 2 assistant (allowlisted reports). Toggle keeps legacy chat/writes. */
  useAssistant = true;
  /** User-facing connection strip only — no provider/model/diagnostics. */
  connectionState: 'connecting' | 'connected' = 'connecting';

  /** Mock-matching suggested question cards (left rail). */
  readonly dashboardSuggestionCards: AiSuggestionCard[] = [
    { text: 'How many vehicles are offline?', icon: 'wifi_off', tone: 'danger' },
    { text: "Show today's trips", icon: 'bar_chart', tone: 'success' },
    { text: 'Check vehicle health', icon: 'monitor_heart', tone: 'teal' },
    { text: 'Pending bookings', icon: 'calendar_month', tone: 'purple' },
    { text: 'Fuel consumption report', icon: 'local_gas_station', tone: 'info' }
  ];

  private readonly defaultSuggestions: string[] = [
    'How many vehicles are offline?',
    "Show today's trips",
    'Check vehicle health',
    'Pending bookings',
    'Fuel consumption report',
    'How healthy is my fleet today?',
    'Which vehicles have maintenance due?',
    'Who is the best performing driver today?'
  ];

  suggestions: string[] = [...this.defaultSuggestions];

  constructor(
    private ai: AiPlatformService,
    private toast: UiToastService,
    private aiAssistantOpenSound: AiAssistantOpenSoundService
  ) {}

  ngOnInit(): void {
    this.aiAssistantOpenSound.notifyAssistantOpened();
    this.refresh();
  }

  ngAfterViewChecked(): void {
    this.ensureContentObserver();
  }

  ngOnDestroy(): void {
    this.contentObserver?.disconnect();
    this.contentObserver = undefined;
    this.observedContent = null;
  }

  /** Track whether the user is near the bottom so widget growth can re-pin. */
  onMessagesScroll(): void {
    const el = this.messagesPane?.nativeElement;
    if (!el) return;
    const distanceFromBottom = el.scrollHeight - el.scrollTop - el.clientHeight;
    this.stickToBottom = distanceFromBottom < 96;
  }

  /** Suggestion chips — full-page chat only. */
  get visibleSuggestions(): string[] {
    if (this.sending || !!this.accessDeniedMessage || !this.suggestions.length) return [];
    return this.suggestions.slice(0, 5);
  }

  /** Left-rail prompt cards — prefer contextual follow-ups after a reply. */
  get visibleSuggestionCards(): AiSuggestionCard[] {
    if (this.sending || this.accessDeniedMessage) return [];
    const followUps = this.suggestions.filter(
      s => !this.dashboardSuggestionCards.some(c => c.text === s)
    );
    if (!followUps.length || this.messages.length === 0) {
      return this.dashboardSuggestionCards;
    }
    return followUps.slice(0, 5).map((text, i) => {
      const fallback = this.dashboardSuggestionCards[i % this.dashboardSuggestionCards.length];
      return { text, icon: fallback.icon, tone: fallback.tone };
    });
  }

  get hasSessions(): boolean {
    return !this.accessDeniedMessage && this.sessions.length > 0;
  }

  get filteredSessions(): AiChatSession[] {
    const q = this.sessionSearch.trim().toLowerCase();
    if (!q) return this.sessions;
    return this.sessions.filter(s => (s.title || '').toLowerCase().includes(q));
  }

  get sessionGroups(): SessionGroup[] {
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    const yesterday = new Date(today);
    yesterday.setDate(yesterday.getDate() - 1);
    const weekAgo = new Date(today);
    weekAgo.setDate(weekAgo.getDate() - 7);

    const buckets: Record<string, AiChatSession[]> = {
      Today: [],
      Yesterday: [],
      'Last 7 days': [],
      Older: []
    };

    for (const s of this.filteredSessions) {
      const raw = s.updatedAt || s.createdAt;
      const d = raw ? new Date(raw) : null;
      if (!d || Number.isNaN(d.getTime())) {
        buckets['Older'].push(s);
        continue;
      }
      const day = new Date(d);
      day.setHours(0, 0, 0, 0);
      if (day.getTime() >= today.getTime()) buckets['Today'].push(s);
      else if (day.getTime() >= yesterday.getTime()) buckets['Yesterday'].push(s);
      else if (day.getTime() >= weekAgo.getTime()) buckets['Last 7 days'].push(s);
      else buckets['Older'].push(s);
    }

    return Object.entries(buckets)
      .filter(([, list]) => list.length > 0)
      .map(([label, sessions]) => ({ label, sessions }));
  }

  get showSessionsPanel(): boolean {
    // Workspace mock is a dedicated two-panel surface; history stays available via New Chat / full page later.
    if (this.workspace) return false;
    return !this.compact && !this.accessDeniedMessage;
  }

  onClose(): void {
    this.closeWorkspace.emit();
  }

  onExpand(): void {
    if (this.compact) {
      this.expandWorkspace.emit();
      return;
    }
    this.newChat();
  }

  copyAnswer(m: ChatBubble): void {
    const text = (m.content || '').trim();
    if (!text || typeof navigator === 'undefined' || !navigator.clipboard?.writeText) {
      this.toast.error('Copy is unavailable in this browser.');
      return;
    }
    void navigator.clipboard.writeText(text).then(
      () => this.toast.success('Answer copied'),
      () => this.toast.error('Could not copy answer')
    );
  }

  refresh(): void {
    this.loading = true;
    this.connectionState = 'connecting';
    this.accessDeniedMessage = null;
    this.loadSessions(true);
  }

  /** Reload session list without blanking the message pane (after a chat turn). */
  private reloadSessionsQuietly(): void {
    this.loadSessions(false);
  }

  private loadSessions(showLoading: boolean): void {
    if (showLoading) {
      this.loading = true;
      this.connectionState = 'connecting';
    }

    this.ai.listChatSessions().subscribe({
      next: s => {
        this.sessions = s ?? [];
        this.loading = false;
        this.connectionState = 'connected';
      },
      error: err => {
        this.loading = false;
        this.sessions = [];
        const status = err?.status ?? err?.statusCode;
        if (status === 403) {
          this.accessDeniedMessage =
            'SheikhGo AI Assistant is not enabled for this company, or you lack chat permission. Ask a platform admin to enable the AI module.';
          this.toast.error(this.accessDeniedMessage);
          return;
        }
        this.connectionState = 'connected';
        if (showLoading) {
          this.toast.error('Could not load chat sessions');
        }
      }
    });
  }

  newChat(): void {
    this.sessionId = null;
    this.messages = [];
    this.draft = '';
    this.pendingFiles = [];
    this.pendingAction = null;
    this.suggestions = [...this.defaultSuggestions];
    queueMicrotask(() => this.composerInput?.nativeElement?.focus());
  }

  openSession(session: AiChatSession): void {
    this.sessionId = session.id;
    this.messages = [];
    this.pendingFiles = [];
    this.pendingAction = null;
    this.suggestions = [...this.defaultSuggestions];
    this.ai.getChatMessages(session.id).subscribe({
      next: rows => {
        this.messages = (rows ?? [])
          .filter(m => m.role === 'user' || m.role === 'assistant')
          .map(m => this.rehydrateBubble(m));
        const lastAssistant = [...this.messages].reverse().find(x => x.role === 'assistant' && x.data);
        const followUps = lastAssistant ? extractFollowUps(lastAssistant.data) : [];
        if (followUps.length) {
          this.suggestions = followUps;
        }
        this.scrollToBottom();
      },
      error: () => this.toast.error('Could not load messages')
    });
    this.loadPendingAction(session.id);
  }

  openAttachPicker(): void {
    if (this.sending || this.accessDeniedMessage) return;
    this.fileInput?.nativeElement?.click();
  }

  onFilesSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const list = Array.from(input.files ?? []);
    input.value = '';
    if (!list.length) return;

    for (const file of list) {
      if (this.pendingFiles.length >= ATTACH_MAX_FILES) {
        this.toast.error(`You can attach up to ${ATTACH_MAX_FILES} files.`);
        break;
      }
      if (file.size > ATTACH_MAX_BYTES) {
        this.toast.error(`${file.name} is larger than 2 MB.`);
        continue;
      }
      if (this.pendingFiles.some(f => f.name === file.name && f.size === file.size)) continue;
      this.pendingFiles = [...this.pendingFiles, file];
    }
  }

  removePendingFile(index: number): void {
    this.pendingFiles = this.pendingFiles.filter((_, i) => i !== index);
  }

  useSuggestion(text: string): void {
    this.draft = text;
    this.send();
  }

  onComposerKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.send();
    }
  }

  send(confirmWrite = false): void {
    const text = confirmWrite ? 'CONFIRM' : this.draft.trim();
    const files = confirmWrite ? [] : [...this.pendingFiles];
    if ((!text && !files.length) || this.sending) return;
    if (confirmWrite && !this.sessionId) {
      this.toast.error('No active session to confirm');
      return;
    }

    const displayText = text || (files.length ? `Analyze attached file${files.length > 1 ? 's' : ''}` : '');
    this.messages.push({
      role: 'user',
      content: polishAssistantEmojis(displayText),
      at: new Date(),
      attachments: files.map(f => ({
        name: f.name,
        kind: f.type?.startsWith('image/') ? 'image' : f.type === 'application/pdf' ? 'pdf' : 'text'
      }))
    });
    if (!confirmWrite) {
      this.draft = '';
      this.pendingFiles = [];
    }

    this.sending = true;
    this.scrollToBottom();

    if (this.useAssistant && !confirmWrite) {
      const question = text || displayText;
      const req$ =
        files.length > 0
          ? this.ai.assistantQueryWithFiles(question, files, this.sessionId)
          : this.ai.assistantQuery(question, this.sessionId);
      req$.subscribe({
        next: (res: AiAssistantQueryResponse) => this.onAssistantReply(res),
        error: err => {
          this.sending = false;
          this.toast.error(this.aiErrorMessage(err, 'Could not get a reply. Please try again.'));
        }
      });
      return;
    }

    this.ai.chat(text || displayText, this.sessionId, confirmWrite).subscribe({
      next: (res: AiChatTurnResponse) => this.onReply(res),
      error: err => {
        this.sending = false;
        this.toast.error(this.aiErrorMessage(err, 'Could not get a reply. Please try again.'));
      }
    });
  }

  private aiErrorMessage(err: { status?: number; error?: { message?: string } }, fallback: string): string {
    if (err?.status === 403) {
      return err.error?.message
        ?? 'SheikhGo AI Assistant is not enabled for this company. Ask a platform admin to enable the AI module.';
    }
    return fallback;
  }

  private onAssistantReply(res: AiAssistantQueryResponse): void {
    this.sending = false;
    if (res?.sessionId) {
      this.sessionId = res.sessionId;
    }
    const followUps = extractFollowUps(res?.data);
    if (followUps.length) {
      this.suggestions = followUps;
    }
    this.messages.push({
      role: 'assistant',
      content: polishAssistantEmojis(res?.answer ?? 'No answer returned.'),
      at: new Date(),
      period: this.shouldShowPeriod(res) ? res.period : undefined,
      toolUsed: res?.toolUsed,
      data: res?.data,
      warnings: res?.warnings ?? [],
      mode: res?.mode
    });
    this.reloadSessionsQuietly();
    this.scrollToBottom();
  }

  private shouldShowPeriod(res: AiAssistantQueryResponse): boolean {
    const tool = (res.toolUsed ?? '').toLowerCase();
    if (!tool) return false;
    if (tool === 'getofflinevehicles' || tool === 'getfleethealth') return false;
    // Rich analytics already shows period context in widgets
    if (
      tool === 'gettripsummary' ||
      tool.includes('get_trip_statistics') ||
      tool.includes('trip_booking') ||
      tool.includes('sales') ||
      tool.includes('getrevenueanalytics')
    ) {
      return false;
    }
    return !!res.period?.start && !!res.period?.end;
  }

  confirmWrite(): void {
    this.send(true);
  }

  dismissConfirm(): void {
    this.pendingAction = null;
  }

  sessionStamp(session: AiChatSession): string {
    const raw = session.updatedAt || session.createdAt;
    if (!raw) return '';
    const d = new Date(raw);
    if (Number.isNaN(d.getTime())) return '';
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    const day = new Date(d);
    day.setHours(0, 0, 0, 0);
    if (day.getTime() === today.getTime()) {
      return d.toLocaleTimeString(undefined, { hour: 'numeric', minute: '2-digit' });
    }
    return d.toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
  }

  private onReply(res: AiChatTurnResponse): void {
    this.sending = false;
    this.sessionId = res.sessionId;
    if (res.suggestedPrompts?.length) {
      this.suggestions = res.suggestedPrompts;
    }
    this.messages.push({
      role: 'assistant',
      content: polishAssistantEmojis(res?.answer ?? 'No answer returned.'),
      at: new Date()
    });
    this.pendingAction = res.pendingAction ?? null;
    if (res.mode === 'confirm_executed') {
      this.pendingAction = null;
      this.toast.success('Action applied');
    }
    this.reloadSessionsQuietly();
    this.scrollToBottom();
  }

  private loadPendingAction(sessionId: string): void {
    this.ai.getPendingAction(sessionId).subscribe({
      next: p => (this.pendingAction = p),
      error: () => {}
    });
  }

  private rehydrateBubble(m: {
    role: string;
    content: string;
    structuredPayload?: string | null;
    toolUsed?: string | null;
    createdAt?: string | null;
  }): ChatBubble {
    let data: unknown;
    if (m.structuredPayload) {
      try {
        data = JSON.parse(m.structuredPayload);
      } catch {
        data = undefined;
      }
    }
    const at = m.createdAt ? new Date(m.createdAt) : undefined;
    return {
      role: m.role as 'user' | 'assistant',
      content: polishAssistantEmojis(m.content ?? ''),
      at: at && !Number.isNaN(at.getTime()) ? at : undefined,
      toolUsed: m.toolUsed ?? undefined,
      data,
      warnings: []
    };
  }

  private ensureContentObserver(): void {
    const content = this.messagesContent?.nativeElement ?? null;
    if (!content || content === this.observedContent) return;
    this.contentObserver?.disconnect();
    this.observedContent = content;
    if (typeof ResizeObserver === 'undefined') return;
    this.contentObserver = new ResizeObserver(() => {
      if (this.stickToBottom) {
        this.scrollToBottom(false);
      }
    });
    this.contentObserver.observe(content);
  }

  /**
   * Scroll the `.messages` overflow pane (not the page). `force` re-pins after
   * send/reply; ResizeObserver uses `force=false` so reading history stays put.
   */
  private scrollToBottom(force = true): void {
    if (force) {
      this.stickToBottom = true;
    } else if (!this.stickToBottom) {
      return;
    }

    requestAnimationFrame(() => {
      const pane = this.messagesPane?.nativeElement;
      if (pane) {
        pane.scrollTop = pane.scrollHeight;
        return;
      }
      this.scrollAnchor?.nativeElement?.scrollIntoView({ behavior: 'smooth', block: 'end' });
    });
  }
}
