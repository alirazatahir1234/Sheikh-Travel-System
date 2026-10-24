import {
  Component,
  ChangeDetectionStrategy,
  input,
  output,
  computed,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { ViewMode } from '../../models/organization.models';

export interface BreadcrumbItem {
  label: string;
  type: 'platform' | 'tenant' | 'branch' | 'department';
  id?: string | number;
}

@Component({
  selector: 'app-top-navbar',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="bg-white border-b border-border sticky top-0 z-40">
      <div class="px-4 py-3 md:px-6 md:py-4">
        <div class="flex items-center justify-between gap-3">
          <!-- Left: Back Button & Breadcrumb -->
          <div class="flex items-center gap-3 md:gap-4 min-w-0">
            <button
              type="button"
              (click)="back.emit()"
              class="w-8 h-8 flex-shrink-0 rounded-lg border border-border flex items-center justify-center text-text-muted hover:text-text hover:bg-surface-alt transition-colors"
              aria-label="Back to companies"
            >
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10 19l-7-7m0 0l7-7m-7 7h18" />
              </svg>
            </button>

            <div class="min-w-0">
              <nav class="hidden sm:flex items-center gap-2 text-sm text-text-muted mb-0.5 flex-wrap" aria-label="Breadcrumb">
                <span>Platform</span>
                @for (item of breadcrumb(); track item.label; let isLast = $last) {
                  <svg class="w-3 h-3 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5l7 7-7 7" />
                  </svg>
                  @if (isLast) {
                    <span class="text-primary-600 font-medium truncate">{{ item.label }}</span>
                  } @else {
                    <button
                      type="button"
                      (click)="breadcrumbClick.emit(item)"
                      class="hover:text-text hover:underline transition-colors truncate max-w-[10rem]"
                    >
                      {{ item.label }}
                    </button>
                  }
                }
                @if (breadcrumb().length === 0) {
                  <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5l7 7-7 7" />
                  </svg>
                  <span class="text-primary-600 font-medium">Organization Hierarchy</span>
                }
              </nav>
              <h1 class="text-base md:text-lg font-bold text-text truncate">{{ pageTitle() }}</h1>
            </div>
          </div>

          <!-- Right: View Toggle & Actions -->
          <div class="flex items-center gap-2 md:gap-3 flex-shrink-0">
            <!-- View Mode Toggle -->
            <div class="flex items-center bg-surface-alt rounded-lg p-1" role="group" aria-label="View mode">
              <button
                type="button"
                (click)="viewModeChange.emit('tree')"
                [class]="getViewButtonClasses('tree')"
                [attr.aria-pressed]="viewMode() === 'tree'"
              >
                <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 6h16M4 10h16M4 14h16M4 18h16" />
                </svg>
                <span class="hidden sm:inline">Tree</span>
              </button>
              <button
                type="button"
                (click)="viewModeChange.emit('diagram')"
                [class]="getViewButtonClasses('diagram')"
                [attr.aria-pressed]="viewMode() === 'diagram'"
              >
                <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M7 21a4 4 0 01-4-4V5a2 2 0 012-2h4a2 2 0 012 2v12a4 4 0 01-4 4zm0 0h12a2 2 0 002-2v-4a2 2 0 00-2-2h-2.343M11 7.343l1.657-1.657a2 2 0 012.828 0l2.829 2.829a2 2 0 010 2.828l-8.486 8.485M7 17h.01" />
                </svg>
                <span class="hidden sm:inline">Diagram</span>
              </button>
            </div>

            <!-- Commit Changes Button -->
            <button
              type="button"
              (click)="commitChanges.emit()"
              class="flex items-center gap-2 px-3 py-2 md:px-4 bg-primary-600 text-white text-sm font-medium rounded-lg hover:bg-primary-700 transition-colors"
            >
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M7 16a4 4 0 01-.88-7.903A5 5 0 1115.9 6L16 6a5 5 0 011 9.9M15 13l-3-3m0 0l-3 3m3-3v12" />
              </svg>
              <span class="hidden md:inline">Commit Changes</span>
              <span class="md:hidden">Commit</span>
            </button>
          </div>
        </div>
      </div>
    </header>
  `,
})
export class TopNavbarComponent {
  readonly viewMode = input<ViewMode>('tree');
  readonly breadcrumb = input<BreadcrumbItem[]>([]);

  readonly back = output<void>();
  readonly viewModeChange = output<ViewMode>();
  readonly commitChanges = output<void>();
  readonly breadcrumbClick = output<BreadcrumbItem>();

  readonly pageTitle = computed(() => {
    const items = this.breadcrumb();
    if (items.length === 0) return 'Organization Hierarchy';
    const last = items[items.length - 1];
    return last.label;
  });

  getViewButtonClasses(mode: ViewMode): string {
    const base = 'flex items-center gap-1.5 px-3 py-1.5 text-sm font-medium rounded-md transition-colors';
    const isActive = this.viewMode() === mode;
    
    if (isActive) {
      return `${base} bg-white text-text shadow-sm`;
    }
    return `${base} text-text-muted hover:text-text`;
  }
}
