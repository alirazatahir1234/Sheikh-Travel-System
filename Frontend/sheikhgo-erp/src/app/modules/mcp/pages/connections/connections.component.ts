import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { McpCatalog, McpCatalogService } from '../../shared/mcp-catalog.service';
import { UiToastService } from '../../../../shared/components/ui/toast/ui-toast.service';

@Component({
  selector: 'app-mcp-connections',
  standalone: true,
  imports: [CommonModule, MatIconModule, MatButtonModule],
  templateUrl: './connections.component.html',
  styleUrls: ['./connections.component.scss']
})
export class McpConnectionsComponent implements OnInit {
  catalog: McpCatalog | null = null;

  cursorSnippet = `{
  "mcpServers": {
    "sheikhgo": {
      "command": "npx",
      "args": ["tsx", "/absolute/path/to/SheikhGo-MCP/src/index.ts"],
      "env": {
        "SHEIKHGO_REPOSITORY": "/absolute/path/to/Sheikh-Travel-System"
      }
    }
  }
}`;

  claudeSnippet = `{
  "mcpServers": {
    "sheikhgo": {
      "command": "npx",
      "args": ["tsx", "/absolute/path/to/SheikhGo-MCP/src/index.ts"],
      "env": {
        "SHEIKHGO_REPOSITORY": "/absolute/path/to/Sheikh-Travel-System"
      }
    }
  }
}`;

  constructor(
    private mcp: McpCatalogService,
    private toast: UiToastService
  ) {}

  ngOnInit(): void {
    this.mcp.load().subscribe(c => (this.catalog = c));
  }

  async copy(text: string, label: string): Promise<void> {
    try {
      await navigator.clipboard.writeText(text);
      this.mcp.pushActivity('Copied connection config', label);
      this.toast.success(`${label} config copied`);
    } catch {
      this.toast.error('Could not copy');
    }
  }
}
