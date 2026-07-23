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
  AiChatMessage,
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
  suggestions: string[] = [
    'How healthy is my fleet today?',
    'Which vehicles are offline?',
    'Show critical GPS alerts',
    'What maintenance is overdue?'
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
      error: () => {
        this.loading = false;
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

    this.ai.chat(text, this.sessionId, confirmWrite).subscribe({
      next: (res: AiChatTurnResponse) => this.onReply(res),
      error: () => {
        this.sending = false;
        this.toast.error('Chat request failed');
      }
    });
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
