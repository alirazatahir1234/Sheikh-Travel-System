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
  selector: 'app-tree-node',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: [`
    .tree-node-wrapper {
      position: relative;
    }
    .tree-line-vertical {
      position: absolute;
      left: 20px;
      top: 0;
      bottom: 0;
      width: 1px;
      background-color: #D8E0EA;
    }
    .tree-line-horizontal {
      position: absolute;
      left: 20px;
      top: 50%;
      width: 12px;
      height: 1px;
      background-color: #D8E0EA;
    }
    .children-container {
      position: relative;
      margin-left: 20px;
      padding-left: 12px;
      border-left: 1px solid #D8E0EA;
    }
    .children-container::before {
      content: '';
      position: absolute;
      left: 0;
      top: 0;
      width: 12px;
      height: 20px;
      border-bottom: 1px solid #D8E0EA;
      border-left: 1px solid transparent;
    }
  `],
  template: `
    <div class="tree-node-wrapper">
      <!-- Node Row -->
      <div
        [class]="nodeClasses()"
        [style.padding-left.px]="level() * 20 + 8"
        (click)="onSelect()"
        (contextmenu)="onContextMenu($event)"
      >
        <!-- Tree connector line -->
        @if (level() > 0) {
          <div class="tree-line-horizontal" [style.left.px]="(level() - 1) * 20 + 8"></div>
        }

        <!-- Multi-select Checkbox -->
        @if (multiSelectEnabled() && node().type !== 'tenant') {
          <button
            type="button"
            (click)="onCheckChange($event)"
            class="flex-shrink-0 w-4 h-4 rounded border flex items-center justify-center transition-colors"
            [class.border-primary-600]="isChecked()"
            [class.bg-primary-600]="isChecked()"
            [class.border-border]="!isChecked()"
            [class.bg-white]="!isChecked()"
          >
            @if (isChecked()) {
              <svg class="w-3 h-3 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="3" d="M5 13l4 4L19 7" />
              </svg>
            }
          </button>
        }

        <!-- Expand/Collapse Toggle -->
        @if (hasChildren()) {
          <button
            type="button"
            (click)="onToggle($event)"
            class="flex-shrink-0 w-5 h-5 flex items-center justify-center text-text-muted hover:text-text rounded transition-colors"
          >
            <svg
              class="w-4 h-4 transition-transform duration-200"
              [class.rotate-90]="isExpanded()"
              fill="none"
              stroke="currentColor"
              viewBox="0 0 24 24"
            >
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5l7 7-7 7" />
            </svg>
          </button>
        } @else {
          <span class="w-5"></span>
        }

        <!-- Node Icon -->
        <div [class]="iconContainerClasses()">
          @switch (node().type) {
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

        <!-- Node Content -->
        <div class="flex-1 min-w-0 flex items-center gap-2">
          <span class="font-medium text-sm text-text truncate">{{ node().name }}</span>
          @if (node().code) {
            <span class="flex-shrink-0 px-1.5 py-0.5 text-[10px] font-medium text-primary-600 bg-primary-50 rounded">
              {{ node().code }}
            </span>
          }
        </div>

        <!-- Collapsed Stats Chips (shown when branch is collapsed) -->
        @if (node().type === 'branch' && node().stats && hasChildren() && !isExpanded()) {
          <div class="flex items-center gap-1">
            @if ((node().stats?.departmentCount ?? 0) > 0) {
              <span class="px-1.5 py-0.5 text-[10px] font-semibold text-blue-700 bg-blue-50 rounded" title="Departments">
                {{ node().stats?.departmentCount }}
              </span>
            }
            @if ((node().stats?.userCount ?? 0) > 0) {
              <span class="px-1.5 py-0.5 text-[10px] font-semibold text-emerald-700 bg-emerald-50 rounded" title="Users">
                {{ node().stats?.userCount }}
              </span>
            }
            @if ((node().stats?.vehicleCount ?? 0) > 0) {
              <span class="px-1.5 py-0.5 text-[10px] font-semibold text-purple-700 bg-purple-50 rounded" title="Vehicles">
                {{ node().stats?.vehicleCount }}
              </span>
            }
          </div>
        }

        <!-- Expanded Stats (shown when branch is expanded) -->
        @if (node().type === 'branch' && node().stats && isExpanded()) {
          <div class="hidden sm:flex items-center gap-3 text-[11px] text-text-muted">
            @if (node().stats?.departmentCount !== undefined) {
              <span class="flex items-center gap-1">
                <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4" />
                </svg>
                {{ node().stats?.departmentCount }} Depts
              </span>
            }
            @if (node().stats?.userCount !== undefined) {
              <span class="flex items-center gap-1">
                <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4.354a4 4 0 110 5.292M15 21H3v-1a6 6 0 0112 0v1zm0 0h6v-1a6 6 0 00-9-5.197M13 7a4 4 0 11-8 0 4 4 0 018 0z" />
                </svg>
                {{ node().stats?.userCount }} Users
              </span>
            }
            @if (node().stats?.vehicleCount !== undefined) {
              <span class="flex items-center gap-1">
                <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path d="M9 17a2 2 0 11-4 0 2 2 0 014 0zM19 17a2 2 0 11-4 0 2 2 0 014 0z" />
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 16V6a1 1 0 00-1-1H4a1 1 0 00-1 1v10a1 1 0 001 1h1m8-1a1 1 0 01-1 1H9m4-1V8a1 1 0 011-1h2.586a1 1 0 01.707.293l3.414 3.414a1 1 0 01.293.707V16a1 1 0 01-1 1h-1m-6-1a1 1 0 001 1h1M5 17a2 2 0 104 0m-4 0a2 2 0 114 0m6 0a2 2 0 104 0m-4 0a2 2 0 114 0" />
                </svg>
                {{ node().stats?.vehicleCount }} Veh
              </span>
            }
          </div>
        }

        <!-- Department Stats -->
        @if (node().type === 'department' && node().stats) {
          <div class="flex items-center gap-2 text-[11px] text-text-muted">
            @if (node().stats?.userCount !== undefined) {
              <span>{{ node().stats?.userCount }} Users</span>
            }
            @if (node().stats?.vehicleCount !== undefined && (node().stats?.vehicleCount ?? 0) > 0) {
              <span>{{ node().stats?.vehicleCount }} Vehicles</span>
            }
          </div>
        }
      </div>

      <!-- Children with connecting lines -->
      @if (hasChildren() && isExpanded()) {
        <div class="children-container" [style.margin-left.px]="level() * 20 + 8">
          @for (child of node().children; track child.id; let last = $last) {
            <div class="relative" [class.pb-0]="last">
              <app-tree-node
                [node]="child"
                [level]="level() + 1"
                [selectedNodeId]="selectedNodeId()"
                [expandedNodeIds]="expandedNodeIds()"
                [multiSelectEnabled]="multiSelectEnabled()"
                [checkedNodeIds]="checkedNodeIds()"
                (nodeSelect)="nodeSelect.emit($event)"
                (nodeToggle)="nodeToggle.emit($event)"
                (nodeCheck)="nodeCheck.emit($event)"
                (nodeContextMenu)="nodeContextMenu.emit($event)"
              />
            </div>
          }
        </div>
      }
    </div>
  `,
})
export class TreeNodeComponent {
  readonly node = input.required<OrganizationNode>();
  readonly level = input<number>(0);
  readonly selectedNodeId = input<string | null>(null);
  readonly expandedNodeIds = input<Set<string>>(new Set());
  readonly multiSelectEnabled = input<boolean>(false);
  readonly checkedNodeIds = input<Set<string>>(new Set());

