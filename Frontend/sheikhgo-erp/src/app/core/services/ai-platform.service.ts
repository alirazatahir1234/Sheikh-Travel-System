import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface FleetHealth {
  healthPercent: number;
  gpsOnlineRate: number;
  maintenanceScore: number;
  complianceScore: number;
  driverScore: number;
  criticalAlerts: number;
  summary: string;
}

export interface AiRecommendation {
  id: number;
  entityType: string;
  entityId: number;
  category: string;
  severity: string;
  title: string;
  action: string;
  source: string;
  score?: number;
  createdAt: string;
}

export interface AiPrediction {
  id: number;
  entityType: string;
  entityId: number;
  predictionType: string;
  probability: number;
  expectedDays?: number;
  label?: string;
  modelVersion: string;
  createdAt: string;
}

export interface AiProviderConfig {
  provider: string;
  isEnabled: boolean;
  copilotEnabled: boolean;
  decisionEngineEnabled: boolean;
  digestEnabled: boolean;
  predictionsEnabled: boolean;
  monthlyBudgetUsd?: number;
  softTokenLimit?: number;
  apiEndpoint?: string;
  modelName?: string;
}

export interface AiCopilotResponse {
  answer: string;
  mode: string;
  toolsUsed: string[];
  usedLlm: boolean;
}

export interface AiAssistantPeriod {
  start: string;
  end: string;
}

export interface AiAssistantQueryResponse {
  answer: string;
  period: AiAssistantPeriod;
  toolUsed?: string | null;
  data?: unknown;
  warnings: string[];
  usedLlm: boolean;
  mode: string;
  /** Phase 7 — persisted conversation id for follow-ups. */
  sessionId?: string | null;
  /** Pending write (e.g. tracker registration ready for CONFIRM). */
  pendingAction?: AiPendingAction | null;
}

export interface AiChatMessage {
  role: string;
  content: string;
  /** JSON for rich UI widgets (trip/sales analytics) — rehydrated from history. */
  structuredPayload?: string | null;
  toolUsed?: string | null;
  createdAt?: string | null;
}

export interface AiChatSession {
  id: string;
  title: string;
  createdAt: string;
  updatedAt: string;
  messageCount: number;
}

export interface AiChatTurnResponse {
  sessionId: string;
  answer: string;
  mode: string;
  usedLlm: boolean;
  provider: string;
  model?: string;
  suggestedPrompts: string[];
  toolsUsed: string[];
  pendingAction?: AiPendingAction | null;
}

export interface AiPendingAction {
  toolName: string;
  summary: string;
  expiresAt: string;
}

export interface AiProviderHealth {
  provider: string;
  model?: string;
  endpoint?: string;
  configured: boolean;
  reachable: boolean;
  statusMessage: string;
}

export interface EscalationRule {
  id: number;
  tenantId?: number;
  eventType: string;
  levelOrder: number;
  targetRole: string;
  timeoutMinutes: number;
  channel: string;
  isActive: boolean;
}

export interface EscalationPending {
  id: number;
  eventType: string;
  currentLevel: number;
  referenceId?: number;
  alertEventId?: number;
  nextEscalateAt?: string;
  status: string;
  createdAt: string;
}

@Injectable({ providedIn: 'root' })
export class AiPlatformService {
  private readonly base = `${environment.apiUrl}/ai`;

  constructor(private http: HttpClient) {}

  getHealth(): Observable<FleetHealth> {
    return this.http.get<FleetHealth>(`${this.base}/health`);
  }

  getRecommendations(): Observable<AiRecommendation[]> {
    return this.http.get<AiRecommendation[]>(`${this.base}/recommendations`);
  }

  getPredictions(entityType?: string): Observable<AiPrediction[]> {
    const params = entityType ? { entityType } : undefined;
    return this.http.get<AiPrediction[]>(`${this.base}/predictions`, { params });
  }

  runPredictions(): Observable<AiPrediction[]> {
    return this.http.post<AiPrediction[]>(`${this.base}/predictions/run`, {});
  }

