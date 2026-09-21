import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap, shareReplay, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { MCP_CATALOG_FALLBACK } from './mcp-catalog.fallback';

export type McpGroupStatus = 'running' | 'planned' | 'stopped';

export interface McpQuickAction {
  id: string;
  label: string;
  icon: string;
  route?: string;
  prompt?: string;
  groupId?: string;
}

export interface McpGroup {
  id: string;
  name: string;
  description: string;
  icon: string;
  status: McpGroupStatus;
  tools: string[];
  plannedTools?: string[];
  note?: string;
}

export interface McpConnection {
  id: string;
  name: string;
  status: string;
  hint: string;
}

export interface McpQuickLink {
  label: string;
  url: string;
  icon: string;
  internal?: boolean;
}

export interface McpCatalog {
  version: string;
  protocol: string;
  package: string;
  exportedAt: string;
  discoveredTools?: string[];
  summary: {
    toolCount: number;
    runningGroups: number;
    plannedGroups: number;
    stoppedGroups: number;
  };
  suggestions: string[];
  quickActions: McpQuickAction[];
  groups: McpGroup[];
  connections: McpConnection[];
  quickLinks: McpQuickLink[];
}

export interface McpActivityItem {
  id: string;
  at: string;
  label: string;
  detail?: string;
}

const ACTIVITY_KEY = 'sheikhgo.mcp.activity';
const MAX_ACTIVITY = 20;

@Injectable({ providedIn: 'root' })
export class McpCatalogService {
  private catalog$?: Observable<McpCatalog>;
  readonly catalog = signal<McpCatalog | null>(null);
  readonly activity = signal<McpActivityItem[]>(this.readActivity());
  readonly pendingPrompt = signal<{ prompt: string; groupId?: string } | null>(null);

  constructor(private http: HttpClient) {}

  load(): Observable<McpCatalog> {
    if (!this.catalog$) {
      this.catalog$ = this.http.get<McpCatalog>('assets/mcp/mcp-catalog.json').pipe(
        tap(c => this.catalog.set(c)),
        catchError(() => {
          const fallback = structuredClone(MCP_CATALOG_FALLBACK);
          this.catalog.set(fallback);
          return of(fallback);
        }),
        shareReplay(1)
      );
    }
    return this.catalog$;
  }

  toolCount(group: McpGroup): number {
    return group.tools?.length || group.plannedTools?.length || 0;
  }

  registeredToolCount(catalog: McpCatalog | null): number {
    if (!catalog) return 0;
    return catalog.groups.reduce((n, g) => n + (g.tools?.length || 0), 0);
  }

  setPendingPrompt(prompt: string, groupId?: string): void {
    this.pendingPrompt.set({ prompt, groupId });
    this.pushActivity('Prompt prepared', prompt.slice(0, 80));
  }

  consumePendingPrompt(): { prompt: string; groupId?: string } | null {
    const p = this.pendingPrompt();
    this.pendingPrompt.set(null);
    return p;
  }

  pushActivity(label: string, detail?: string): void {
    const item: McpActivityItem = {
      id: `${Date.now()}-${Math.random().toString(36).slice(2, 7)}`,
      at: new Date().toISOString(),
      label,
      detail
    };
    const next = [item, ...this.activity()].slice(0, MAX_ACTIVITY);
    this.activity.set(next);
    try {
      localStorage.setItem(ACTIVITY_KEY, JSON.stringify(next));
    } catch {
      /* ignore quota */
    }
  }

  clearActivity(): void {
    this.activity.set([]);
    try {
      localStorage.removeItem(ACTIVITY_KEY);
    } catch {
      /* ignore */
    }
  }

  private readActivity(): McpActivityItem[] {
    try {
      const raw = localStorage.getItem(ACTIVITY_KEY);
      if (!raw) return [];
      const parsed = JSON.parse(raw) as McpActivityItem[];
      return Array.isArray(parsed) ? parsed : [];
    } catch {
      return [];
    }
  }
}
