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
    { label: 'Chat', route: '/mcp/chat', icon: 'chat' },
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
}
