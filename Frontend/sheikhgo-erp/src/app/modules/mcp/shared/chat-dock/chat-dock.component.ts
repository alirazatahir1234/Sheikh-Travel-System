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

  prompt = '';
  groupId = 'architecture';
  groups: McpGroup[] = [];
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
      if (!this.groups.find(g => g.id === this.groupId) && this.groups.length) {
        this.groupId = this.groups[0].id;
      }
    }
    this.applyPending();
  }

  private applyPending(): void {
    if (this.pendingApplied) return;
    const pending = this.mcp.consumePendingPrompt();
    if (!pending) return;
    this.pendingApplied = true;
    this.prompt = pending.prompt;
    if (pending.groupId) this.groupId = pending.groupId;
  }

  useSuggestion(text: string): void {
    this.prompt = text;
  }

  async copyForClient(client: 'cursor' | 'claude'): Promise<void> {
    const group = this.groups.find(g => g.id === this.groupId);
    const body = [
      `[SheikhGo MCP · ${client}]`,
      `Tool group: ${group?.name || this.groupId}`,
      '',
      this.prompt.trim() || '(empty prompt)'
    ].join('\n');

    try {
      await navigator.clipboard.writeText(body);
      this.mcp.pushActivity(`Copied prompt for ${client}`, this.prompt.slice(0, 80));
      this.toast.success(`Prompt copied for ${client === 'cursor' ? 'Cursor' : 'Claude'}`);
      this.copied.emit(body);
    } catch {
      this.toast.error('Could not copy to clipboard');
    }
  }
}
