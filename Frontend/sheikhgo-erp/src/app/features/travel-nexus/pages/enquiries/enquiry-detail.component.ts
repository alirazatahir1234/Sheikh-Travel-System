import { CurrencyPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { catchError, of } from 'rxjs';
import { UiDrawerComponent, UiModalComponent, UiPageHeaderComponent, UiStatusBadgeComponent } from '../../../../shared/components/ui';
import { UiToastService } from '../../../../shared/components/ui/toast/ui-toast.service';
import { HasPermissionDirective } from '../../../../shared/directives/has-permission.directive';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';
import { TravelCustomerPickerComponent } from '../../components/customer-picker.component';
import {
  ACTIVITY_ICON, ENQUIRY_STATUS_LABEL, ENQUIRY_STATUS_VARIANT, TRAVEL_ACTIVITY_DIRECTIONS, TRAVEL_PERMISSIONS,
  TRAVEL_USER_ACTIVITY_TYPES, TravelActivityDirection, TravelCustomerKind, TravelCustomerListItem, TravelEnquiry,
  TravelEnquiryAction, TravelLostReason, TravelVisaType
} from '../../models/travel.models';
import { asUtc, fromDateTimeLocal, isDue } from '../../models/travel-date.util';
import { TravelConfigService } from '../../services/travel-config.service';
import { TravelEnquiryService } from '../../services/travel-enquiry.service';

type SimpleAction = Extract<TravelEnquiryAction, 'MarkContacted' | 'MarkQualified' | 'MarkQuoted' | 'Reopen'>;

const SIMPLE_ACTIONS: { action: SimpleAction; label: string; icon: string }[] = [
  { action: 'MarkContacted', label: 'Mark contacted', icon: 'call_made' },
  { action: 'MarkQualified', label: 'Mark qualified', icon: 'verified' },
  { action: 'MarkQuoted', label: 'Mark quoted', icon: 'request_quote' },
  { action: 'Reopen', label: 'Reopen', icon: 'restart_alt' }
];

@Component({
  selector: 'app-travel-enquiry-detail',
  standalone: true,
  imports: [DatePipe, CurrencyPipe, ReactiveFormsModule, RouterLink, MatButtonModule, MatIconModule, UiPageHeaderComponent,
    UiStatusBadgeComponent, UiDrawerComponent, UiModalComponent, HasPermissionDirective, TravelCustomerPickerComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="tn-page">
      @if (error()) {
        <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ error() }}
          <a mat-stroked-button routerLink="/travel/enquiries" class="ml-auto">Back to enquiries</a></div>
      } @else if (!enquiry()) {
        <div class="tn-card tn-card-body space-y-3">@for (s of [1,2,3]; track s) { <div class="h-8 animate-pulse rounded bg-fleet-surface-muted"></div> }</div>
      } @else {
        @let e = enquiry()!;
        <ui-page-header [title]="e.displayName" [eyebrow]="'Enquiry ' + e.enquiryNumber" icon="contact_phone"
          [subtitle]="e.serviceType + (e.destinationCountryName ? ' · ' + e.destinationCountryName : '')"
          [breadcrumbs]="[{ label: 'TravelNexus' }, { label: 'Enquiries', route: '/travel/enquiries' }, { label: e.enquiryNumber }]">
          <ng-container *appHasPermission="perms.enquiryManage">
            @for (a of simpleActions(); track a.action) {
              <button mat-stroked-button [disabled]="busy()" (click)="changeStatus(a.action)"><mat-icon>{{ a.icon }}</mat-icon> {{ a.label }}</button>
            }
            @if (can('MarkLost')) { <button mat-stroked-button color="warn" [disabled]="busy()" (click)="openLost()"><mat-icon>block</mat-icon> Mark lost</button> }
            @if (can('Edit')) { <a mat-stroked-button [routerLink]="['/travel/enquiries', e.id, 'edit']"><mat-icon>edit</mat-icon> Edit</a> }
            @if (can('Convert')) { <button mat-flat-button color="primary" [disabled]="busy()" (click)="openConvert()"><mat-icon>task_alt</mat-icon> Convert</button> }
          </ng-container>
        </ui-page-header>

        <div class="grid gap-5 lg:grid-cols-3">
          <div class="tn-card tn-card-body space-y-4 lg:col-span-1">
            <div class="flex items-center gap-2">
              <ui-status-badge [status]="e.status" [label]="statusLabel[e.status]" [variant]="statusVariant[e.status]"></ui-status-badge>
              @if (e.priority !== 'Normal') { <ui-status-badge [status]="e.priority" [variant]="e.priority === 'High' ? 'error' : 'inactive'"></ui-status-badge> }
            </div>
            <dl class="tn-kv">
              @if (e.customerId) {
                <div><dt>Customer</dt><dd><a class="text-fleet-primary underline" [routerLink]="['/travel/customers', e.customerId]">{{ e.displayName }}</a></dd></div>
              } @else {
                <div><dt>Lead</dt><dd>{{ e.leadName }}</dd></div>
                <div><dt>Phone</dt><dd>{{ e.leadPhone }}</dd></div>
                @if (e.leadEmail) { <div><dt>Email</dt><dd>{{ e.leadEmail }}</dd></div> }
              }
              @if (e.visaTypeName) { <div><dt>Visa type</dt><dd>{{ e.visaTypeName }}</dd></div> }
              <div><dt>Travel dates</dt><dd>{{ e.travelDateFrom ? (e.travelDateFrom | date: 'mediumDate': 'UTC') : '—' }}{{ e.travelDateTo ? ' → ' + (e.travelDateTo | date: 'mediumDate': 'UTC') : '' }}</dd></div>
              <div><dt>Travelers</dt><dd>{{ e.adults }} adult(s){{ e.children ? ', ' + e.children + ' child(ren)' : '' }}{{ e.infants ? ', ' + e.infants + ' infant(s)' : '' }}</dd></div>
              <div><dt>Budget</dt><dd>{{ e.budget !== null ? (e.budget | currency: e.currencyCode ?? '' : 'code' : '1.0-2') : '—' }}</dd></div>
              <div><dt>Agent</dt><dd>{{ e.assignedAgentName || 'Unassigned' }}</dd></div>
              <div><dt>Branch</dt><dd>{{ e.branchName || '—' }}</dd></div>
              <div><dt>Lead source</dt><dd>{{ e.leadSourceName || '—' }}</dd></div>
              <div><dt>Next follow-up</dt><dd [class.text-red-600]="followUpDue()">{{ e.nextFollowUpAt ? (utc(e.nextFollowUpAt) | date: 'medium') : '—' }}</dd></div>
              <div><dt>Created</dt><dd>{{ utc(e.createdAt) | date: 'medium' }}</dd></div>
              @if (e.status === 'Lost') {
                <div><dt>Lost reason</dt><dd>{{ e.lostReasonName }}{{ e.lostNotes ? ' — ' + e.lostNotes : '' }}</dd></div>
              }
              @if (e.convertedVisaCaseId) {
                <div><dt>Visa case</dt><dd><a class="text-fleet-primary underline" [routerLink]="['/travel/visa-cases', e.convertedVisaCaseId]">{{ e.convertedVisaCaseNumber }}</a></dd></div>
              }
            </dl>
            @if (e.notes) { <p class="whitespace-pre-line text-sm text-fleet-text-muted">{{ e.notes }}</p> }
          </div>

          <div class="tn-card lg:col-span-2">
            <div class="flex items-center justify-between border-b border-fleet-border px-5 py-4">
              <h3 class="tn-section-title">Follow-ups &amp; communication</h3>
              <button mat-flat-button color="primary" *appHasPermission="perms.enquiryManage" (click)="openActivity()"><mat-icon>add_comment</mat-icon> Log activity</button>
            </div>
            <div class="px-5">
              @if (!e.activities.length) {
                <p class="py-8 text-center text-sm text-fleet-text-muted">No activity yet. Log the first call or message.</p>
              } @else {
                <ul class="tn-timeline">
                  @for (a of e.activities; track a.id) {
                    <li>
                      <span class="tn-timeline-icon"><mat-icon class="!text-[18px]">{{ activityIcon[a.activityType] }}</mat-icon></span>
                      <div class="min-w-0 flex-1">
                        <div class="flex flex-wrap items-baseline justify-between gap-2">
                          <strong class="text-sm">{{ a.activityType === 'StatusChange' ? 'Status' : a.activityType }}
                            @if (a.activityType !== 'StatusChange') { <span class="text-xs font-normal text-fleet-text-muted">· {{ a.direction }}</span> }</strong>
                          <span class="text-xs text-fleet-text-muted">{{ utc(a.occurredAt) | date: 'medium' }}{{ a.createdByName ? ' · ' + a.createdByName : '' }}</span>
                        </div>
                        <p class="text-sm">{{ a.summary }}</p>
                        @if (a.outcome) { <p class="text-xs text-fleet-text-muted">Outcome: {{ a.outcome }}</p> }
                        @if (a.nextFollowUpAt) { <p class="text-xs text-fleet-text-muted">Next follow-up: {{ utc(a.nextFollowUpAt) | date: 'medium' }}</p> }
                      </div>
                    </li>
                  }
                </ul>
              }
            </div>
          </div>
        </div>

        <!-- Log activity -->
        <ui-drawer [(open)]="activityOpen" title="Log activity" width="460px">
          <form class="space-y-4" [formGroup]="activityForm" (ngSubmit)="saveActivity()" novalidate>
            <div class="tn-grid">
              <div class="tn-field"><label for="a-type">Type</label>
                <select id="a-type" class="tn-input" formControlName="activityType">
                  @for (t of activityTypes; track t) { <option [value]="t">{{ t }}</option> }
                </select></div>
              <div class="tn-field"><label for="a-dir">Direction</label>
                <select id="a-dir" class="tn-input" formControlName="direction">
                  @for (d of directions; track d) { <option [value]="d">{{ d }}</option> }
                </select></div>
            </div>
            <div class="tn-field"><label for="a-sum" class="tn-required">Summary</label>
              <textarea id="a-sum" class="tn-input" formControlName="summary" maxlength="1000" placeholder="What was discussed?"></textarea></div>
            <div class="tn-field"><label for="a-out">Outcome</label><input id="a-out" class="tn-input" formControlName="outcome" maxlength="500" /></div>
            <div class="tn-field"><label for="a-next">Next follow-up</label>
              <input id="a-next" class="tn-input" type="datetime-local" formControlName="nextFollowUpAt" />
              <span class="tn-hint">Leave empty to clear the follow-up.</span></div>
            @if (dialogError()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ dialogError() }}</div> }
            <div class="tn-actions">
              <button mat-stroked-button type="button" (click)="activityOpen.set(false)">Cancel</button>
              <button mat-flat-button color="primary" type="submit" [disabled]="busy() || activityForm.invalid">Save</button>
            </div>
          </form>
        </ui-drawer>

        <!-- Mark lost -->
        <ui-modal [(open)]="lostOpen" title="Mark enquiry as lost" size="sm">
          <div class="space-y-4">
            <div class="tn-field"><label for="l-reason" class="tn-required">Reason</label>
              <select id="l-reason" class="tn-input" [value]="lostReasonId() ?? ''" (change)="lostReasonId.set(+$any($event.target).value || null)">
                <option value="">Choose…</option>
                @for (r of lostReasons(); track r.id) { <option [value]="r.id">{{ r.name }}</option> }
              </select>
              @if (!lostReasons().length) { <span class="tn-hint">Add lost reasons in TravelNexus settings first.</span> }</div>
            <div class="tn-field"><label for="l-notes">Notes</label>
              <textarea id="l-notes" class="tn-input" maxlength="1000" [value]="lostNotes()" (input)="lostNotes.set($any($event.target).value)"></textarea></div>
            @if (dialogError()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ dialogError() }}</div> }
          </div>
          <div modal-footer class="tn-actions">
            <button mat-stroked-button (click)="lostOpen.set(false)">Cancel</button>
            <button mat-flat-button color="warn" [disabled]="busy() || !lostReasonId()" (click)="markLost()">Mark lost</button>
          </div>
        </ui-modal>

        <!-- Convert -->
        <ui-modal [(open)]="convertOpen" title="Convert enquiry" size="md">
          <div class="space-y-4">
            @if (e.customerId) {
              <p class="text-sm">The enquiry is linked to <strong>{{ e.displayName }}</strong>.</p>
            } @else {
              <div class="tn-segment" role="radiogroup" aria-label="Customer">
                <button type="button" role="radio" [attr.aria-checked]="convertMode() === 'new'" [class.active]="convertMode() === 'new'" (click)="convertMode.set('new')">Create customer from lead</button>
                <button type="button" role="radio" [attr.aria-checked]="convertMode() === 'existing'" [class.active]="convertMode() === 'existing'" (click)="convertMode.set('existing')">Link existing customer</button>
              </div>
              @if (convertMode() === 'new') {
                <div class="tn-field"><label for="c-kind">Customer type</label>
                  <select id="c-kind" class="tn-input" [value]="newKind()" (change)="newKind.set($any($event.target).value)">
                    <option value="Individual">Individual</option><option value="Corporate">Corporate</option>
                  </select>
                  <span class="tn-hint">{{ e.leadName }} · {{ e.leadPhone }} will be added to Customers.</span></div>
              } @else {
                <div class="tn-field"><label for="convert-customer">Customer</label>
                  <app-travel-customer-picker inputId="convert-customer" [(selected)]="convertCustomer" /></div>
              }
            }
            <label class="flex items-center gap-2 text-sm">
              <input type="checkbox" [checked]="createCase()" (change)="createCase.set($any($event.target).checked)" /> Open a visa case now
            </label>
            @if (createCase()) {
              <div class="tn-field"><label for="c-vt" class="tn-required">Visa type</label>
                <select id="c-vt" class="tn-input" [value]="convertVisaTypeId() ?? ''" (change)="convertVisaTypeId.set(+$any($event.target).value || null)">
                  <option value="">Choose…</option>
                  @for (v of convertVisaTypes(); track v.id) { <option [value]="v.id">{{ v.countryName }} · {{ v.name }}</option> }
                </select></div>
            }
            @if (dialogError()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ dialogError() }}</div> }
          </div>
          <div modal-footer class="tn-actions">
            <button mat-stroked-button (click)="convertOpen.set(false)">Cancel</button>
            <button mat-flat-button color="primary" [disabled]="busy() || !convertReady()" (click)="convert()">Convert</button>
          </div>
        </ui-modal>
      }
    </div>
  `,
  styleUrl: '../../travel-nexus.scss'
})
export class TravelEnquiryDetailComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly api = inject(TravelEnquiryService);
  private readonly config = inject(TravelConfigService);
  private readonly toast = inject(UiToastService);
  private readonly fb = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly perms = TRAVEL_PERMISSIONS;
  protected readonly statusLabel = ENQUIRY_STATUS_LABEL;
  protected readonly statusVariant = ENQUIRY_STATUS_VARIANT;
  protected readonly activityIcon = ACTIVITY_ICON;
  protected readonly activityTypes = TRAVEL_USER_ACTIVITY_TYPES;
  protected readonly directions = TRAVEL_ACTIVITY_DIRECTIONS;
  protected readonly utc = asUtc;
  private readonly id = Number(this.route.snapshot.paramMap.get('id'));

  protected readonly enquiry = signal<TravelEnquiry | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly dialogError = signal<string | null>(null);

  protected readonly simpleActions = computed(() =>
    SIMPLE_ACTIONS.filter(a => this.enquiry()?.allowedActions.includes(a.action)));
  protected readonly followUpDue = computed(() => {
    const e = this.enquiry();
    return !!e && isDue(e.nextFollowUpAt) && e.status !== 'Won' && e.status !== 'Lost';
  });

  // activity
  protected readonly activityOpen = signal(false);
  protected readonly activityForm = this.fb.group({
    activityType: ['Call' as (typeof TRAVEL_USER_ACTIVITY_TYPES)[number]],
    direction: ['Outbound' as TravelActivityDirection],
    summary: ['', [Validators.required, Validators.maxLength(1000)]],
    outcome: [''],
    nextFollowUpAt: ['']
  });

  // lost
  protected readonly lostOpen = signal(false);
  protected readonly lostReasons = signal<TravelLostReason[]>([]);
  protected readonly lostReasonId = signal<number | null>(null);
  protected readonly lostNotes = signal('');

  // convert
  protected readonly convertOpen = signal(false);
  protected readonly convertMode = signal<'new' | 'existing'>('new');
  protected readonly newKind = signal<TravelCustomerKind>('Individual');
  protected readonly convertCustomer = signal<TravelCustomerListItem | null>(null);
  protected readonly createCase = signal(false);
  protected readonly convertVisaTypeId = signal<number | null>(null);
  private readonly allVisaTypes = toSignal(this.config.getVisaTypes(undefined, true).pipe(catchError(() => of([] as TravelVisaType[]))),
    { initialValue: [] as TravelVisaType[] });
  protected readonly convertVisaTypes = computed(() => {
    const country = this.enquiry()?.destinationCountryCode;
    const all = this.allVisaTypes();
    return country ? all.filter(v => v.countryCode === country) : all;
  });
  protected readonly convertReady = computed(() => {
    const e = this.enquiry();
    if (!e) return false;
    if (!e.customerId && this.convertMode() === 'existing' && !this.convertCustomer()) return false;
    return !this.createCase() || !!this.convertVisaTypeId();
  });

  constructor() { this.load(); }

  protected can(action: TravelEnquiryAction): boolean { return !!this.enquiry()?.allowedActions.includes(action); }

  protected changeStatus(action: SimpleAction): void {
    this.busy.set(true);
    this.api.changeStatus(this.id, action).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: status => { this.busy.set(false); this.toast.success(`Enquiry is now ${status}.`); this.load(); },
      error: err => { this.busy.set(false); this.toast.error(apiErrorMessage(err, 'Could not change the status.')); this.load(); }
    });
  }

  protected openActivity(): void {
    this.dialogError.set(null);
    this.activityForm.reset({ activityType: 'Call', direction: 'Outbound', summary: '', outcome: '', nextFollowUpAt: '' });
    this.activityOpen.set(true);
  }

  protected saveActivity(): void {
    if (this.activityForm.invalid) return;
    const v = this.activityForm.getRawValue();
    this.busy.set(true);
    this.api.logActivity(this.id, {
      activityType: v.activityType!, direction: v.direction!, summary: v.summary!.trim(),
      outcome: v.outcome?.trim() || null, occurredAt: null, nextFollowUpAt: fromDateTimeLocal(v.nextFollowUpAt)
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.busy.set(false); this.activityOpen.set(false); this.toast.success('Activity logged.'); this.load(); },
      error: err => { this.busy.set(false); this.dialogError.set(apiErrorMessage(err, 'Could not log the activity.')); }
    });
  }

  protected openLost(): void {
    this.dialogError.set(null);
    this.lostReasonId.set(null);
    this.lostNotes.set('');
    this.config.getLostReasons(true).pipe(catchError(() => of([]))).subscribe(r => this.lostReasons.set(r));
    this.lostOpen.set(true);
  }

  protected markLost(): void {
    const reason = this.lostReasonId();
    if (!reason) return;
    this.busy.set(true);
    this.api.markLost(this.id, reason, this.lostNotes().trim() || null).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.busy.set(false); this.lostOpen.set(false); this.toast.success('Enquiry marked as lost.'); this.load(); },
      error: err => { this.busy.set(false); this.dialogError.set(apiErrorMessage(err, 'Could not mark the enquiry as lost.')); }
    });
  }

  protected openConvert(): void {
    const e = this.enquiry();
    this.dialogError.set(null);
    this.convertMode.set('new');
    this.convertCustomer.set(null);
    this.createCase.set(e?.serviceType === 'Visa' || e?.serviceType === 'Umrah');
    this.convertVisaTypeId.set(e?.visaTypeId ?? null);
    this.convertOpen.set(true);
  }

  protected convert(): void {
    const e = this.enquiry();
    if (!e || !this.convertReady()) return;
    const existing = !e.customerId && this.convertMode() === 'existing' ? this.convertCustomer()!.customerId : null;
    this.busy.set(true);
    this.api.convert(this.id, {
      existingCustomerId: existing,
      newCustomerKind: !e.customerId && this.convertMode() === 'new' ? this.newKind() : null,
      createVisaCase: this.createCase(),
      visaCountryCode: null,
      visaTypeId: this.createCase() ? this.convertVisaTypeId() : null,
      intendedTravelDate: null
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: r => {
        this.busy.set(false);
        this.convertOpen.set(false);
        this.toast.success(r.visaCaseNumber ? `Converted. Visa case ${r.visaCaseNumber} opened.` : 'Enquiry converted.');
        if (r.visaCaseId) this.router.navigate(['/travel/visa-cases', r.visaCaseId]);
        else this.load();
      },
      error: err => { this.busy.set(false); this.dialogError.set(apiErrorMessage(err, 'Could not convert the enquiry.')); }
    });
  }

  private load(): void {
    this.api.getEnquiry(this.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: e => this.enquiry.set(e),
      error: err => this.error.set(apiErrorMessage(err, 'Enquiry not found.'))
    });
  }
}
