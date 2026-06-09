import {
  Component,
  ChangeDetectionStrategy,
  signal,
  computed,
  input,
  output,
  ViewChild,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { OrganizationNode, Department } from '../../models/organization.models';
import { TreeNodeComponent } from '../tree-node/tree-node.component';
import { UnassignedDepartmentComponent } from '../unassigned-department/unassigned-department.component';
import { ContextMenuComponent } from '../context-menu/context-menu.component';
import { HierarchyMinimapComponent } from '../hierarchy-minimap/hierarchy-minimap.component';

@Component({
  selector: 'app-organization-tree',
  standalone: true,
  imports: [CommonModule, FormsModule, TreeNodeComponent, UnassignedDepartmentComponent, ContextMenuComponent, HierarchyMinimapComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="bg-white rounded-lg shadow border border-border overflow-hidden h-full flex flex-col">
      <!-- Header -->
      <div class="px-4 py-3 border-b border-border">
        <div class="flex items-center justify-between mb-3">
          <h3 class="text-sm font-semibold text-text">Organization Tree</h3>
          <div class="flex items-center gap-1">
            <button
              type="button"
              (click)="expandAll()"
              class="flex items-center gap-1 px-2 py-1 text-xs font-medium text-text-muted hover:text-text hover:bg-surface-alt rounded transition-colors"
            >
              <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7" />
              </svg>
              EXPAND
            </button>
            <button
              type="button"
              (click)="collapseAll()"
              class="flex items-center gap-1 px-2 py-1 text-xs font-medium text-text-muted hover:text-text hover:bg-surface-alt rounded transition-colors"
            >
              <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
              </svg>
              COLLAPSE
            </button>
          </div>
        </div>

        <!-- Action Toolbar -->
        <div class="flex flex-wrap items-center gap-2 mb-3">
          <button
            type="button"
            (click)="addBranch.emit()"
            class="flex items-center gap-1.5 px-2.5 py-1.5 text-xs font-medium text-white bg-primary-600 rounded-lg hover:bg-primary-700 transition-colors"
          >
            <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
            </svg>
            Branch
          </button>
          <button
            type="button"
            (click)="addDepartment.emit()"
            class="flex items-center gap-1.5 px-2.5 py-1.5 text-xs font-medium text-primary-700 bg-primary-50 border border-primary-200 rounded-lg hover:bg-primary-100 transition-colors"
          >
            <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
            </svg>
            Department
          </button>
          <button
            type="button"
            (click)="addTeam.emit()"
            class="flex items-center gap-1.5 px-2.5 py-1.5 text-xs font-medium text-text-muted bg-surface-alt border border-border rounded-lg hover:bg-gray-100 transition-colors"
          >
            <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
            </svg>
            Team
          </button>
          <div class="flex-1"></div>
          <button
            type="button"
            (click)="importHierarchy.emit()"
            class="flex items-center gap-1.5 px-2.5 py-1.5 text-xs font-medium text-text-muted hover:text-text hover:bg-surface-alt rounded-lg transition-colors"
            title="Import hierarchy from file"
          >
            <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-8l-4-4m0 0L8 8m4-4v12" />
            </svg>
            Import
          </button>
          <button
            type="button"
            (click)="exportHierarchy.emit()"
            class="flex items-center gap-1.5 px-2.5 py-1.5 text-xs font-medium text-text-muted hover:text-text hover:bg-surface-alt rounded-lg transition-colors"
            title="Export hierarchy to JSON"
          >
            <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4" />
            </svg>
            Export
          </button>
        </div>

        <!-- Search -->
        <div class="relative">
          <svg
            class="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-text-soft"
            fill="none"
            stroke="currentColor"
            viewBox="0 0 24 24"
          >
            <path
              stroke-linecap="round"
              stroke-linejoin="round"
              stroke-width="2"
              d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z"
            />
          </svg>
          <input
            type="text"
            [ngModel]="searchQuery()"
            (ngModelChange)="searchQuery.set($event)"
            (focus)="searchFocused.set(true)"
            (blur)="onSearchBlur()"
            placeholder="Search branch, dept, or user..."
            class="w-full pl-9 pr-3 py-2 text-sm border border-border rounded-lg bg-surface-alt placeholder:text-text-soft focus:outline-none focus:ring-2 focus:ring-primary-600/20 focus:border-primary-600 transition-all"
          />

          <!-- Smart Search Results Dropdown -->
          @if (searchQuery().trim() && searchFocused() && searchResults().length > 0) {
            <div class="absolute z-50 top-full left-0 right-0 mt-1 bg-white border border-border rounded-lg shadow-lg max-h-64 overflow-y-auto">
              @for (result of searchResults(); track result.node.id) {
                <button
                  type="button"
                  (mousedown)="onSearchResultClick(result)"
                  class="w-full flex items-start gap-2 px-3 py-2 text-left hover:bg-surface-alt transition-colors"
                >
                  <div class="flex-shrink-0 mt-0.5">
                    @switch (result.node.type) {
                      @case ('branch') {
                        <svg class="w-4 h-4 text-primary-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4" />
                        </svg>
                      }
                      @case ('department') {
                        <svg class="w-4 h-4 text-gray-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0zm6 3a2 2 0 11-4 0 2 2 0 014 0zM7 10a2 2 0 11-4 0 2 2 0 014 0z" />
                        </svg>
                      }
                      @default {
                        <svg class="w-4 h-4 text-text-muted" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4" />
                        </svg>
                      }
                    }
                  </div>
                  <div class="min-w-0 flex-1">
                    <div class="text-sm font-medium text-text" [innerHTML]="highlightMatch(result.node.name)"></div>
                    <div class="text-xs text-text-muted">{{ result.path }}</div>
                  </div>
                </button>
              }
            </div>
          }
        </div>
      </div>

      <!-- Bulk Action Bar -->
      @if (selectedNodeIds().size > 0) {
        <div class="px-3 py-2 bg-primary-50 border-y border-primary-100 flex items-center justify-between">
          <div class="flex items-center gap-2 text-sm text-primary-700">
            <span class="font-medium">{{ selectedNodeIds().size }} selected</span>
            <button
              type="button"
              (click)="clearSelection()"
              class="text-primary-600 hover:text-primary-800 underline text-xs"
            >
              Clear
            </button>
          </div>
          <div class="flex items-center gap-2">
            <button
              type="button"
              (click)="bulkMove.emit(getSelectedNodes())"
              class="px-2 py-1 text-xs font-medium text-primary-700 hover:bg-primary-100 rounded transition-colors"
            >
              Move
            </button>
            <button
              type="button"
              (click)="bulkArchive.emit(getSelectedNodes())"
              class="px-2 py-1 text-xs font-medium text-primary-700 hover:bg-primary-100 rounded transition-colors"
            >
              Archive
            </button>
            <button
              type="button"
              (click)="bulkExport.emit(getSelectedNodes())"
              class="px-2 py-1 text-xs font-medium text-primary-700 hover:bg-primary-100 rounded transition-colors"
            >
              Export
            </button>
            <button
              type="button"
              (click)="bulkAssignManager.emit(getSelectedNodes())"
              class="px-2 py-1 text-xs font-medium text-primary-700 hover:bg-primary-100 rounded transition-colors"
            >
              Assign Manager
            </button>
          </div>
        </div>
      }

      <!-- Tree Content -->
      <div class="flex-1 overflow-y-auto p-2">
        @for (node of filteredNodes(); track node.id) {
          <app-tree-node
            [node]="node"
            [level]="0"
            [selectedNodeId]="selectedNodeId()"
            [expandedNodeIds]="expandedNodeIds()"
            [multiSelectEnabled]="multiSelectMode()"
            [checkedNodeIds]="selectedNodeIds()"
            (nodeSelect)="onNodeSelect($event)"
            (nodeToggle)="onNodeToggle($event)"
            (nodeCheck)="onNodeCheck($event)"
            (nodeContextMenu)="onNodeContextMenu($event)"
          />
        }
      </div>

      <!-- Mini-map -->
      @if (nodes().length > 0) {
        <div class="p-2 border-t border-border bg-surface-alt">
          <app-hierarchy-minimap
            [nodes]="nodes()"
            [selectedNodeId]="selectedNodeId()"
            (branchSelected)="onMinimapBranchSelected($event)"
          />
        </div>
      }

      <!-- Context Menu -->
      <app-context-menu
        #contextMenu
        (itemSelected)="onContextMenuAction($event)"
      />

      <!-- Unassigned Departments -->
      @if (unassignedDepartments().length > 0) {
        <div class="border-t border-border bg-surface-alt">
          <app-unassigned-department
            [departments]="unassignedDepartments()"
            (departmentSelect)="onDepartmentSelect($event)"
          />
        </div>
      }
    </div>
  `,
})
export class OrganizationTreeComponent {
  @ViewChild('contextMenu') contextMenu!: ContextMenuComponent;

  readonly nodes = input.required<OrganizationNode[]>();
  readonly unassignedDepartments = input<Department[]>([]);
  readonly selectedNodeId = input<string | null>(null);

  readonly nodeSelected = output<OrganizationNode>();
  readonly departmentSelected = output<Department>();
  readonly addBranch = output<void>();
  readonly addDepartment = output<void>();
  readonly addTeam = output<void>();
  readonly importHierarchy = output<void>();
  readonly exportHierarchy = output<void>();
  readonly bulkMove = output<OrganizationNode[]>();
  readonly bulkArchive = output<OrganizationNode[]>();
  readonly bulkExport = output<OrganizationNode[]>();
  readonly bulkAssignManager = output<OrganizationNode[]>();
  readonly contextMenuAction = output<{ action: string; node: OrganizationNode }>();

  readonly searchQuery = signal<string>('');
  readonly expandedNodeIds = signal<Set<string>>(new Set(['tenant-1', 'branch-1']));
  readonly selectedNodeIds = signal<Set<string>>(new Set());
  readonly multiSelectMode = signal<boolean>(true);
  readonly searchFocused = signal<boolean>(false);

  readonly filteredNodes = computed(() => {
    const query = this.searchQuery().toLowerCase().trim();
    if (!query) return this.nodes();

    return this.filterNodes(this.nodes(), query);
  });

  readonly searchResults = computed(() => {
    const query = this.searchQuery().toLowerCase().trim();
    if (!query) return [];

    const results: Array<{ node: OrganizationNode; path: string }> = [];
    this.collectSearchResults(this.nodes(), query, [], results);
    return results.slice(0, 10);
  });

  private filterNodes(nodes: OrganizationNode[], query: string): OrganizationNode[] {
    return nodes
      .map(node => {
        const matchesQuery =
          node.name.toLowerCase().includes(query) ||
          (node.code?.toLowerCase().includes(query) ?? false);

        const filteredChildren = this.filterNodes(node.children, query);
        const hasMatchingChildren = filteredChildren.length > 0;

        if (matchesQuery || hasMatchingChildren) {
          return {
            ...node,
            children: hasMatchingChildren ? filteredChildren : node.children,
            isExpanded: hasMatchingChildren || matchesQuery,
          };
        }
        return null;
      })
      .filter((node): node is OrganizationNode => node !== null);
  }

  onNodeSelect(node: OrganizationNode): void {
    this.nodeSelected.emit(node);
  }

  onNodeToggle(nodeId: string): void {
    const expanded = new Set(this.expandedNodeIds());
    if (expanded.has(nodeId)) {
      expanded.delete(nodeId);
    } else {
      expanded.add(nodeId);
    }
    this.expandedNodeIds.set(expanded);
  }

  onDepartmentSelect(department: Department): void {
    this.departmentSelected.emit(department);
  }

  onNodeContextMenu(event: { node: OrganizationNode; x: number; y: number }): void {
    this.contextMenu?.show(event.node, event.x, event.y);
  }

  onContextMenuAction(event: { action: string; node: OrganizationNode }): void {
    this.contextMenuAction.emit(event);
  }

  onMinimapBranchSelected(nodeId: string): void {
    const node = this.findNodeById(this.nodes(), nodeId);
    if (node) {
      this.nodeSelected.emit(node);
      this.expandToNode(nodeId);
    }
  }

  private findNodeById(nodes: OrganizationNode[], nodeId: string): OrganizationNode | null {
    for (const node of nodes) {
      if (node.id === nodeId) return node;
      if (node.children.length > 0) {
        const found = this.findNodeById(node.children, nodeId);
        if (found) return found;
      }
    }
    return null;
  }

  expandAll(): void {
    const allIds = this.collectAllNodeIds(this.nodes());
    this.expandedNodeIds.set(new Set(allIds));
  }

  collapseAll(): void {
    this.expandedNodeIds.set(new Set());
  }

  clearSelection(): void {
    this.selectedNodeIds.set(new Set());
  }

  onSearchBlur(): void {
    setTimeout(() => this.searchFocused.set(false), 200);
  }

  onSearchResultClick(result: { node: OrganizationNode; path: string }): void {
    this.searchQuery.set('');
    this.searchFocused.set(false);
    this.nodeSelected.emit(result.node);
    this.expandToNode(result.node.id);
  }

  highlightMatch(text: string): string {
    const query = this.searchQuery().toLowerCase().trim();
    if (!query) return text;

    const regex = new RegExp(`(${this.escapeRegex(query)})`, 'gi');
    return text.replace(regex, '<mark class="bg-yellow-200 rounded px-0.5">$1</mark>');
  }

  private escapeRegex(str: string): string {
    return str.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  }

  private collectSearchResults(
    nodes: OrganizationNode[],
    query: string,
    pathParts: string[],
    results: Array<{ node: OrganizationNode; path: string }>
  ): void {
    for (const node of nodes) {
      const currentPath = [...pathParts, node.name];
      const matchesQuery =
        node.name.toLowerCase().includes(query) ||
        (node.code?.toLowerCase().includes(query) ?? false);

      if (matchesQuery && node.type !== 'tenant') {
        results.push({
          node,
          path: pathParts.join(' > ') || 'Root',
        });
      }

      if (node.children.length > 0) {
        this.collectSearchResults(node.children, query, currentPath, results);
      }
    }
  }

  private expandToNode(nodeId: string): void {
    const expanded = new Set(this.expandedNodeIds());
    const parentIds = this.getParentIds(this.nodes(), nodeId, []);
    for (const id of parentIds) {
      expanded.add(id);
    }
    this.expandedNodeIds.set(expanded);
  }

  private getParentIds(nodes: OrganizationNode[], targetId: string, path: string[]): string[] {
    for (const node of nodes) {
      if (node.id === targetId) {
        return path;
      }
      if (node.children.length > 0) {
        const result = this.getParentIds(node.children, targetId, [...path, node.id]);
        if (result.length > 0 || (node.children.some(c => c.id === targetId))) {
          return result.length > 0 ? result : [...path, node.id];
        }
      }
    }
    return [];
  }

  onNodeCheck(event: { nodeId: string; checked: boolean }): void {
    const selected = new Set(this.selectedNodeIds());
    if (event.checked) {
      selected.add(event.nodeId);
    } else {
      selected.delete(event.nodeId);
    }
    this.selectedNodeIds.set(selected);
  }

  getSelectedNodes(): OrganizationNode[] {
    const selectedIds = this.selectedNodeIds();
    return this.collectNodesByIds(this.nodes(), selectedIds);
  }

  private collectNodesByIds(nodes: OrganizationNode[], ids: Set<string>): OrganizationNode[] {
    const result: OrganizationNode[] = [];
    for (const node of nodes) {
      if (ids.has(node.id)) {
        result.push(node);
      }
      if (node.children.length > 0) {
        result.push(...this.collectNodesByIds(node.children, ids));
      }
    }
    return result;
  }

  private collectAllNodeIds(nodes: OrganizationNode[]): string[] {
    const ids: string[] = [];
    for (const node of nodes) {
      ids.push(node.id);
      if (node.children.length > 0) {
        ids.push(...this.collectAllNodeIds(node.children));
      }
    }
    return ids;
  }
}
