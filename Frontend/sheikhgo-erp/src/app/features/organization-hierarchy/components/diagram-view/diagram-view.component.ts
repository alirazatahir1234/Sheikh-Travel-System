import {
  Component,
  ChangeDetectionStrategy,
  input,
  output,
  computed,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { OrganizationNode } from '../../models/organization.models';

@Component({
  selector: 'app-diagram-view',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: [`
    .org-chart {
      display: flex;
      flex-direction: column;
      align-items: center;
      min-width: fit-content;
    }
    .org-level {
      display: flex;
      justify-content: center;
      position: relative;
    }
    .org-node {
      position: relative;
      margin: 0 20px;
    }
    .org-node::before {
      content: '';
      position: absolute;
      top: -20px;
      left: 50%;
      width: 2px;
      height: 20px;
      background: #D8E0EA;
    }
    .org-node:first-child:last-child::before,
    .root-node::before {
      display: none;
    }
    .org-children {
      display: flex;
      justify-content: center;
      padding-top: 20px;
      position: relative;
    }
    .org-children::before {
      content: '';
      position: absolute;
      top: 0;
      left: 50%;
      width: 2px;
      height: 20px;
      background: #D8E0EA;
    }
    .org-children::after {
      content: '';
      position: absolute;
      top: 20px;
      height: 2px;
      background: #D8E0EA;
    }
    .org-children.has-multiple::after {
      left: 25%;
      right: 25%;
    }
    .org-children:not(.has-multiple)::after {
      display: none;
    }
  `],
  template: `
    <div class="bg-white rounded-lg border border-border p-6 overflow-auto">
      <!-- Header -->
      <div class="flex items-center justify-between mb-6">
        <div>
          <h3 class="text-lg font-semibold text-text">Organization Diagram</h3>
          <p class="text-sm text-text-muted">Visual hierarchy overview</p>
        </div>
        <div class="flex items-center gap-2">
          <button
            type="button"
            class="p-2 text-text-muted hover:text-text rounded-lg hover:bg-surface-alt transition-colors"
            title="Zoom In"
          >
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0zM10 7v3m0 0v3m0-3h3m-3 0H7" />
            </svg>
          </button>
          <button
            type="button"
            class="p-2 text-text-muted hover:text-text rounded-lg hover:bg-surface-alt transition-colors"
            title="Zoom Out"
          >
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0zM13 10H7" />
            </svg>
          </button>
          <button
            type="button"
            class="p-2 text-text-muted hover:text-text rounded-lg hover:bg-surface-alt transition-colors"
            title="Reset View"
          >
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
            </svg>
          </button>
        </div>
      </div>

      <!-- Org Chart -->
      <div class="min-h-[400px] flex items-start justify-center pt-8">
        @if (nodes().length > 0) {
          <div class="org-chart">
            @for (node of nodes(); track node.id) {
              <ng-container *ngTemplateOutlet="nodeTemplate; context: { $implicit: node, isRoot: true }" />
            }
          </div>
        } @else {
          <div class="text-center py-12">
            <svg class="w-16 h-16 mx-auto mb-4 text-text-soft" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M7 21a4 4 0 01-4-4V5a2 2 0 012-2h4a2 2 0 012 2v12a4 4 0 01-4 4zm0 0h12a2 2 0 002-2v-4a2 2 0 00-2-2h-2.343M11 7.343l1.657-1.657a2 2 0 012.828 0l2.829 2.829a2 2 0 010 2.828l-8.486 8.485M7 17h.01" />
            </svg>
            <p class="text-text-muted">No organization data to display</p>
          </div>
        }
      </div>
    </div>

    <!-- Node Template -->
    <ng-template #nodeTemplate let-node let-isRoot="isRoot">
      <div class="org-node" [class.root-node]="isRoot">
        <!-- Node Card -->
        <div
          class="min-w-[180px] p-3 rounded-lg border-2 cursor-pointer transition-all"
          [class]="getNodeClasses(node)"
          (click)="nodeSelected.emit(node)"
        >
          <div class="flex items-center gap-2">
            <div [class]="getIconClasses(node)">
              @switch (node.type) {
                @case ('tenant') {
                  <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4" />
                  </svg>
                }
                @case ('branch') {
                  <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4" />
                  </svg>
                }
                @case ('department') {
                  <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0zm6 3a2 2 0 11-4 0 2 2 0 014 0zM7 10a2 2 0 11-4 0 2 2 0 014 0z" />
                  </svg>
                }
              }
            </div>
            <div class="flex-1 min-w-0">
              <h4 class="text-sm font-semibold text-text truncate">{{ node.name }}</h4>
              @if (node.stats && node.type === 'branch') {
                <div class="text-xs text-text-muted">
                  {{ node.stats.departmentCount ?? 0 }} depts · {{ node.stats.userCount ?? 0 }} users
                </div>
              }
              @if (node.stats && node.type === 'department') {
                <div class="text-xs text-text-muted">
                  {{ node.stats.userCount ?? 0 }} users
                </div>
              }
            </div>
          </div>
        </div>

        <!-- Children -->
        @if (getBranchChildren(node).length > 0) {
          <div class="org-children" [class.has-multiple]="getBranchChildren(node).length > 1">
            @for (child of getBranchChildren(node); track child.id) {
              <ng-container *ngTemplateOutlet="nodeTemplate; context: { $implicit: child, isRoot: false }" />
            }
          </div>
        }
      </div>
    </ng-template>
  `,
})
export class DiagramViewComponent {
  readonly nodes = input<OrganizationNode[]>([]);
  readonly selectedNodeId = input<string | null>(null);

  readonly nodeSelected = output<OrganizationNode>();

  getBranchChildren(node: OrganizationNode): OrganizationNode[] {
    return node.children.filter(c => c.type === 'branch');
  }

  getNodeClasses(node: OrganizationNode): string {
    const isSelected = this.selectedNodeId() === node.id;
    const base = 'bg-white hover:shadow-md';

    if (isSelected) {
      return `${base} border-primary-500 shadow-md`;
    }

    switch (node.type) {
      case 'tenant':
        return `${base} border-primary-200 hover:border-primary-300`;
      case 'branch':
        return `${base} border-gray-200 hover:border-primary-200`;
      case 'department':
        return `${base} border-gray-100 hover:border-gray-200`;
      default:
        return `${base} border-gray-200`;
    }
  }

  getIconClasses(node: OrganizationNode): string {
    const base = 'w-8 h-8 rounded-lg flex items-center justify-center flex-shrink-0';

    switch (node.type) {
      case 'tenant':
        return `${base} bg-primary-100 text-primary-700`;
      case 'branch':
        return `${base} bg-primary-50 text-primary-600`;
      case 'department':
        return `${base} bg-gray-100 text-gray-600`;
      default:
        return `${base} bg-gray-100 text-gray-600`;
    }
  }
}
