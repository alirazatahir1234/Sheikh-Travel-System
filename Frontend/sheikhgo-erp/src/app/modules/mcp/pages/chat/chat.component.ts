import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { McpCatalog, McpCatalogService } from '../../shared/mcp-catalog.service';
import { McpChatDockComponent } from '../../shared/chat-dock/chat-dock.component';

@Component({
  selector: 'app-mcp-chat',
  standalone: true,
  imports: [CommonModule, McpChatDockComponent],
  template: `
    <div class="page" *ngIf="catalog as c">
      <header>
        <h2>MCP Request Builder</h2>
        <p>
          Write a natural-language investigation, prepare an MCP request plan, then copy it into
          Cursor or Claude. SheikhGo-MCP runs over stdio outside the browser — there is no in-app LLM
          here.
        </p>
      </header>
      <app-mcp-chat-dock [catalog]="c"></app-mcp-chat-dock>
    </div>
  `,
  styles: [
    `
      header h2 {
        margin: 0;
        font-size: 18px;
        font-weight: 700;
        color: var(--stb-text, #0f172a);
      }
      header p {
        margin: 4px 0 16px;
        color: var(--stb-text-muted, #64748b);
        font-size: 13px;
        max-width: 44rem;
        line-height: 1.5;
      }
    `
  ]
})
export class McpChatComponent implements OnInit {
  catalog: McpCatalog | null = null;

  constructor(private mcp: McpCatalogService) {}

  ngOnInit(): void {
    this.mcp.load().subscribe(c => (this.catalog = c));
  }
}
