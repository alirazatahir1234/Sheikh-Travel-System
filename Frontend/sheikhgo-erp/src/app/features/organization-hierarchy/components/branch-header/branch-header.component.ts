import {
  Component,
  ChangeDetectionStrategy,
  input,
  output,
  computed,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { Branch, TabItem } from '../../models/organization.models';
import { StatusBadgeComponent } from '../status-badge/status-badge.component';
import { TabNavigationComponent } from '../tab-navigation/tab-navigation.component';
import { BranchHealth } from '../../services/organization-hierarchy.service';

export interface BranchManagerInfo {
  id: number;
  name: string;
  avatarUrl?: string;
  isPlaceholder?: boolean;
}

@Component({
  selector: 'app-branch-header',
  standalone: true,
  imports: [CommonModule, StatusBadgeComponent, TabNavigationComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="bg-white rounded-t-lg border border-border border-b-0">
      <!-- Top Section -->
      <div class="px-6 py-4">
        <div class="flex items-start justify-between">
          <!-- Branch Info -->
          <div class="flex items-center gap-4">
            <!-- Branch Icon with Manager Avatar or Default -->
            <div class="relative">
              @if (manager(); as mgr) {
                @if (mgr.avatarUrl) {
                  <img
                    [src]="mgr.avatarUrl"
                    [alt]="mgr.name"
                    class="w-12 h-12 rounded-xl object-cover border-2 border-primary-200"
                  />
                } @else {
                  <div class="w-12 h-12 rounded-xl bg-primary-100 border-2 border-primary-200 flex items-center justify-center text-primary-700 font-bold text-lg">
                    {{ getInitials(mgr.name) }}
                  </div>
                }
                @if (mgr.isPlaceholder) {
                  <div class="absolute -bottom-1 -right-1 w-4 h-4 bg-amber-500 rounded-full border-2 border-white flex items-center justify-center" title="Manager not assigned">
                    <svg class="w-2.5 h-2.5 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="3" d="M12 9v2m0 4h.01" />
                    </svg>
                  </div>
                }
              } @else {
                <div class="w-12 h-12 rounded-xl bg-primary-50 border border-primary-100 flex items-center justify-center">
                  <svg class="w-6 h-6 text-primary-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4" />
                  </svg>
                </div>
              }
            </div>

            <!-- Name and Details -->
            <div>
              <div class="flex items-center gap-3 mb-1">
                <h2 class="text-xl font-bold text-text">{{ branch().name }}</h2>
                <app-status-badge [status]="branch().status" />
                <!-- Health Badge -->
                @if (health(); as h) {
                  <span [class]="getHealthBadgeClass(h)">
                    @switch (h) {
                      @case ('Healthy') {
                        <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" />
                        </svg>
                      }
                      @case ('NoManager') {
                        <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
                        </svg>
                      }
                      @default {
                        <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                        </svg>
                      }
                    }
                    {{ getHealthLabel(h) }}
                  </span>
                }
              </div>
              <div class="flex items-center gap-4 text-sm text-text-muted">
                <span class="flex items-center gap-1.5">
                  <span class="text-text-soft">ID:</span>
                  <span class="font-medium text-text">{{ branchDisplayId() }}</span>
                </span>
                @if (manager(); as mgr) {
                  <span class="w-px h-4 bg-border"></span>
                  <span class="flex items-center gap-1.5">
                    <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z" />
                    </svg>
                    {{ mgr.name }}
                    @if (mgr.isPlaceholder) {
                      <span class="text-xs text-amber-600">(placeholder)</span>
                    }
                  </span>
                }
                <span class="w-px h-4 bg-border"></span>
                <span class="flex items-center gap-1.5">
                  <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4.354a4 4 0 110 5.292M15 21H3v-1a6 6 0 0112 0v1zm0 0h6v-1a6 6 0 00-9-5.197M13 7a4 4 0 11-8 0 4 4 0 018 0z" />
                  </svg>
                  {{ branch().userCount }} Users
                </span>
                <span class="flex items-center gap-1.5">
                  <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path d="M9 17a2 2 0 11-4 0 2 2 0 014 0zM19 17a2 2 0 11-4 0 2 2 0 014 0z" />
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 16V6a1 1 0 00-1-1H4a1 1 0 00-1 1v10a1 1 0 001 1h1m8-1a1 1 0 01-1 1H9m4-1V8a1 1 0 011-1h2.586a1 1 0 01.707.293l3.414 3.414a1 1 0 01.293.707V16a1 1 0 01-1 1h-1m-6-1a1 1 0 001 1h1M5 17a2 2 0 104 0m-4 0a2 2 0 114 0m6 0a2 2 0 104 0m-4 0a2 2 0 114 0" />
                  </svg>
                  {{ branch().vehicleCount }} Vehicles
                </span>
              </div>
            </div>
          </div>

          <!-- Action Buttons -->
          <div class="flex items-center gap-2">
            <button
              type="button"
              (click)="edit.emit()"
              class="w-9 h-9 rounded-lg border border-border bg-white flex items-center justify-center text-text-muted hover:text-text hover:border-border-strong transition-colors"
              title="Edit Branch"
            >
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15.232 5.232l3.536 3.536m-2.036-5.036a2.5 2.5 0 113.536 3.536L6.5 21.036H3v-3.572L16.732 3.732z" />
              </svg>
            </button>
            <button
              type="button"
              (click)="duplicate.emit()"
              class="w-9 h-9 rounded-lg border border-border bg-white flex items-center justify-center text-text-muted hover:text-text hover:border-border-strong transition-colors"
              title="Duplicate Branch"
            >
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 16H6a2 2 0 01-2-2V6a2 2 0 012-2h8a2 2 0 012 2v2m-6 12h8a2 2 0 002-2v-8a2 2 0 00-2-2h-8a2 2 0 00-2 2v8a2 2 0 002 2z" />
              </svg>
            </button>
            <button
              type="button"
              (click)="delete.emit()"
              class="w-9 h-9 rounded-lg border border-border bg-white flex items-center justify-center text-text-muted hover:text-red-500 hover:border-red-200 transition-colors"
              title="Delete Branch"
            >
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
              </svg>
            </button>
          </div>
        </div>
      </div>

      <!-- Tab Navigation -->
      <app-tab-navigation
        [tabs]="tabs()"
        [activeTab]="activeTab()"
        (tabChange)="tabChange.emit($event)"
      />
    </div>
  `,
})
export class BranchHeaderComponent {
  readonly branch = input.required<Branch>();
  readonly tabs = input.required<TabItem[]>();
  readonly activeTab = input<string>('overview');
  readonly health = input<BranchHealth | null>(null);
  readonly manager = input<BranchManagerInfo | null>(null);

  readonly edit = output<void>();
  readonly duplicate = output<void>();
  readonly delete = output<void>();
  readonly tabChange = output<string>();

  readonly branchDisplayId = computed(() => {
    const branch = this.branch();
    return `${branch.branchCode}-${branch.city?.substring(0, 3).toUpperCase() ?? 'XXX'}`;
  });

  getInitials(name: string): string {
    return name
      .split(' ')
      .map(part => part[0])
      .join('')
      .toUpperCase()
      .slice(0, 2);
  }

  getHealthBadgeClass(health: BranchHealth): string {
    const base = 'inline-flex items-center gap-1 px-2 py-0.5 text-[10px] font-semibold rounded-full';
    switch (health) {
      case 'Healthy':
        return `${base} bg-emerald-50 text-emerald-700`;
      case 'NoManager':
        return `${base} bg-amber-50 text-amber-700`;
      case 'NoDepartments':
        return `${base} bg-orange-50 text-orange-700`;
      case 'Inactive':
        return `${base} bg-gray-50 text-gray-600`;
      default:
        return `${base} bg-gray-50 text-gray-600`;
    }
  }

  getHealthLabel(health: BranchHealth): string {
    switch (health) {
      case 'Healthy':
        return 'Healthy';
      case 'NoManager':
        return 'No Manager';
      case 'NoDepartments':
        return 'No Depts';
      case 'Inactive':
        return 'Inactive';
      default:
        return health;
    }
  }
}
