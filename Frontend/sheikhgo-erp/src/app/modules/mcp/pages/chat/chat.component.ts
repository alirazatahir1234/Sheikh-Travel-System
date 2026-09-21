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
        <h2>Chat dock</h2>
        <p>
          Prepare prompts for Cursor or Claude Desktop. SheikhGo-MCP runs over stdio outside the browser —
          there is no fake in-app LLM here.
        </p>
      </header>
      <app-mcp-chat-dock [catalog]="c"></app-mcp-chat-dock>
    </div>
  `,
  styles: [
    `
      header h2 {
        margin: 0;
        font-size: 1.15rem;
        color: #0f172a;
      }
      header p {
        margin: 0.35rem 0 0.9rem;
        color: #64748b;
        font-size: 0.85rem;
        max-width: 40rem;
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
