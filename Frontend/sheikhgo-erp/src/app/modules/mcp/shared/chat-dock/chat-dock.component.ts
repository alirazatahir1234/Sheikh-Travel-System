import { Component, EventEmitter, Input, OnChanges, OnInit, Output, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { McpCatalog, McpCatalogService, McpGroup } from '../mcp-catalog.service';
import { UiToastService } from '../../../../shared/components/ui/toast/ui-toast.service';

export type McpBuilderPhase = 'draft' | 'prepared';

export interface McpPreparedRequest {
  intent: string;
  group: McpGroup;
  autoDetected: boolean;
  tools: string[];
  operations: string[];
  prompt: string;
}

/** Expand short suggestion chips into editable natural-language requests. */
const SUGGESTION_EXPANSIONS: Record<string, string> = {
  'Analyze the Trip module':
    `Analyze the Trip module and show:

• Related controllers
• Services
• Repositories
• Database tables
• Dependencies
• Potential architectural issues`,
  'Find all vehicle-related code':
    `Find all code related to vehicles and vehicle maintenance.

Explain how the services, controllers, and repositories are connected across the stack.`,
  'Which vehicles need maintenance?':
    `Which vehicles need maintenance?

Summarize maintenance status, overdue items, and related fleet services or tables if available via MCP tools.`,
  'Run all unit tests':
    `Identify and run (or describe how to run) unit tests relevant to recent changes.

Highlight missing tests for key handlers and commands.`,
  'Why did build fail?':
    `Investigate why the latest build may have failed.

Review recent commits, build-related signals, and likely breaking changes.`,
  'Where should vehicle inspection live?':
    `Where should vehicle inspection live in SheikhGo architecture?

Recommend the correct module/layer and note related services, tables, and dependencies.`
};

const GROUP_KEYWORDS: { id: string; words: string[] }[] = [
  { id: 'fleet', words: ['fleet', 'vehicle', 'driver', 'maintenance', 'fuel', 'assignment'] },
  { id: 'gps', words: ['gps', 'traccar', 'tracking', 'geofence', 'position', 'live track'] },
  { id: 'database', words: ['database', 'sql', 'table', 'schema', 'column', 'query'] },
  { id: 'testing', words: ['test', 'unit test', 'coverage', 'spec'] },
  { id: 'logs', words: ['log', 'error', 'exception', 'slow request'] },
  { id: 'devops', words: ['build', 'deploy', 'devops', 'infrastructure', 'pipeline'] },
  { id: 'security', words: ['security', 'auth', 'secret', 'permission', 'vulnerability'] },
  { id: 'analytics', words: ['analytics', 'statistic', 'utilization', 'performance metric'] },
  { id: 'finance', words: ['invoice', 'payment', 'revenue', 'finance'] },
  { id: 'crm', words: ['customer', 'crm', 'booking', 'relationship'] },
  { id: 'code', words: ['search code', 'find code', 'refactor', 'implement', 'git', 'commit', 'diff'] },
  {
    id: 'architecture',
    words: ['architecture', 'module', 'dependency', 'clean architecture', 'adr', 'impact', 'layer']
  }
];

@Component({
  selector: 'app-mcp-chat-dock',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatIconModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule
  ],
  templateUrl: './chat-dock.component.html',
  styleUrls: ['./chat-dock.component.scss']
})
export class McpChatDockComponent implements OnInit, OnChanges {
  @Input() catalog: McpCatalog | null = null;
  @Input() compact = false;
  @Output() copied = new EventEmitter<string>();

  /** 'auto' or a catalog group id */
  groupId = 'auto';
  prompt = '';
  groups: McpGroup[] = [];
  phase: McpBuilderPhase = 'draft';
  prepared: McpPreparedRequest | null = null;
  private pendingApplied = false;

  constructor(
    private mcp: McpCatalogService,
    private toast: UiToastService
  ) {}