  generateDigest(): Observable<{ generated: boolean }> {
    return this.http.post<{ generated: boolean }>(`${this.base}/digest/morning`, {});
  }

  ask(question: string): Observable<AiCopilotResponse> {
    return this.http.post<AiCopilotResponse>(`${this.base}/copilot/ask`, { question });
  }

  /** Phase 2 AI Assistant — allowlisted read-only reporting tools. */
  assistantQuery(question: string, sessionId?: string | null): Observable<AiAssistantQueryResponse> {
    return this.http.post<AiAssistantQueryResponse>(`${this.base}/assistant/query`, {
      question,
      sessionId: sessionId ?? null
    });
  }

  /**
   * Assistant query with file attachments (text/CSV/JSON/Markdown/images/PDF).
   * Text is inlined server-side; images/PDF are noted without storing binary in chat.
   */
  assistantQueryWithFiles(
    question: string,
    files: File[],
    sessionId?: string | null
  ): Observable<AiAssistantQueryResponse> {
    const form = new FormData();
    form.append('question', question ?? '');
    if (sessionId) form.append('sessionId', sessionId);
    for (const f of files) {
      form.append('files', f, f.name);
    }
    return this.http.post<AiAssistantQueryResponse>(`${this.base}/assistant/query-with-files`, form);
  }

  assistantTools(): Observable<string[]> {
    return this.http.get<string[]>(`${this.base}/assistant/tools`);
  }

  chat(
    message: string,
    sessionId?: string | null,
    confirmWrite = false
  ): Observable<AiChatTurnResponse> {
    return this.http.post<AiChatTurnResponse>(`${this.base}/chat`, {
      message: confirmWrite ? (message || 'CONFIRM') : message,
      sessionId: sessionId ?? null,
      confirmWrite
    });
  }

  getPendingAction(sessionId: string): Observable<AiPendingAction | null> {
    return this.http.get<AiPendingAction | null>(`${this.base}/chat/sessions/${sessionId}/pending`);
  }

  listChatSessions(): Observable<AiChatSession[]> {
    return this.http.get<AiChatSession[]>(`${this.base}/chat/sessions`);
  }

  getChatMessages(sessionId: string): Observable<AiChatMessage[]> {
    return this.http.get<AiChatMessage[]>(`${this.base}/chat/sessions/${sessionId}/messages`);
  }

  getProviderHealth(): Observable<AiProviderHealth> {
    return this.http.get<AiProviderHealth>(`${this.base}/chat/provider-health`);
  }

  getConfig(): Observable<AiProviderConfig> {
    return this.http.get<AiProviderConfig>(`${this.base}/management/config`);
  }

  saveConfig(config: AiProviderConfig): Observable<AiProviderConfig> {
    return this.http.put<AiProviderConfig>(`${this.base}/management/config`, config);
  }

  getEscalationRules(): Observable<EscalationRule[]> {
    return this.http.get<EscalationRule[]>(`${this.base}/escalation/rules`);
  }

  saveEscalationRule(rule: EscalationRule): Observable<EscalationRule> {
    return this.http.put<EscalationRule>(`${this.base}/escalation/rules`, rule);
  }

  getPendingEscalations(): Observable<EscalationPending[]> {
    return this.http.get<EscalationPending[]>(`${this.base}/escalation/pending`);
  }

  ackEscalation(id: number): Observable<{ acknowledged: boolean }> {
    return this.http.post<{ acknowledged: boolean }>(`${this.base}/escalation/${id}/ack`, {});
  }

  getDatasets(): Observable<{ name: string; rowCount: number; lastCapturedAt?: string; freshness: string }[]> {
    return this.http.get<{ name: string; rowCount: number; lastCapturedAt?: string; freshness: string }[]>(`${this.base}/datasets`);
  }

  recordLearning(eventType: string, action: string): Observable<{ recorded: boolean }> {
    return this.http.post<{ recorded: boolean }>(`${this.base}/learning`, { eventType, action });
  }
}