  readonly nodeSelect = output<OrganizationNode>();
  readonly nodeToggle = output<string>();
  readonly nodeCheck = output<{ nodeId: string; checked: boolean }>();
  readonly nodeContextMenu = output<{ node: OrganizationNode; x: number; y: number }>();

  readonly hasChildren = computed(() => this.node().children.length > 0);

  readonly isExpanded = computed(() => this.expandedNodeIds().has(this.node().id));

  readonly isSelected = computed(() => this.selectedNodeId() === this.node().id);

  readonly isChecked = computed(() => this.checkedNodeIds().has(this.node().id));

  readonly nodeClasses = computed(() => {
    const base =
      'flex items-center gap-2 py-2 px-2 rounded-lg cursor-pointer transition-all duration-150';
    const selected = this.isSelected()
      ? 'bg-primary-50 border border-primary-200'
      : 'hover:bg-surface-alt border border-transparent';
    return `${base} ${selected}`;
  });

  readonly iconContainerClasses = computed(() => {
    const base = 'flex-shrink-0 w-7 h-7 rounded-lg flex items-center justify-center';
    switch (this.node().type) {
      case 'tenant':
        return `${base} bg-primary-100 text-primary-700`;
      case 'branch':
        return `${base} bg-primary-50 text-primary-600`;
      case 'department':
        return `${base} bg-gray-100 text-gray-600`;
      default:
        return `${base} bg-gray-100 text-gray-600`;
    }
  });

  onSelect(): void {
    this.nodeSelect.emit(this.node());
  }

  onToggle(event: Event): void {
    event.stopPropagation();
    this.nodeToggle.emit(this.node().id);
  }

  onCheckChange(event: Event): void {
    event.stopPropagation();
    this.nodeCheck.emit({ nodeId: this.node().id, checked: !this.isChecked() });
  }

  onContextMenu(event: MouseEvent): void {
    if (this.node().type === 'tenant') return;
    event.preventDefault();
    event.stopPropagation();
    this.nodeContextMenu.emit({
      node: this.node(),
      x: event.clientX,
      y: event.clientY,
    });
  }
}
