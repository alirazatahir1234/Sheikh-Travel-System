import { CurrencyPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { Router, RouterLink } from '@angular/router';
import { UiEmptyStateComponent, UiPageHeaderComponent, UiStatusBadgeComponent } from '../../../../shared/components/ui';
import { HasPermissionDirective } from '../../../../shared/directives/has-permission.directive';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';
import { PACKAGE_STATUS_VARIANT, TRAVEL_PERMISSIONS, TravelPackageListItem, TravelPackageStatus } from '../../models/travel.models';
import { TravelPackageService } from '../../services/travel-package.service';

@Component({
  selector: 'app-travel-package-list',
  standalone: true,
  imports: [CurrencyPipe, RouterLink, MatButtonModule, MatIconModule, UiPageHeaderComponent, UiEmptyStateComponent, UiStatusBadgeComponent,
    HasPermissionDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="tn-page">
      <ui-page-header title="Packages" eyebrow="TravelNexus" icon="inventory_2"
        subtitle="Umrah and Hajj packages: nights, hotels and room prices. Groups are created from published packages."
        [breadcrumbs]="[{ label: 'TravelNexus' }, { label: 'Packages' }]">
        <a mat-flat-button color="primary" routerLink="/travel/packages/new" *appHasPermission="perms.packageManage"><mat-icon>add</mat-icon> New package</a>
      </ui-page-header>

      <div class="tn-card">
        <div class="flex flex-wrap gap-2 border-b border-fleet-border px-5 py-4" role="group" aria-label="Filter by status">
          @for (s of statuses; track s.value) {
            <button type="button" class="tn-chip" [class.active]="status() === s.value" (click)="setStatus(s.value)">{{ s.label }}</button>
          }
        </div>
        @if (error()) {
          <div class="tn-card-body"><div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ error() }}
            <button mat-stroked-button class="ml-auto" (click)="load()">Retry</button></div></div>
        } @else {
          <div class="overflow-x-auto">
            <table class="w-full min-w-[820px] text-left text-sm">
              <thead class="bg-fleet-surface-alt/60">
                <tr class="text-[11px] font-bold uppercase tracking-wider text-fleet-text-muted">
                  <th class="px-5 py-3">Code</th><th class="px-5 py-3">Package</th><th class="px-5 py-3">Type</th>
                  <th class="px-5 py-3 text-center">Nights (Makkah / Madinah)</th><th class="px-5 py-3">Visa type</th>
                  <th class="px-5 py-3 text-right">From (adult)</th><th class="px-5 py-3">Status</th><th class="px-5 py-3 text-center">Groups</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-fleet-border">
                @if (loading()) {
                  @for (s of [1,2,3]; track s) { <tr>@for (c of [1,2,3,4,5,6,7,8]; track c) { <td class="px-5 py-4"><div class="h-3.5 w-3/4 animate-pulse rounded bg-fleet-surface-muted"></div></td> }</tr> }
                } @else if (!rows().length) {
                  <tr><td colspan="8"><ui-empty-state icon="inventory_2" title="No packages"
                    [description]="status() ? 'No packages with this status.' : 'Create a package, add hotels and room prices, then publish it.'"></ui-empty-state></td></tr>
                } @else {
                  @for (r of rows(); track r.id) {
                    <tr class="cursor-pointer hover:bg-fleet-surface-alt/40" (click)="open(r)">
                      <td class="px-5 py-3 font-mono text-xs">{{ r.code }}</td>
                      <td class="px-5 py-3 font-semibold">{{ r.name }}</td>
                      <td class="px-5 py-3">{{ r.packageType }}</td>
                      <td class="px-5 py-3 text-center">{{ r.durationNights }} ({{ r.nightsMakkah }} / {{ r.nightsMadinah }})</td>
                      <td class="px-5 py-3">{{ r.visaTypeName || '—' }}</td>
                      <td class="px-5 py-3 text-right">{{ r.fromPrice !== null ? (r.fromPrice | currency: r.currencyCode : 'code' : '1.0-2') : '—' }}</td>
                      <td class="px-5 py-3"><ui-status-badge [status]="r.status" [variant]="statusVariant[r.status]"></ui-status-badge></td>
                      <td class="px-5 py-3 text-center">{{ r.groupCount }}</td>
                    </tr>
                  }
                }
              </tbody>
            </table>
          </div>
        }
      </div>
    </div>
  `,
  styleUrl: '../../travel-nexus.scss'
})
export class TravelPackageListComponent {
  private readonly api = inject(TravelPackageService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly perms = TRAVEL_PERMISSIONS;
  protected readonly statusVariant = PACKAGE_STATUS_VARIANT;
  protected readonly statuses: { value: TravelPackageStatus | ''; label: string }[] = [
    { value: '', label: 'All' }, { value: 'Draft', label: 'Draft' }, { value: 'Published', label: 'Published' }, { value: 'Archived', label: 'Archived' }
  ];
  protected readonly status = signal<TravelPackageStatus | ''>('');
  protected readonly rows = signal<TravelPackageListItem[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  constructor() { this.load(); }

  protected setStatus(s: TravelPackageStatus | ''): void { this.status.set(s); this.load(); }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.getPackages(this.status() || undefined).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: r => { this.rows.set(r); this.loading.set(false); },
      error: err => { this.error.set(apiErrorMessage(err, 'Could not load packages.')); this.loading.set(false); }
    });
  }

  protected open(r: TravelPackageListItem): void { this.router.navigate(['/travel/packages', r.id]); }
}
