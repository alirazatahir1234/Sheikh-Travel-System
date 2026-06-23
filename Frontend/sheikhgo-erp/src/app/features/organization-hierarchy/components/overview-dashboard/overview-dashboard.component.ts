import {
  Component,
  ChangeDetectionStrategy,
  input,
  output,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { AuditLog } from '../../models/organization.models';
import { TenantOverviewStats } from '../../services/organization-hierarchy.service';

@Component({
  selector: 'app-overview-dashboard',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="bg-white rounded-lg border border-border overflow-hidden">
      <!-- Header -->
      <div class="px-6 py-4 border-b border-border">
        <h2 class="text-lg font-semibold text-text">Organization Overview</h2>
        @if (tenantName()) {
          <p class="text-sm text-text-muted mt-0.5">{{ tenantName() }}</p>
        }
      </div>

      <!-- Stats Grid -->
      <div class="p-6">
        <div class="grid grid-cols-2 md:grid-cols-3 gap-4">
          @for (stat of statCards(); track stat.label) {
            <div class="bg-surface-alt rounded-lg p-4 border border-border/50">
              <div class="flex items-center gap-3">
                <div [class]="stat.iconBgClass + ' w-10 h-10 rounded-lg flex items-center justify-center'">
                  @switch (stat.icon) {
                    @case ('branch') {
                      <svg class="w-5 h-5" [class]="stat.iconClass" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4" />
                      </svg>
                    }
                    @case ('department') {
                      <svg class="w-5 h-5" [class]="stat.iconClass" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 7v10a2 2 0 002 2h14a2 2 0 002-2V9a2 2 0 00-2-2h-6l-2-2H5a2 2 0 00-2 2z" />
                      </svg>
                    }
                    @case ('user') {
                      <svg class="w-5 h-5" [class]="stat.iconClass" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4.354a4 4 0 110 5.292M15 21H3v-1a6 6 0 0112 0v1zm0 0h6v-1a6 6 0 00-9-5.197M13 7a4 4 0 11-8 0 4 4 0 018 0z" />
                      </svg>
                    }
                    @case ('vehicle') {
                      <svg class="w-5 h-5" [class]="stat.iconClass" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path d="M9 17a2 2 0 11-4 0 2 2 0 014 0zM19 17a2 2 0 11-4 0 2 2 0 014 0z" />
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 16V6a1 1 0 00-1-1H4a1 1 0 00-1 1v10a1 1 0 001 1h1m8-1a1 1 0 01-1 1H9m4-1V8a1 1 0 011-1h2.586a1 1 0 01.707.293l3.414 3.414a1 1 0 01.293.707V16a1 1 0 01-1 1h-1m-6-1a1 1 0 001 1h1M5 17a2 2 0 104 0m-4 0a2 2 0 114 0m6 0a2 2 0 104 0m-4 0a2 2 0 114 0" />
                      </svg>
                    }
                    @case ('driver') {
                      <svg class="w-5 h-5" [class]="stat.iconClass" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z" />
                      </svg>
                    }
                    @case ('gps') {
                      <svg class="w-5 h-5" [class]="stat.iconClass" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17.657 16.657L13.414 20.9a1.998 1.998 0 01-2.827 0l-4.244-4.243a8 8 0 1111.314 0z" />
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 11a3 3 0 11-6 0 3 3 0 016 0z" />
                      </svg>
                    }
                  }
                </div>
                <div>
                  <div class="text-2xl font-bold text-text">
                    {{ stat.value }}
                    @if (stat.isPlaceholder) {
                      <span class="text-xs font-normal text-text-muted ml-1">(est.)</span>
                    }
                  </div>
                  <div class="text-xs text-text-muted">{{ stat.label }}</div>
                </div>
              </div>
            </div>
          }
        </div>

        <!-- Quick Actions -->
        <div class="mt-6">
          <h3 class="text-sm font-semibold text-text mb-3">Quick Actions</h3>
          <div class="flex flex-wrap gap-2">
            <button
              type="button"
              (click)="addBranch.emit()"
              class="flex items-center gap-2 px-3 py-2 text-sm font-medium text-primary-700 bg-primary-50 border border-primary-200 rounded-lg hover:bg-primary-100 transition-colors"
            >
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
              </svg>
              Add Branch
            </button>
            <button
              type="button"
              (click)="addDepartment.emit()"
              class="flex items-center gap-2 px-3 py-2 text-sm font-medium text-text-muted bg-surface-alt border border-border rounded-lg hover:bg-gray-100 transition-colors"
            >
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
              </svg>
              Add Department
            </button>
            <button
              type="button"
              (click)="importData.emit()"
              class="flex items-center gap-2 px-3 py-2 text-sm font-medium text-text-muted bg-surface-alt border border-border rounded-lg hover:bg-gray-100 transition-colors"
            >
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-8l-4-4m0 0L8 8m4-4v12" />
              </svg>
              Import Data
            </button>
          </div>
        </div>

        <!-- Recent Activity -->
        @if (recentActivity().length > 0) {
          <div class="mt-6">
            <h3 class="text-sm font-semibold text-text mb-3">Recent Activity</h3>
            <div class="space-y-2">
              @for (activity of recentActivity().slice(0, 5); track activity.id) {
                <div class="flex items-start gap-3 p-3 bg-surface-alt rounded-lg">
                  <div class="flex-shrink-0 mt-0.5">
                    <div class="w-2 h-2 rounded-full" [class]="getActivityDotClass(activity.severity)"></div>
                  </div>
                  <div class="flex-1 min-w-0">
                    <p class="text-sm text-text">
                      <span class="font-medium">{{ activity.userName }}</span>
                      {{ activity.action.toLowerCase() }}
                    </p>
                    <p class="text-xs text-text-muted mt-0.5">{{ formatTimestamp(activity.timestamp) }}</p>
                  </div>
                </div>
              }
            </div>
          </div>
        }

        <!-- Help Text -->
        <div class="mt-6 p-4 bg-blue-50 border border-blue-100 rounded-lg">
          <div class="flex items-start gap-3">
            <svg class="w-5 h-5 text-blue-600 flex-shrink-0 mt-0.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
            <div>
              <p class="text-sm font-medium text-blue-900">Getting Started</p>
              <p class="text-sm text-blue-700 mt-1">
                Select a branch from the organization tree to view and manage its departments, users, vehicles, and drivers.
              </p>
            </div>
          </div>
        </div>
      </div>
    </div>
  `,
})
export class OverviewDashboardComponent {
  readonly tenantName = input<string | null>(null);
  readonly stats = input<TenantOverviewStats | null>(null);
  readonly recentActivity = input<AuditLog[]>([]);

  readonly addBranch = output<void>();
  readonly addDepartment = output<void>();
  readonly importData = output<void>();

  statCards() {
    const s = this.stats();
    if (!s) return [];

    return [
      {
        label: 'Branches',
        value: s.branchCount,
        icon: 'branch',
        iconBgClass: 'bg-primary-100',
        iconClass: 'text-primary-600',
        isPlaceholder: false,
      },
      {
        label: 'Departments',
        value: s.departmentCount,
        icon: 'department',
        iconBgClass: 'bg-amber-100',
        iconClass: 'text-amber-600',
        isPlaceholder: false,
      },
      {
        label: 'Users',
        value: s.userCount,
        icon: 'user',
        iconBgClass: 'bg-emerald-100',
        iconClass: 'text-emerald-600',
        isPlaceholder: false,
      },
      {
        label: 'Vehicles',
        value: s.vehicleCount,
        icon: 'vehicle',
        iconBgClass: 'bg-blue-100',
        iconClass: 'text-blue-600',
        isPlaceholder: false,
      },
      {
        label: 'Drivers',
        value: s.driverCount,
        icon: 'driver',
        iconBgClass: 'bg-purple-100',
        iconClass: 'text-purple-600',
        isPlaceholder: s.isPlaceholder.driverCount,
      },
      {
        label: 'GPS Devices',
        value: s.gpsDeviceCount,
        icon: 'gps',
        iconBgClass: 'bg-rose-100',
        iconClass: 'text-rose-600',
        isPlaceholder: s.isPlaceholder.gpsDeviceCount,
      },
    ];
  }

  getActivityDotClass(severity: 'info' | 'warning' | 'error'): string {
    switch (severity) {
      case 'warning':
        return 'bg-amber-500';
      case 'error':
        return 'bg-red-500';
      default:
        return 'bg-emerald-500';
    }
  }

  formatTimestamp(timestamp: string): string {
    const date = new Date(timestamp);
    const now = new Date();
    const diffMs = now.getTime() - date.getTime();
    const diffMins = Math.floor(diffMs / 60000);
    const diffHours = Math.floor(diffMs / 3600000);
    const diffDays = Math.floor(diffMs / 86400000);

    if (diffMins < 1) return 'Just now';
    if (diffMins < 60) return `${diffMins}m ago`;
    if (diffHours < 24) return `${diffHours}h ago`;
    if (diffDays < 7) return `${diffDays}d ago`;

    return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
  }
}
