import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatIconModule } from '@angular/material/icon';
import { McpCatalog, McpCatalogService, McpGroup } from '../../shared/mcp-catalog.service';
import { McpStatusPillComponent } from '../../shared/status-pill/status-pill.component';

@Component({
  selector: 'app-mcp-tools',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatFormFieldModule,
    MatSelectModule,
    MatIconModule,
    McpStatusPillComponent
  ],
  templateUrl: './tools.component.html',
  styleUrls: ['./tools.component.scss']
})
export class McpToolsComponent implements OnInit {
  catalog: McpCatalog | null = null;
  groupId = '';

  constructor(
    private mcp: McpCatalogService,
    private route: ActivatedRoute
  ) {}

  ngOnInit(): void {
    this.mcp.load().subscribe(c => {
      this.catalog = c;
      const q = this.route.snapshot.queryParamMap.get('group');
      this.groupId = q || c.groups[0]?.id || '';
    });
    this.route.queryParamMap.subscribe(p => {
      const g = p.get('group');
      if (g) this.groupId = g;
    });
  }

  selected(): McpGroup | undefined {
    return this.catalog?.groups.find(g => g.id === this.groupId);
  }

  toolsFor(group: McpGroup | undefined): string[] {
    if (!group) return [];
    if (group.tools?.length) return group.tools;
    return group.plannedTools || [];
  }
}
