import { Component, ElementRef, OnInit, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatChipsModule } from '@angular/material/chips';
import {
  AiAssistantQueryResponse,
  AiChatSession,
  AiChatTurnResponse,
  AiPendingAction,
  AiPlatformService,
  AiProviderHealth
} from '../../../core/services/ai-platform.service';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';

interface ChatBubble {
  role: 'user' | 'assistant' | 'system';
  content: string;
  mode?: string;
  usedLlm?: boolean;
  tools?: string[];
  period?: { start: string; end: string };
  warnings?: string[];
  data?: unknown;
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
    MatChipsModule
  ],
  templateUrl: './ai-chat.component.html',
  styleUrls: ['./ai-chat.component.scss']
})
export class AiChatComponent implements OnInit {
  @ViewChild('scrollAnchor') scrollAnchor?: ElementRef<HTMLDivElement>;

  loading = true;
  sending = false;
  draft = '';
  sessionId: string | null = null;
  pendingAction: AiPendingAction | null = null;
  messages: ChatBubble[] = [];
  sessions: AiChatSession[] = [];
  /** Default to Phase 2 assistant (allowlisted reports). Toggle keeps legacy chat/writes. */
  useAssistant = true;
  suggestions: string[] = [
    'What is our daily, weekly, and monthly revenue?',
    'Which vehicle consumed the most fuel this month?',
    'Who is the best performing driver today?',
    'Show trip and booking analytics this month.',
    'What is our cost per kilometer this month?',
    'What is our fleet utilization rate this month?',
    'Summarize overspeed and geofence violations this month.',
    'Which vehicles have maintenance due?',
    'Which vehicles are at maintenance risk?',
    'Which trips are at risk of delay?',
    'Show fuel anomaly investigation for this month.',
    'Show idle time report this month.',
    'Any SOS events this month?',
    'Summarize expenses this month.',
    'What are our peak booking hours this month?'
  ];
  providerHealth: AiProviderHealth | null = null;

  constructor(
    private ai: AiPlatformService,
    private toast: UiToastService
  ) {}

  ngOnInit(): void {
    this.refresh();
  }

  refresh(): void {
    this.loading = true;
    this.ai.getProviderHealth().subscribe({
      next: h => (this.providerHealth = h),
      error: () => {}
    });
    this.ai.listChatSessions().subscribe({
      next: s => {
        this.sessions = s;
        this.loading = false;
      },
      error: err => {
        this.loading = false;
        const status = err?.status ?? err?.statusCode;
        if (status === 403) {
          this.toast.error(
            'AAS Assistant is not enabled for this company. Ask a platform admin to enable the AI module.'
          );
          return;
        }
        this.toast.error('Could not load chat sessions');
      }
    });
  }

  newChat(): void {
    this.sessionId = null;
    this.messages = [];
    this.draft = '';
    this.pendingAction = null;
  }

  openSession(session: AiChatSession): void {
    this.sessionId = session.id;
    this.messages = [];
    this.pendingAction = null;
    this.ai.getChatMessages(session.id).subscribe({
      next: rows => {
        this.messages = rows
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
          this.toast.error(this.aiErrorMessage(err, 'Assistant request failed'));
        }
      });
      return;
    }

    this.ai.chat(text, this.sessionId, confirmWrite).subscribe({
      next: (res: AiChatTurnResponse) => this.onReply(res),
      error: err => {
        this.sending = false;
        this.toast.error(this.aiErrorMessage(err, 'Chat request failed'));
      }
    });
  }

  private aiErrorMessage(err: { status?: number; error?: { message?: string } }, fallback: string): string {
    if (err?.status === 403) {
      return err.error?.message
        ?? 'AAS Assistant is not enabled for this company. Ask a platform admin to enable the AI module.';
    }
    return fallback;
  }

  private onAssistantReply(res: AiAssistantQueryResponse): void {
    this.sending = false;
    this.messages.push({
      role: 'assistant',
      content: res.answer,
      mode: res.mode,
      usedLlm: res.usedLlm,
      tools: res.toolUsed ? [res.toolUsed] : [],
      period: res.period,
      warnings: res.warnings ?? [],
      data: res.data
    });
    this.scrollToBottom();
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
    const tools =
      res.toolsUsed?.filter(t =>
        !['llm_chat', 'llm_fallback', 'pending_confirm', 'confirm_executed', 'confirm_failed'].includes(t)
      ) ?? [];
    this.messages.push({
      role: 'assistant',
      content: res.answer,
      mode: res.mode,
      usedLlm: res.usedLlm,
      tools
    });
    this.pendingAction = res.pendingAction ?? null;
    if (res.mode === 'confirm_executed') {
      this.pendingAction = null;
      this.toast.success('Action applied');
    }
    this.refresh();
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
