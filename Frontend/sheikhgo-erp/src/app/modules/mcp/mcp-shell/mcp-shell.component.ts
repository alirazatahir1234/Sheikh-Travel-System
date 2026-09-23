import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { McpCatalogService, McpCatalog } from '../shared/mcp-catalog.service';
import { McpStatusPillComponent } from '../shared/status-pill/status-pill.component';

interface McpNavItem {
  label: string;
  route: string;
  icon: string;
  exact?: boolean;
}

@Component({
  selector: 'app-mcp-shell',
  standalone: true,
  imports: [
    CommonModule,
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatIconModule,
    MatButtonModule,
    McpStatusPillComponent
  ],
  templateUrl: './mcp-shell.component.html',
  styleUrls: ['./mcp-shell.component.scss']
})
export class McpShellComponent implements OnInit {
  catalog: McpCatalog | null = null;
  nav: McpNavItem[] = [
    { label: 'Dashboard', route: '/mcp', icon: 'dashboard', exact: true },
    { label: 'Servers', route: '/mcp/servers', icon: 'dns' },
    { label: 'Tools', route: '/mcp/tools', icon: 'construction' },
    { label: 'Chat', route: '/mcp/chat', icon: 'edit_note' },
    { label: 'Connections', route: '/mcp/connections', icon: 'hub' },
    { label: 'Settings', route: '/mcp/settings', icon: 'settings' }
  ];

  constructor(readonly mcp: McpCatalogService) {}

  ngOnInit(): void {
    this.mcp.load().subscribe(c => (this.catalog = c));
  }

  clearActivity(): void {
    this.mcp.clearActivity();
  }

  registeredTools(): number {
    return this.mcp.registeredToolCount(this.catalog);
  }

  /** Catalog-derived readiness — not live process health or LLM execution. */
  systemStatus(): { status: string; label: string; message: string } {
    const c = this.catalog;
    if (!c) {
      return {
        status: 'planned',
        label: 'Loading',
        message: 'Loading MCP catalog…'
      };
    }
    if (c.summary.runningGroups > 0) {
      return {
        status: 'running',
        label: 'Catalog Ready',
        message: 'This console prepares MCP requests. Execution happens in Cursor or Claude over stdio.'
      };
    }
    if (c.summary.stoppedGroups > 0) {
      return {
        status: 'stopped',
        label: 'Attention',
        message: 'Some tool groups are stopped in the catalog.'
      };
    }
    return {
      status: 'planned',
      label: 'Catalog Loaded',
      message: 'No running tool groups in the current catalog.'
    };
  }

  connectionStatusLabel(status: string): string {
    const s = (status || '').toLowerCase();
    if (s === 'connected') return 'Connected';
    if (s === 'disconnected') return 'Disconnected';
    return 'Not Verified';
  }

  connectionStatusClass(status: string): string {
    const s = (status || '').toLowerCase();
    if (s === 'connected') return 'running';
    if (s === 'disconnected') return 'stopped';
    return 'unknown';
  }

  connectionHint(status: string): string {
    const s = (status || '').toLowerCase();
    if (s === 'connected') return 'Verified connection reported.';
    if (s === 'disconnected') return 'Client reported disconnected.';
    return 'Local stdio connection cannot be verified from the browser.';
  }
}