  ngOnInit(): void {
    this.applyPending();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['catalog'] && this.catalog) {
      this.groups = this.catalog.groups;
    }
    this.applyPending();
  }

  private applyPending(): void {
    if (this.pendingApplied) return;
    const pending = this.mcp.consumePendingPrompt();
    if (!pending) return;
    this.pendingApplied = true;
    this.prompt = pending.prompt;
    this.groupId = pending.groupId || 'auto';
    this.phase = 'draft';
    this.prepared = null;
  }

  onPromptChange(): void {
    if (this.phase === 'prepared') {
      this.phase = 'draft';
      this.prepared = null;
    }
  }

  useSuggestion(chip: string): void {
    this.prompt = SUGGESTION_EXPANSIONS[chip] || chip;
    this.phase = 'draft';
    this.prepared = null;
  }

  prepareRequest(): void {
    const ok = this.buildPreparedRequest();
    if (!ok) return;
    this.toast.success('MCP request ready — copy for Cursor or Claude to execute');
  }

  editRequest(): void {
    this.phase = 'draft';
  }

  async copyForClient(client: 'cursor' | 'claude'): Promise<void> {
    if (this.phase !== 'prepared' || !this.prepared) {
      if (!this.buildPreparedRequest()) return;
    }
    if (!this.prepared) return;

    const body = this.buildClientPayload(client, this.prepared);

    try {
      await navigator.clipboard.writeText(body);
      const clientLabel = client === 'cursor' ? 'Cursor' : 'Claude';
      this.mcp.pushActivity(
        `Copied for ${clientLabel}`,
        `${this.prepared.group.name} · ${this.prepared.prompt.slice(0, 72)}`
      );
      this.toast.success(`Copied for ${clientLabel}`);
      this.copied.emit(body);
    } catch {
      this.toast.error('Could not copy to clipboard');
    }
  }

  /** Builds the prepared plan. Returns false if validation failed. */
  private buildPreparedRequest(): boolean {
    const text = this.prompt.trim();
    if (!text) {
      this.toast.error('Enter what you want to investigate');
      return false;
    }

    const autoDetected = this.groupId === 'auto';
    const group = autoDetected ? this.detectGroup(text) : this.groups.find(g => g.id === this.groupId);

    if (!group) {
      this.toast.error('No matching MCP tool group found');
      return false;
    }

    const tools = this.suggestTools(group, text);
    const operations = this.buildOperations(group, tools, text);
    const intent = this.inferIntent(group, text);

    this.prepared = {
      intent,
      group,
      autoDetected,
      tools,
      operations,
      prompt: text
    };
    this.phase = 'prepared';

    if (autoDetected) {
      this.groupId = group.id;
    }

    this.mcp.pushActivity('Request prepared', `${group.name} · ${text.slice(0, 72)}`);
    return true;
  }

  private detectGroup(text: string): McpGroup | undefined {
    const lower = text.toLowerCase();
    let best: { id: string; score: number } | null = null;

    for (const entry of GROUP_KEYWORDS) {
      let score = 0;
      for (const word of entry.words) {
        if (lower.includes(word)) score += word.length > 6 ? 2 : 1;
      }
      if (score > 0 && (!best || score > best.score)) {
        best = { id: entry.id, score };
      }
    }

    const id = best?.id || 'architecture';
    return this.groups.find(g => g.id === id) || this.groups[0];
  }

  private suggestTools(group: McpGroup, text: string): string[] {
    const pool = [...(group.tools || []), ...(group.plannedTools || [])];
    if (!pool.length) return [];

    const lower = text.toLowerCase();
    const scored = pool.map(tool => {
      const parts = tool.toLowerCase().split('_');
      let score = 0;
      for (const part of parts) {
        if (part.length > 2 && lower.includes(part)) score += 2;
      }
      if (lower.includes('module') && tool.includes('module')) score += 3;
      if (lower.includes('depend') && tool.includes('depend')) score += 3;
      if (lower.includes('architect') && tool.includes('architect')) score += 3;
      if (lower.includes('review') && tool.includes('review')) score += 3;
      if (lower.includes('change') && tool.includes('change')) score += 2;
      if (lower.includes('search') && tool.includes('search')) score += 2;
      if (lower.includes('maintenance') && tool.includes('maintenance')) score += 3;
      if (lower.includes('test') && tool.includes('test')) score += 2;
      if (lower.includes('build') && (tool.includes('build') || tool.includes('git'))) score += 2;
      return { tool, score };
    });

    scored.sort((a, b) => b.score - a.score);
    const picked = scored.filter(s => s.score > 0).slice(0, 4).map(s => s.tool);
    if (picked.length >= 2) return picked;
    return pool.slice(0, Math.min(3, pool.length));
  }

  private buildOperations(group: McpGroup, tools: string[], text: string): string[] {
    if (tools.length) {
      return tools.map(t => this.humanizeTool(t));
    }
    return [
      `Use ${group.name} tools`,
      `Investigate: ${text.slice(0, 80)}${text.length > 80 ? '…' : ''}`,
      'Return findings in Cursor / Claude'
    ];
  }

  private humanizeTool(tool: string): string {
    return tool
      .split('_')
      .map(w => w.charAt(0).toUpperCase() + w.slice(1))
      .join(' ');
  }

  private inferIntent(group: McpGroup, text: string): string {
    const lower = text.toLowerCase();
    if (lower.includes('architect') || lower.includes('module')) return 'Architecture Analysis';
    if (lower.includes('maintenance') || lower.includes('vehicle')) return 'Fleet Investigation';
    if (lower.includes('test')) return 'Testing Investigation';
    if (lower.includes('build') || lower.includes('fail')) return 'Build Investigation';
    if (lower.includes('database') || lower.includes('table') || lower.includes('sql')) {
      return 'Database Analysis';
    }
    if (lower.includes('search') || lower.includes('find code')) return 'Code Search';
    return `${group.name.replace(/ MCP$/, '')} Investigation`;
  }

  private buildClientPayload(client: 'cursor' | 'claude', req: McpPreparedRequest): string {
    const clientName = client === 'cursor' ? 'Cursor' : 'Claude';
    const toolLines = req.tools.length
      ? req.tools.map((t, i) => `${i + 1}. ${t}`).join('\n')
      : '(Use available tools in this MCP group)';

    return [
      `Use SheikhGo MCP (${req.group.name}) via ${clientName}.`,
      '',
      'Request:',
      req.prompt,
      '',
      'Preferred tools:',
      toolLines,
      '',
      'Return:',
      '- Clear findings',
      '- Relevant files / modules / tables',
      '- Dependencies and risks',
      '- Recommended next steps'
    ].join('\n');
  }
}
