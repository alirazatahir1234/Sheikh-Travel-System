import {
  Component,
  ChangeDetectionStrategy,
  input,
  output,
} from '@angular/core';
import { CommonModule } from '@angular/common';

export interface AssetCategory {
  id: string;
  label: string;
  icon: 'vehicle' | 'driver' | 'gps';
  count: number;
  isPlaceholder: boolean;
}

@Component({
  selector: 'app-assets-tab',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div>
      <!-- Header -->
      <div class="flex items-center justify-between mb-4">
        <div>
          <h3 class="text-lg font-semibold text-text">Assets</h3>
          <p class="text-sm text-text-muted">Vehicles, drivers, and GPS devices in this branch</p>
        </div>
        <button
          type="button"
          (click)="addAsset.emit()"
          class="flex items-center gap-2 px-3 py-2 text-sm font-medium text-white bg-primary-600 rounded-lg hover:bg-primary-700 transition-colors"
        >
          <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
          </svg>
          Add Asset
        </button>
      </div>

      <!-- Asset Categories Grid -->
      <div class="grid grid-cols-3 gap-4 mb-6">
        @for (category of categories(); track category.id) {
          <div
            class="p-4 bg-surface-alt rounded-lg border border-border/50 hover:border-border cursor-pointer transition-colors"
            (click)="categorySelected.emit(category.id)"
          >
            <div class="flex items-center gap-3">
              <div [class]="getIconBgClass(category.icon)">
                @switch (category.icon) {
                  @case ('vehicle') {
                    <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path d="M9 17a2 2 0 11-4 0 2 2 0 014 0zM19 17a2 2 0 11-4 0 2 2 0 014 0z" />
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 16V6a1 1 0 00-1-1H4a1 1 0 00-1 1v10a1 1 0 001 1h1m8-1a1 1 0 01-1 1H9m4-1V8a1 1 0 011-1h2.586a1 1 0 01.707.293l3.414 3.414a1 1 0 01.293.707V16a1 1 0 01-1 1h-1m-6-1a1 1 0 001 1h1M5 17a2 2 0 104 0m-4 0a2 2 0 114 0m6 0a2 2 0 104 0m-4 0a2 2 0 114 0" />
                    </svg>
                  }
                  @case ('driver') {
                    <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z" />
                    </svg>
                  }
                  @case ('gps') {
                    <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17.657 16.657L13.414 20.9a1.998 1.998 0 01-2.827 0l-4.244-4.243a8 8 0 1111.314 0z" />
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 11a3 3 0 11-6 0 3 3 0 016 0z" />
                    </svg>
                  }
                }
              </div>
              <div>
                <div class="text-2xl font-bold text-text">
                  {{ category.count }}
                  @if (category.isPlaceholder) {
                    <span class="text-xs font-normal text-text-muted ml-1">*</span>
                  }
                </div>
                <div class="text-xs text-text-muted">{{ category.label }}</div>
              </div>
            </div>
          </div>
        }
      </div>

      <!-- Placeholder Notice -->
      <div class="p-4 bg-blue-50 border border-blue-100 rounded-lg">
        <div class="flex items-start gap-3">
          <svg class="w-5 h-5 text-blue-600 flex-shrink-0 mt-0.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
          </svg>
          <div>
            <p class="text-sm font-medium text-blue-900">Asset Management</p>
            <p class="text-sm text-blue-700 mt-1">
              Branch-level asset counts are derived from tenant quotas. For detailed asset management,
              please use the dedicated Fleet Management module.
            </p>
          </div>
        </div>
      </div>

      <!-- Quick Links -->
      <div class="mt-6">
        <h4 class="text-sm font-semibold text-text mb-3">Quick Actions</h4>
        <div class="flex flex-wrap gap-2">
          <button
            type="button"
            class="flex items-center gap-2 px-3 py-2 text-sm font-medium text-text-muted bg-surface-alt border border-border rounded-lg hover:bg-gray-100 transition-colors"
          >
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path d="M9 17a2 2 0 11-4 0 2 2 0 014 0zM19 17a2 2 0 11-4 0 2 2 0 014 0z" />
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 16V6a1 1 0 00-1-1H4a1 1 0 00-1 1v10a1 1 0 001 1h1m8-1a1 1 0 01-1 1H9m4-1V8a1 1 0 011-1h2.586a1 1 0 01.707.293l3.414 3.414a1 1 0 01.293.707V16a1 1 0 01-1 1h-1m-6-1a1 1 0 001 1h1M5 17a2 2 0 104 0m-4 0a2 2 0 114 0m6 0a2 2 0 104 0m-4 0a2 2 0 114 0" />
            </svg>
            Manage Vehicles
          </button>
          <button
            type="button"
            class="flex items-center gap-2 px-3 py-2 text-sm font-medium text-text-muted bg-surface-alt border border-border rounded-lg hover:bg-gray-100 transition-colors"
          >
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z" />
            </svg>
            Manage Drivers
          </button>
          <button
            type="button"
            class="flex items-center gap-2 px-3 py-2 text-sm font-medium text-text-muted bg-surface-alt border border-border rounded-lg hover:bg-gray-100 transition-colors"
          >
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17.657 16.657L13.414 20.9a1.998 1.998 0 01-2.827 0l-4.244-4.243a8 8 0 1111.314 0z" />
            </svg>
            GPS Tracking
          </button>
        </div>
      </div>
    </div>
  `,
})
export class AssetsTabComponent {
  readonly categories = input<AssetCategory[]>([]);

  readonly addAsset = output<void>();
  readonly categorySelected = output<string>();

  getIconBgClass(icon: string): string {
    const base = 'w-10 h-10 rounded-lg flex items-center justify-center';
    switch (icon) {
      case 'vehicle':
        return `${base} bg-blue-100 text-blue-600`;
      case 'driver':
        return `${base} bg-purple-100 text-purple-600`;
      case 'gps':
        return `${base} bg-rose-100 text-rose-600`;
      default:
        return `${base} bg-gray-100 text-gray-600`;
    }
  }
}
