import {
  Component,
  ChangeDetectionStrategy,
  inject,
  input,
  output,
  signal,
  computed,
  OnInit,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Tenant } from '../../../../core/models/platform.model';

@Component({
  selector: 'app-tenant-selector',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="relative">
      <label class="block text-xs font-medium text-text-muted mb-1">Select Tenant</label>
      <div class="relative">
        <button
          type="button"
          (click)="toggleDropdown()"
          class="w-full flex items-center justify-between gap-2 px-3 py-2 bg-white border border-border rounded-lg text-sm text-text hover:border-border-strong focus:outline-none focus:ring-2 focus:ring-primary-600/20 focus:border-primary-600 transition-all"
        >
          <div class="flex items-center gap-2 min-w-0">
            <div class="w-6 h-6 rounded bg-primary-50 flex items-center justify-center flex-shrink-0">
              <svg class="w-3.5 h-3.5 text-primary-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4" />
              </svg>
            </div>
            <span class="truncate font-medium">{{ selectedTenantName() }}</span>
          </div>
          <svg
            class="w-4 h-4 text-text-muted flex-shrink-0 transition-transform"
            [class.rotate-180]="isOpen()"
            fill="none"
            stroke="currentColor"
            viewBox="0 0 24 24"
          >
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7" />
          </svg>
        </button>

        @if (isOpen()) {
          <div class="absolute z-50 top-full left-0 right-0 mt-1 bg-white border border-border rounded-lg shadow-lg max-h-64 overflow-y-auto">
            @if (loading()) {
              <div class="px-3 py-4 text-center text-sm text-text-muted">
                Loading tenants...
              </div>
            } @else if (tenants().length === 0) {
              <div class="px-3 py-4 text-center text-sm text-text-muted">
                No tenants available
              </div>
            } @else {
              @for (tenant of tenants(); track tenant.id) {
                <button
                  type="button"
                  (click)="selectTenant(tenant)"
                  class="w-full flex items-center gap-2 px-3 py-2 text-sm text-left hover:bg-surface-alt transition-colors"
                  [class.bg-primary-50]="tenant.id === selectedTenantId()"
                >
                  <div class="w-6 h-6 rounded bg-primary-50 flex items-center justify-center flex-shrink-0">
                    <svg class="w-3.5 h-3.5 text-primary-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4" />
                    </svg>
                  </div>
                  <div class="min-w-0 flex-1">
                    <div class="font-medium text-text truncate">{{ tenant.name }}</div>
                    <div class="text-xs text-text-muted">
                      {{ tenant.branchCount }} branches · {{ tenant.activeUserCount }} users
                    </div>
                  </div>
                  @if (tenant.id === selectedTenantId()) {
                    <svg class="w-4 h-4 text-primary-600 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" />
                    </svg>
                  }
                </button>
              }
            }
          </div>
        }
      </div>
    </div>
  `,
  host: {
    '(document:click)': 'onDocumentClick($event)',
  },
})
export class TenantSelectorComponent {
  readonly tenants = input<Tenant[]>([]);
  readonly selectedTenantId = input<number | null>(null);
  readonly loading = input<boolean>(false);

  readonly tenantSelected = output<number>();

  readonly isOpen = signal(false);

  readonly selectedTenantName = computed(() => {
    const id = this.selectedTenantId();
    if (!id) return 'Select a tenant...';
    const tenant = this.tenants().find(t => t.id === id);
    return tenant?.name ?? 'Unknown Tenant';
  });

  private buttonElement: HTMLElement | null = null;

  toggleDropdown(): void {
    this.isOpen.update(v => !v);
  }

  selectTenant(tenant: Tenant): void {
    this.tenantSelected.emit(tenant.id);
    this.isOpen.set(false);
  }

  onDocumentClick(event: Event): void {
    const target = event.target as HTMLElement;
    if (!target.closest('app-tenant-selector')) {
      this.isOpen.set(false);
    }
  }
}
