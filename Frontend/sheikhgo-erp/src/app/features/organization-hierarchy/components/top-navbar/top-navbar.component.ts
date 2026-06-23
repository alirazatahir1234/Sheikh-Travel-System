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
      <div class="px-6 py-4">
        <div class="flex items-center justify-between">
          <!-- Left: Back Button & Breadcrumb -->
          <div class="flex items-center gap-4">
            <button
              type="button"
              (click)="back.emit()"
              class="w-8 h-8 rounded-lg border border-border flex items-center justify-center text-text-muted hover:text-text hover:bg-surface-alt transition-colors"
            >
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10 19l-7-7m0 0l7-7m-7 7h18" />
              </svg>
            </button>

            <div>
              <nav class="flex items-center gap-2 text-sm text-text-muted mb-0.5 flex-wrap">
                <span>Platform</span>
                @for (item of breadcrumb(); track item.label; let isLast = $last) {
                  <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5l7 7-7 7" />
                  </svg>
                  @if (isLast) {
                    <span class="text-primary-600 font-medium">{{ item.label }}</span>
                  } @else {
                    <button
                      type="button"
                      (click)="breadcrumbClick.emit(item)"
                      class="hover:text-text hover:underline transition-colors"
                    >
                      {{ item.label }}
                    </button>
                  }
                }
                @if (breadcrumb().length === 0) {
                  <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5l7 7-7 7" />
                  </svg>
                  <span class="text-primary-600 font-medium">Organization Hierarchy</span>
                }
              </nav>
              <h1 class="text-lg font-bold text-text">{{ pageTitle() }}</h1>
            </div>
          </div>

          <!-- Right: View Toggle & Actions -->
          <div class="flex items-center gap-3">
            <!-- View Mode Toggle -->
            <div class="flex items-center bg-surface-alt rounded-lg p-1">
              <button
                type="button"
                (click)="viewModeChange.emit('tree')"
                [class]="getViewButtonClasses('tree')"
              >
                <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 6h16M4 10h16M4 14h16M4 18h16" />
                </svg>
                Tree
              </button>
              <button
                type="button"
                (click)="viewModeChange.emit('diagram')"
                [class]="getViewButtonClasses('diagram')"
              >
                <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M7 21a4 4 0 01-4-4V5a2 2 0 012-2h4a2 2 0 012 2v12a4 4 0 01-4 4zm0 0h12a2 2 0 002-2v-4a2 2 0 00-2-2h-2.343M11 7.343l1.657-1.657a2 2 0 012.828 0l2.829 2.829a2 2 0 010 2.828l-8.486 8.485M7 17h.01" />
                </svg>
                Diagram
              </button>
            </div>

            <!-- Notification Bell -->
            <button
              type="button"
              class="w-9 h-9 rounded-lg border border-border flex items-center justify-center text-text-muted hover:text-text hover:bg-surface-alt transition-colors relative"
            >
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9" />
              </svg>
              <span class="absolute -top-0.5 -right-0.5 w-2 h-2 bg-red-500 rounded-full"></span>
            </button>

            <!-- Settings -->
            <button
              type="button"
              class="w-9 h-9 rounded-lg border border-border flex items-center justify-center text-text-muted hover:text-text hover:bg-surface-alt transition-colors"
            >
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z" />
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
              </svg>
            </button>

            <!-- Commit Changes Button -->
            <button
              type="button"
              (click)="commitChanges.emit()"
              class="flex items-center gap-2 px-4 py-2 bg-primary-600 text-white text-sm font-medium rounded-lg hover:bg-primary-700 transition-colors"
            >
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M7 16a4 4 0 01-.88-7.903A5 5 0 1115.9 6L16 6a5 5 0 011 9.9M15 13l-3-3m0 0l-3 3m3-3v12" />
              </svg>
              Commit Changes
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
