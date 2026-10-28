import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap, shareReplay, of, finalize } from 'rxjs';
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

export interface McpToolDef {
  name: string;
  description: string;
  inputProperties?: string[];
  planned?: boolean;
}

export interface McpGroup {
  id: string;
  name: string;
  description: string;
  icon: string;
  status: McpGroupStatus;
  tools: string[];
  toolDefs?: McpToolDef[];
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
  toolRegistry?: Record<string, McpToolDef>;
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
const CATALOG_ASSET = 'assets/mcp/mcp-catalog.json';

@Injectable({ providedIn: 'root' })
export class McpCatalogService {
  private catalog$?: Observable<McpCatalog>;
  readonly catalog = signal<McpCatalog | null>(null);
  readonly loading = signal(false);
  readonly lastLoadedAt = signal<string | null>(null);
  readonly activity = signal<McpActivityItem[]>(this.readActivity());
  readonly pendingPrompt = signal<{ prompt: string; groupId?: string } | null>(null);

  constructor(private http: HttpClient) {}

  /**
   * Load catalog. By default reuses in-memory Observable.
   * Pass `{ force: true }` (or call {@link reload}) to bust HTTP + shareReplay cache.
   */
  load(options?: { force?: boolean }): Observable<McpCatalog> {
    if (options?.force) {
      this.catalog$ = undefined;
    }
    if (!this.catalog$) {
      this.loading.set(true);
      const url = `${CATALOG_ASSET}?t=${Date.now()}`;
      this.catalog$ = this.http.get<McpCatalog>(url).pipe(
        tap(c => {
          this.catalog.set(c);
          this.lastLoadedAt.set(new Date().toISOString());
        }),
        catchError(() => {
          const fallback = structuredClone(MCP_CATALOG_FALLBACK);
          this.catalog.set(fallback);
          this.lastLoadedAt.set(new Date().toISOString());
          return of(fallback);
        }),
        finalize(() => this.loading.set(false)),
        shareReplay(1)
      );
    }
    return this.catalog$;
  }

  /** Explicit refresh — used by MCP console Refresh control and page load. */
  reload(): Observable<McpCatalog> {
    this.pushActivity('Catalog refresh', 'Reloading mcp-catalog.json');
    return this.load({ force: true });
  }

  toolCount(group: McpGroup): number {
    return group.tools?.length || group.plannedTools?.length || 0;
  }

  registeredToolCount(catalog: McpCatalog | null): number {
    if (!catalog) return 0;
    return catalog.groups.reduce((n, g) => n + (g.tools?.length || 0), 0);
  }

  toolDef(catalog: McpCatalog | null, name: string): McpToolDef | undefined {
    if (!catalog) return undefined;
    if (catalog.toolRegistry?.[name]) return catalog.toolRegistry[name];
    for (const g of catalog.groups) {
      const hit = g.toolDefs?.find(t => t.name === name);
      if (hit) return hit;
    }
    return undefined;
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
