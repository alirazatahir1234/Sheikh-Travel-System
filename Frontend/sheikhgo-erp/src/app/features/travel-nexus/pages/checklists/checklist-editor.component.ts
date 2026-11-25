import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { UiConfirmService, UiPageHeaderComponent, UiStatusBadgeComponent } from '../../../../shared/components/ui';
import { UiToastService } from '../../../../shared/components/ui/toast/ui-toast.service';
import { HasPermissionDirective } from '../../../../shared/directives/has-permission.directive';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';
import {
  APPLICANT_CONDITION_LABEL, CHECKLIST_STATUS_VARIANT, SaveTravelChecklistItem, TRAVEL_APPLICANT_CONDITIONS, TRAVEL_PERMISSIONS,
  TravelApplicantCondition, TravelChecklist, TravelDocumentType
} from '../../models/travel.models';
import { asUtc } from '../../models/travel-date.util';
import { TravelChecklistService } from '../../services/travel-checklist.service';
import { TravelConfigService } from '../../services/travel-config.service';

interface EditableItem extends SaveTravelChecklistItem { key: number; }

/** Edits a Draft checklist's items; Active / Retired versions are read-only (create a new version to change). */
@Component({
  selector: 'app-travel-checklist-editor',
  standalone: true,
  imports: [DatePipe, RouterLink, MatButtonModule, MatIconModule, UiPageHeaderComponent, UiStatusBadgeComponent, HasPermissionDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="tn-page">
      @if (error()) {
        <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ error() }}
          <a mat-stroked-button routerLink="/travel/checklists" class="ml-auto">Back</a></div>
      } @else if (!checklist()) {
        <div class="tn-card tn-card-body space-y-3">@for (s of [1,2,3]; track s) { <div class="h-8 animate-pulse rounded bg-fleet-surface-muted"></div> }</div>
      } @else {
        @let c = checklist()!;
        <ui-page-header [title]="c.name" [eyebrow]="c.countryName + ' · ' + (c.visaTypeName || 'All visa types') + ' · v' + c.version" icon="checklist"
          [breadcrumbs]="[{ label: 'TravelNexus' }, { label: 'Document checklists', route: '/travel/checklists' }, { label: c.name + ' v' + c.version }]">
          <ng-container *appHasPermission="perms.settings">
            @if (editable()) {
              <button mat-stroked-button color="warn" [disabled]="busy()" (click)="deleteDraft()"><mat-icon>delete_outline</mat-icon> Delete draft</button>
              <button mat-stroked-button [disabled]="busy() || !dirty()" (click)="save()"><mat-icon>save</mat-icon> Save</button>
              <button mat-flat-button color="primary" [disabled]="busy() || dirty() || !items().length" (click)="activate()"
                [title]="dirty() ? 'Save changes first' : ''"><mat-icon>check_circle</mat-icon> Activate</button>
            } @else {
              <button mat-flat-button color="primary" [disabled]="busy()" (click)="newVersion()"><mat-icon>content_copy</mat-icon> New version</button>
            }
          </ng-container>
        </ui-page-header>

        <div class="tn-card tn-card-body flex flex-wrap items-center gap-3 text-sm">
          <ui-status-badge [status]="c.status" [variant]="statusVariant[c.status]"></ui-status-badge>
          @if (c.status === 'Active') { <span>Used for new visa cases since {{ utc(c.activatedAt) | date: 'mediumDate' }}.</span> }
          @if (c.status === 'Retired') { <span class="text-fleet-text-muted">Replaced by a newer version; cases created with it keep it.</span> }
          @if (c.status === 'Draft') { <span class="text-fleet-text-muted">Draft — not used until activated. Activating retires the current active version.</span> }
        </div>

        <div class="tn-card">
          <div class="flex items-center justify-between border-b border-fleet-border px-5 py-4">
            <h3 class="tn-section-title">Documents ({{ items().length }})</h3>
            @if (editable()) { <button mat-stroked-button (click)="addItem()"><mat-icon>add</mat-icon> Add document</button> }
          </div>
          @if (!items().length) {
            <p class="px-5 py-6 text-sm text-fleet-text-muted">No documents yet.{{ editable() ? ' Add passport, photo, bank statement…' : '' }}</p>
          } @else {
            <div class="overflow-x-auto">
              <table class="w-full min-w-[860px] text-left text-sm">
                <thead class="bg-fleet-surface-alt/60">
                  <tr class="text-[11px] font-bold uppercase tracking-wider text-fleet-text-muted">
                    <th class="px-4 py-3">Document</th><th class="px-4 py-3">Applies to</th><th class="px-4 py-3 text-center">Mandatory</th>
                    <th class="px-4 py-3">Min validity after travel (days)</th><th class="px-4 py-3">Instructions</th><th class="px-4 py-3"></th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-fleet-border">
                  @for (it of items(); track it.key; let i = $index; let last = $last) {
                    <tr>
                      <td class="px-4 py-2">
                        <select class="tn-input" [disabled]="!editable()" [value]="it.documentTypeId || ''" (change)="patch(i, { documentTypeId: +$any($event.target).value })" aria-label="Document type">
                          <option value="">Choose…</option>
                          @for (d of documentTypes(); track d.id) { <option [value]="d.id">{{ d.name }}</option> }
                        </select></td>
                      <td class="px-4 py-2">
                        <select class="tn-input" [disabled]="!editable()" [value]="it.applicantCondition" (change)="patch(i, { applicantCondition: $any($event.target).value })" aria-label="Applies to">
                          @for (cnd of conditions; track cnd) { <option [value]="cnd">{{ conditionLabel[cnd] }}</option> }
                        </select></td>
                      <td class="px-4 py-2 text-center"><input type="checkbox" [disabled]="!editable()" [checked]="it.isMandatory" (change)="patch(i, { isMandatory: $any($event.target).checked })" aria-label="Mandatory" /></td>
                      <td class="px-4 py-2"><input class="tn-input w-28" type="number" min="1" max="3650" [disabled]="!editable()" [value]="it.minValidityDays ?? ''"
                        (input)="patch(i, { minValidityDays: $any($event.target).value ? +$any($event.target).value : null })" aria-label="Minimum validity days" /></td>
                      <td class="px-4 py-2"><input class="tn-input" maxlength="500" [disabled]="!editable()" [value]="it.instructions ?? ''"
                        (input)="patch(i, { instructions: $any($event.target).value || null })" aria-label="Instructions" /></td>
                      <td class="px-4 py-2 whitespace-nowrap text-right">
                        @if (editable()) {
                          <button mat-icon-button [disabled]="i === 0" (click)="move(i, -1)" aria-label="Move up"><mat-icon>arrow_upward</mat-icon></button>
                          <button mat-icon-button [disabled]="last" (click)="move(i, 1)" aria-label="Move down"><mat-icon>arrow_downward</mat-icon></button>
                          <button mat-icon-button (click)="removeItem(i)" aria-label="Remove"><mat-icon>delete_outline</mat-icon></button>
                        }
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }
          @if (validation()) { <div class="tn-card-body"><div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ validation() }}</div></div> }
        </div>
      }
    </div>
  `,
  styleUrl: '../../travel-nexus.scss'
})
export class TravelChecklistEditorComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly api = inject(TravelChecklistService);
  private readonly config = inject(TravelConfigService);
  private readonly toast = inject(UiToastService);
  private readonly confirm = inject(UiConfirmService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly perms = TRAVEL_PERMISSIONS;
  protected readonly statusVariant = CHECKLIST_STATUS_VARIANT;
  protected readonly conditions = TRAVEL_APPLICANT_CONDITIONS;
  protected readonly conditionLabel = APPLICANT_CONDITION_LABEL;
  protected readonly utc = asUtc;

  protected readonly checklist = signal<TravelChecklist | null>(null);
  protected readonly documentTypes = signal<TravelDocumentType[]>([]);
  protected readonly items = signal<EditableItem[]>([]);
  protected readonly dirty = signal(false);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly editable = computed(() => this.checklist()?.status === 'Draft');
  protected readonly validation = computed(() => {
    const list = this.items();
    if (list.some(i => !i.documentTypeId)) return 'Choose a document type for every row.';
    const keys = list.map(i => `${i.documentTypeId}|${i.applicantCondition}`);
    return new Set(keys).size !== keys.length ? 'Each document can appear only once per "applies to" rule.' : null;
  });
  private nextKey = 1;

  constructor() {
    this.route.paramMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(p => this.load(Number(p.get('id'))));
  }

  protected patch(index: number, change: Partial<SaveTravelChecklistItem>): void {
    this.items.update(list => list.map((it, i) => (i === index ? { ...it, ...change } : it)));
    this.dirty.set(true);
  }

  protected addItem(): void {
    this.items.update(list => [...list, { key: this.nextKey++, documentTypeId: 0, isMandatory: true, applicantCondition: 'All' as TravelApplicantCondition, minValidityDays: null, instructions: null }]);
    this.dirty.set(true);
  }

  protected removeItem(index: number): void {
    this.items.update(list => list.filter((_, i) => i !== index));
    this.dirty.set(true);
  }

  protected move(index: number, delta: number): void {
    this.items.update(list => {
      const copy = [...list];
      [copy[index], copy[index + delta]] = [copy[index + delta], copy[index]];
      return copy;
    });
    this.dirty.set(true);
  }

  protected save(): void {
    if (this.validation()) return;
    const c = this.checklist()!;
    this.busy.set(true);
    this.api.saveItems(c.id, this.items().map(({ key: _key, ...rest }) => rest)).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.busy.set(false); this.toast.success('Checklist saved.'); this.load(c.id); },
      error: err => { this.busy.set(false); this.toast.error(apiErrorMessage(err, 'Could not save the checklist.')); }
    });
  }

  protected async activate(): Promise<void> {
    const c = this.checklist()!;
    const ok = await this.confirm.open({
      variant: 'warning', title: 'Activate checklist',
      message: `New visa cases for ${c.countryName} · ${c.visaTypeName || 'all visa types'} will use v${c.version}. The current active version is retired; existing cases keep theirs.`,
      confirmText: 'Activate'
    });
    if (!ok) return;
    this.busy.set(true);
    this.api.activate(c.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.busy.set(false); this.toast.success('Checklist activated.'); this.load(c.id); },
      error: err => { this.busy.set(false); this.toast.error(apiErrorMessage(err, 'Could not activate the checklist.')); }
    });
  }

  protected newVersion(): void {
    const c = this.checklist()!;
    this.busy.set(true);
    this.api.newVersion(c.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: id => { this.busy.set(false); this.toast.success('New draft version created.'); this.router.navigate(['/travel/checklists', id]); },
      error: err => { this.busy.set(false); this.toast.error(apiErrorMessage(err, 'Could not create a new version.')); }
    });
  }

  protected async deleteDraft(): Promise<void> {
    const c = this.checklist()!;
    if (!(await this.confirm.confirmDelete(`Delete the draft "${c.name}" v${c.version}?`))) return;
    this.api.delete(c.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.toast.success('Draft deleted.'); this.router.navigate(['/travel/checklists']); },
      error: err => this.toast.error(apiErrorMessage(err, 'Could not delete the draft.'))
    });
  }

  private load(id: number): void {
    forkJoin({ checklist: this.api.getChecklist(id), types: this.config.getDocumentTypes() })
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: ({ checklist, types }) => {
          this.checklist.set(checklist);
          this.documentTypes.set(types.filter(t => t.isActive || checklist.items.some(i => i.documentTypeId === t.id)));
          this.items.set(checklist.items.map(i => ({
            key: this.nextKey++, documentTypeId: i.documentTypeId, isMandatory: i.isMandatory,
            applicantCondition: i.applicantCondition, minValidityDays: i.minValidityDays, instructions: i.instructions
          })));
          this.dirty.set(false);
        },
        error: err => this.error.set(apiErrorMessage(err, 'Checklist not found.'))
      });
  }
}
