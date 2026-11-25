import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { Router } from '@angular/router';
import { catchError, of } from 'rxjs';
import { UiEmptyStateComponent, UiModalComponent, UiPageHeaderComponent, UiStatusBadgeComponent } from '../../../../shared/components/ui';
import { HasPermissionDirective } from '../../../../shared/directives/has-permission.directive';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';
import { CHECKLIST_STATUS_VARIANT, TRAVEL_PERMISSIONS, TravelChecklistListItem, TravelVisaType } from '../../models/travel.models';
import { asUtc } from '../../models/travel-date.util';
import { TravelChecklistService } from '../../services/travel-checklist.service';
import { TravelConfigService } from '../../services/travel-config.service';

@Component({
  selector: 'app-travel-checklist-list',
  standalone: true,
  imports: [DatePipe, MatButtonModule, MatIconModule, UiPageHeaderComponent, UiEmptyStateComponent, UiStatusBadgeComponent,
    UiModalComponent, HasPermissionDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="tn-page">
      <ui-page-header title="Document Checklists" eyebrow="TravelNexus" icon="checklist"
        subtitle="Required documents per country and visa type. Cases keep the version they started with."
        [breadcrumbs]="[{ label: 'TravelNexus' }, { label: 'Document checklists' }]">
        <button mat-flat-button color="primary" *appHasPermission="perms.settings" (click)="openCreate()"><mat-icon>add</mat-icon> New checklist</button>
      </ui-page-header>

      <div class="tn-card">
        @if (error()) {
          <div class="tn-card-body"><div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ error() }}
            <button mat-stroked-button class="ml-auto" (click)="load()">Retry</button></div></div>
        } @else {
          <div class="overflow-x-auto">
            <table class="w-full min-w-[760px] text-left text-sm">
              <thead class="bg-fleet-surface-alt/60">
                <tr class="text-[11px] font-bold uppercase tracking-wider text-fleet-text-muted">
                  <th class="px-5 py-3">Country</th><th class="px-5 py-3">Visa type</th><th class="px-5 py-3">Name</th>
                  <th class="px-5 py-3 text-center">Version</th><th class="px-5 py-3">Status</th><th class="px-5 py-3 text-center">Documents</th>
                  <th class="px-5 py-3">Activated</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-fleet-border">
                @if (loading()) {
                  @for (s of [1,2,3]; track s) { <tr>@for (c of [1,2,3,4,5,6,7]; track c) { <td class="px-5 py-4"><div class="h-3.5 w-3/4 animate-pulse rounded bg-fleet-surface-muted"></div></td> }</tr> }
                } @else if (!rows().length) {
                  <tr><td colspan="7"><ui-empty-state icon="checklist" title="No checklists yet"
                    description="Create one per destination (e.g. Saudi Arabia · Umrah) so visa cases list the right documents."></ui-empty-state></td></tr>
                } @else {
                  @for (r of rows(); track r.id) {
                    <tr class="cursor-pointer hover:bg-fleet-surface-alt/40" (click)="open(r)">
                      <td class="px-5 py-3 font-semibold">{{ r.countryName }}</td>
                      <td class="px-5 py-3">{{ r.visaTypeName || 'All visa types' }}</td>
                      <td class="px-5 py-3">{{ r.name }}</td>
                      <td class="px-5 py-3 text-center">v{{ r.version }}</td>
                      <td class="px-5 py-3"><ui-status-badge [status]="r.status" [variant]="statusVariant[r.status]"></ui-status-badge></td>
                      <td class="px-5 py-3 text-center">{{ r.itemCount }}</td>
                      <td class="px-5 py-3 text-fleet-text-muted">{{ r.activatedAt ? (utc(r.activatedAt) | date: 'mediumDate') : '—' }}</td>
                    </tr>
                  }
                }
              </tbody>
            </table>
          </div>
        }
      </div>
    </div>

    <ui-modal [(open)]="createOpen" title="New checklist" size="sm">
      <div class="space-y-4">
        <div class="tn-field"><label for="cl-country" class="tn-required">Country</label>
          <select id="cl-country" class="tn-input" [value]="country()" (change)="country.set($any($event.target).value); visaTypeId.set(null)">
            <option value="">Choose…</option>
            @for (c of countries(); track c.code) { <option [value]="c.code">{{ c.name }}</option> }
          </select></div>
        <div class="tn-field"><label for="cl-vt">Visa type</label>
          <select id="cl-vt" class="tn-input" [value]="visaTypeId() ?? ''" (change)="visaTypeId.set(+$any($event.target).value || null)">
            <option value="">All visa types of the country</option>
            @for (v of typesForCountry(); track v.id) { <option [value]="v.id">{{ v.name }}</option> }
          </select></div>
        <div class="tn-field"><label for="cl-name" class="tn-required">Name</label>
          <input id="cl-name" class="tn-input" maxlength="150" [value]="name()" (input)="name.set($any($event.target).value)" placeholder="e.g. Umrah visa documents" /></div>
        @if (dialogError()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ dialogError() }}</div> }
      </div>
      <div modal-footer class="tn-actions">
        <button mat-stroked-button (click)="createOpen.set(false)">Cancel</button>
        <button mat-flat-button color="primary" [disabled]="busy() || !country() || !name().trim()" (click)="create()">Create draft</button>
      </div>
    </ui-modal>
  `,
  styleUrl: '../../travel-nexus.scss'
})
export class TravelChecklistListComponent {
  private readonly api = inject(TravelChecklistService);
  private readonly config = inject(TravelConfigService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly perms = TRAVEL_PERMISSIONS;
  protected readonly statusVariant = CHECKLIST_STATUS_VARIANT;
  protected readonly utc = asUtc;
  protected readonly rows = signal<TravelChecklistListItem[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  protected readonly createOpen = signal(false);
  protected readonly busy = signal(false);
  protected readonly dialogError = signal<string | null>(null);
  protected readonly country = signal('');
  protected readonly visaTypeId = signal<number | null>(null);
  protected readonly name = signal('');
  protected readonly countries = toSignal(this.config.countries$.pipe(catchError(() => of([]))), { initialValue: [] });
  private readonly visaTypes = toSignal(this.config.getVisaTypes().pipe(catchError(() => of([] as TravelVisaType[]))), { initialValue: [] as TravelVisaType[] });
  protected readonly typesForCountry = computed(() => this.visaTypes().filter(v => v.countryCode === this.country()));

  constructor() { this.load(); }

  load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.getChecklists().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: r => { this.rows.set(r); this.loading.set(false); },
      error: err => { this.error.set(apiErrorMessage(err, 'Could not load checklists.')); this.loading.set(false); }
    });
  }

  protected open(r: TravelChecklistListItem): void { this.router.navigate(['/travel/checklists', r.id]); }

  protected openCreate(): void {
    this.country.set('');
    this.visaTypeId.set(null);
    this.name.set('');
    this.dialogError.set(null);
    this.createOpen.set(true);
  }

  protected create(): void {
    this.busy.set(true);
    this.api.create({ countryCode: this.country(), visaTypeId: this.visaTypeId(), name: this.name().trim(), notes: null })
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: id => { this.busy.set(false); this.createOpen.set(false); this.router.navigate(['/travel/checklists', id]); },
        error: err => { this.busy.set(false); this.dialogError.set(apiErrorMessage(err, 'Could not create the checklist.')); }
      });
  }
}
