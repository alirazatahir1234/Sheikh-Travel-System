import { Component, ElementRef, HostBinding, Input, OnInit, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import {
  AiAssistantQueryResponse,
  AiChatSession,
  AiChatTurnResponse,
  AiPendingAction,
  AiPlatformService
} from '../../../core/services/ai-platform.service';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';

interface ChatBubble {
  role: 'user' | 'assistant' | 'system';
  content: string;
  period?: { start: string; end: string };
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
    MatProgressSpinnerModule
  ],
  templateUrl: './ai-chat.component.html',
  styleUrls: ['./ai-chat.component.scss']
})
export class AiChatComponent implements OnInit {
  /** Narrow slide-over / FAB embed — tighter layout, fewer chips. */
  @Input() compact = false;

  @HostBinding('class.is-host-compact')
  get hostCompact(): boolean {
    return this.compact;
  }

  @ViewChild('scrollAnchor') scrollAnchor?: ElementRef<HTMLDivElement>;

  loading = true;
  sending = false;
  draft = '';
  sessionId: string | null = null;
  pendingAction: AiPendingAction | null = null;
  messages: ChatBubble[] = [];
  sessions: AiChatSession[] = [];
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
    private toast: UiToastService
  ) {}

  ngOnInit(): void {
    this.refresh();
  }

  /** Suggestion chips — always visible when idle; compact drawer shows fewer. */
  get visibleSuggestions(): string[] {
    if (this.sending || !!this.accessDeniedMessage || !this.suggestions.length) return [];
    const limit = this.compact ? 5 : 8;
    return this.suggestions.slice(0, limit);
  }

  get hasSessions(): boolean {
    return !this.accessDeniedMessage && this.sessions.length > 0;
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
        // Sessions failed but chat may still work — keep a connected UI, toast only.
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
    this.pendingAction = null;
    this.suggestions = [...this.defaultSuggestions];
  }

  openSession(session: AiChatSession): void {
    this.sessionId = session.id;
    this.messages = [];
    this.pendingAction = null;
    this.ai.getChatMessages(session.id).subscribe({
      next: rows => {
        this.messages = (rows ?? [])
          .filter(m => m.role === 'user' || m.role === 'assistant')
          .map(m => ({ role: m.role as 'user' | 'assistant', content: m.content }));
        this.scrollToBottom();
      },
      error: () => this.toast.error('Could not load messages')
    });
    this.loadPendingAction(session.id);
  }

  useSuggestion(text: string): void {
    this.draft = text;
    this.send();
  }

  send(confirmWrite = false): void {
    const text = confirmWrite ? 'CONFIRM' : this.draft.trim();
    if (!text || this.sending) return;
    if (confirmWrite && !this.sessionId) {
      this.toast.error('No active session to confirm');
      return;
    }

    this.messages.push({ role: 'user', content: text });
    if (!confirmWrite) this.draft = '';

    this.sending = true;
    this.scrollToBottom();

    if (this.useAssistant && !confirmWrite) {
      this.ai.assistantQuery(text).subscribe({
        next: (res: AiAssistantQueryResponse) => this.onAssistantReply(res),
        error: err => {
          this.sending = false;
          this.toast.error(this.aiErrorMessage(err, 'Could not get a reply. Please try again.'));
        }
      });
      return;
    }

    this.ai.chat(text, this.sessionId, confirmWrite).subscribe({
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
    this.messages.push({
      role: 'assistant',
      content: res?.answer ?? 'No answer returned.',
      period: this.shouldShowPeriod(res) ? res.period : undefined
    });
    this.scrollToBottom();
  }

  private shouldShowPeriod(res: AiAssistantQueryResponse): boolean {
    const tool = (res.toolUsed ?? '').toLowerCase();
    if (!tool) return false;
    if (tool === 'getofflinevehicles' || tool === 'getfleethealth') return false;
    return !!res.period?.start && !!res.period?.end;
  }

  confirmWrite(): void {
    this.send(true);
  }

  dismissConfirm(): void {
    this.pendingAction = null;
  }

  private onReply(res: AiChatTurnResponse): void {
    this.sending = false;
    this.sessionId = res.sessionId;
    if (res.suggestedPrompts?.length) {
      this.suggestions = res.suggestedPrompts;
    }
    this.messages.push({
      role: 'assistant',
      content: res?.answer ?? 'No answer returned.'
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

  private scrollToBottom(): void {
    requestAnimationFrame(() => {
      this.scrollAnchor?.nativeElement?.scrollIntoView({ behavior: 'smooth' });
    });
  }
}
