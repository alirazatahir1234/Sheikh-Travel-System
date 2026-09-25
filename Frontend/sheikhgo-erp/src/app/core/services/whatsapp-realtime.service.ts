import { Injectable, OnDestroy } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';

export interface WhatsAppRealtimeEvent {
  type?: string;
  accountCode?: string;
  conversationId?: number;
  messageId?: number;
  metaMessageId?: string;
  direction?: string;
  status?: string;
  unreadCount?: number;
  assignedUserId?: number | null;
  assignedUserName?: string | null;
  isBotEnabled?: boolean;
  currentBotState?: string | null;
}

@Injectable({ providedIn: 'root' })
export class WhatsAppRealtimeService implements OnDestroy {
  private hub?: signalR.HubConnection;
  private readonly events$ = new Subject<WhatsAppRealtimeEvent>();

  readonly events = this.events$.asObservable();

  constructor(private auth: AuthService) {}

  async start(): Promise<void> {
    if (this.hub) return;
    const hubUrl = environment.apiUrl.replace('/api', '/hubs/whatsapp');
    this.hub = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl, {
        accessTokenFactory: () => this.auth.getToken() ?? ''
      })
      .withAutomaticReconnect()
      .build();

    this.hub.on('ReceiveWhatsAppEvent', (payload: WhatsAppRealtimeEvent) => {
      this.events$.next(payload ?? {});
    });

    try {
      await this.hub.start();
    } catch {
      /* soft-fail: inbox still works via REST */
    }
  }

  async stop(): Promise<void> {
    if (!this.hub) return;
    try {
      await this.hub.stop();
    } catch {
      /* ignore */
    }
    this.hub = undefined;
  }

  ngOnDestroy(): void {
    void this.stop();
    this.events$.complete();
  }
}
