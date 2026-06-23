import {
  Component,
  ChangeDetectionStrategy,
  input,
  output,
  computed,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { OrganizationNode } from '../../models/organization.models';

@Component({
  selector: 'app-hierarchy-minimap',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="bg-white rounded-lg border border-border shadow-sm overflow-hidden">
      <!-- Header -->
      <div class="px-3 py-2 border-b border-border bg-surface-alt">
        <div class="flex items-center justify-between">
          <span class="text-xs font-semibold text-text-muted uppercase tracking-wide">Hierarchy Map</span>
          <button
            type="button"
            (click)="isExpanded.set(!isExpanded())"
            class="w-5 h-5 flex items-center justify-center text-text-muted hover:text-text rounded transition-colors"
          >
            <svg
              class="w-3.5 h-3.5 transition-transform"
              [class.rotate-180]="isExpanded()"
              fill="none"
              stroke="currentColor"
              viewBox="0 0 24 24"
            >
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7" />
            </svg>
          </button>
        </div>
      </div>

      @if (isExpanded()) {
        <div class="p-2 max-h-48 overflow-y-auto">
          @if (tenant(); as t) {
            <div class="text-xs font-semibold text-text mb-2 flex items-center gap-1.5">
              <div class="w-1.5 h-1.5 rounded-full bg-primary-600"></div>
              {{ t.name }}
            </div>
          }

          <div class="space-y-1 pl-3">
            @for (branch of branches(); track branch.id) {
              <button
                type="button"
                (click)="onBranchClick(branch)"
                class="w-full flex items-center gap-2 px-2 py-1 text-left rounded transition-colors text-xs"
                [class.bg-primary-50]="isSelected(branch.id)"
                [class.text-primary-700]="isSelected(branch.id)"
                [class.text-text]="!isSelected(branch.id)"
                [class.hover:bg-surface-alt]="!isSelected(branch.id)"
              >
                <!-- Connector line -->
                <div class="flex items-center">
                  <div class="w-2 h-px bg-border"></div>
                  <div class="w-1.5 h-1.5 rounded-full"
                    [class.bg-primary-600]="isSelected(branch.id)"
                    [class.bg-text-soft]="!isSelected(branch.id)"
                  ></div>
                </div>
                <span class="truncate">{{ branch.name }}</span>
                @if (branch.stats?.departmentCount) {
                  <span class="flex-shrink-0 text-[10px] text-text-muted">({{ branch.stats?.departmentCount }})</span>
                }
              </button>

              <!-- Child branches -->
              @for (child of branch.children; track child.id) {
                @if (child.type === 'branch') {
                  <button
                    type="button"
                    (click)="onBranchClick(child)"
                    class="w-full flex items-center gap-2 px-2 py-1 pl-6 text-left rounded transition-colors text-xs"
                    [class.bg-primary-50]="isSelected(child.id)"
                    [class.text-primary-700]="isSelected(child.id)"
                    [class.text-text]="!isSelected(child.id)"
                    [class.hover:bg-surface-alt]="!isSelected(child.id)"
                  >
                    <div class="flex items-center">
                      <div class="w-2 h-px bg-border"></div>
                      <div class="w-1 h-1 rounded-full"
                        [class.bg-primary-500]="isSelected(child.id)"
                        [class.bg-text-soft]="!isSelected(child.id)"
                      ></div>
                    </div>
                    <span class="truncate">{{ child.name }}</span>
                  </button>
                }
              }
            }
          </div>

          @if (branches().length === 0) {
            <div class="text-xs text-text-muted text-center py-2">
              No branches yet
            </div>
          }
        </div>
      }
    </div>
  `,
})
export class HierarchyMinimapComponent {
  readonly nodes = input<OrganizationNode[]>([]);
  readonly selectedNodeId = input<string | null>(null);

  readonly branchSelected = output<string>();

  isSelected(nodeId: string): boolean {
    return this.selectedNodeId() === nodeId;
  }

  readonly isExpanded = signal(true);

  readonly tenant = computed(() => {
    const nodes = this.nodes();
    if (nodes.length > 0 && nodes[0].type === 'tenant') {
      return nodes[0];
    }
    return null;
  });

  readonly branches = computed(() => {
    const tenant = this.tenant();
    if (!tenant) return [];

    return tenant.children.filter(c => c.type === 'branch');
  });

  onBranchClick(node: OrganizationNode): void {
    this.branchSelected.emit(node.id);
  }
}
