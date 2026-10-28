import { Component, ElementRef, HostBinding, Input, OnInit, ViewChild } from '@angular/core';
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
export class AiChatComponent implements OnInit {
  readonly aiUi = AI_ASSISTANT_UI;

  /** Narrow slide-over / FAB embed — tighter layout, fewer chips. */
  @Input() compact = false;

  @HostBinding('class.is-host-compact')
  get hostCompact(): boolean {
    return this.compact;
  }

  @ViewChild('scrollAnchor') scrollAnchor?: ElementRef<HTMLDivElement>;
  @ViewChild('composerInput') composerInput?: ElementRef<HTMLTextAreaElement>;
  @ViewChild('fileInput') fileInput?: ElementRef<HTMLInputElement>;

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

  private readonly defaultSuggestions: string[] = [
    'How many vehicles are offline?',
    'How healthy is my fleet today?',
    'What is our daily, weekly, and monthly revenue?',
    'Which vehicles have maintenance due?',
    'Who is the best performing driver today?',
    'Show trip and booking analytics this month.',
    'What is our cost per kilometer this month?',
    'Any SOS events this month?',
    'What are our peak booking hours this month?'
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

  /** Suggestion chips — always visible when idle; compact drawer shows fewer. */
  get visibleSuggestions(): string[] {
    if (this.sending || !!this.accessDeniedMessage || !this.suggestions.length) return [];
    const limit = this.compact ? 4 : 5;
    return this.suggestions.slice(0, limit);
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
    return !this.compact && !this.accessDeniedMessage;
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
      content: polishAssistantEmojis(res?.answer ?? 'No answer returned.')
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
  }): ChatBubble {
    let data: unknown;
    if (m.structuredPayload) {
      try {
        data = JSON.parse(m.structuredPayload);
      } catch {
        data = undefined;
      }
    }
    return {
      role: m.role as 'user' | 'assistant',
      content: polishAssistantEmojis(m.content ?? ''),
      toolUsed: m.toolUsed ?? undefined,
      data,
      warnings: []
    };
  }

  private scrollToBottom(): void {
    requestAnimationFrame(() => {
      this.scrollAnchor?.nativeElement?.scrollIntoView({ behavior: 'smooth' });
    });
  }
}
