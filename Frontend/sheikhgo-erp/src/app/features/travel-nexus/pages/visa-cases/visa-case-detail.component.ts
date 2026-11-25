import { CurrencyPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { catchError, of } from 'rxjs';
import {
  UiConfirmService, UiDrawerComponent, UiModalComponent, UiPageHeaderComponent, UiStatusBadgeComponent
} from '../../../../shared/components/ui';
import { UiToastService } from '../../../../shared/components/ui/toast/ui-toast.service';
import { HasPermissionDirective } from '../../../../shared/directives/has-permission.directive';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';
import {
  APPLICANT_DECISION_VARIANT, TRAVEL_PERMISSIONS, TravelTraveler, TravelVisaAction, TravelVisaApplicant, TravelVisaCase,
  TravelVisaCaseStatus, TravelVisaTransitionAction, VISA_STATUS_LABEL, VISA_STATUS_VARIANT, VISA_TRANSITION_LABEL
} from '../../models/travel.models';
import { asUtc, fromDateTimeLocal, toDateTimeLocal } from '../../models/travel-date.util';
import { TravelCustomerService } from '../../services/travel-customer.service';
import { VisaCaseDocumentsComponent } from '../../components/visa-case-documents.component';
import { TravelVisaService } from '../../services/travel-visa.service';

/** Transition buttons in display order; Hold and Cancel ask for a note. */
const TRANSITIONS: { action: TravelVisaTransitionAction; icon: string; needsNote: boolean; warn?: boolean }[] = [
  { action: 'StartDocuments', icon: 'description', needsNote: false },
  { action: 'VerifyDocuments', icon: 'fact_check', needsNote: false },
  { action: 'ReopenDocuments', icon: 'undo', needsNote: false },
  { action: 'StartProcessing', icon: 'hourglass_top', needsNote: false },
  { action: 'Resume', icon: 'play_arrow', needsNote: false },
  { action: 'Hold', icon: 'pause', needsNote: true },
  { action: 'Cancel', icon: 'cancel', needsNote: true, warn: true }
];

@Component({
  selector: 'app-travel-visa-case-detail',
  standalone: true,
  imports: [DatePipe, CurrencyPipe, ReactiveFormsModule, RouterLink, MatButtonModule, MatIconModule, UiPageHeaderComponent,
    UiStatusBadgeComponent, UiDrawerComponent, UiModalComponent, HasPermissionDirective, VisaCaseDocumentsComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="tn-page">
      @if (error()) {
        <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ error() }}
          <a mat-stroked-button routerLink="/travel/visa-cases" class="ml-auto">Back to visa cases</a></div>
      } @else if (!visaCase()) {
        <div class="tn-card tn-card-body space-y-3">@for (s of [1,2,3]; track s) { <div class="h-8 animate-pulse rounded bg-fleet-surface-muted"></div> }</div>
      } @else {
        @let c = visaCase()!;
        <ui-page-header [title]="c.countryName + ' · ' + c.visaTypeName" [eyebrow]="'Visa case ' + c.caseNumber" icon="assignment_ind"
          [subtitle]="c.customerName"
          [breadcrumbs]="[{ label: 'TravelNexus' }, { label: 'Visa cases', route: '/travel/visa-cases' }, { label: c.caseNumber }]">
          <ng-container *appHasPermission="perms.visaManage">
            @for (t of transitions(); track t.action) {
              <button mat-stroked-button [color]="t.warn ? 'warn' : undefined"
                [disabled]="busy() || (t.action === 'VerifyDocuments' && documentsOutstanding() > 0)"
                [title]="t.action === 'VerifyDocuments' && documentsOutstanding() > 0 ? documentsOutstanding() + ' mandatory document(s) still need verifying or waiving' : ''"
                (click)="startTransition(t.action)">
                <mat-icon>{{ t.icon }}</mat-icon> {{ transitionLabel[t.action] }}
                @if (t.action === 'VerifyDocuments' && c.documentsMandatoryTotal) {
                  <span class="text-xs">({{ c.documentsMandatoryComplete }}/{{ c.documentsMandatoryTotal }})</span>
                }
              </button>
            }
            @if (can('Submit')) { <button mat-flat-button color="primary" [disabled]="busy()" (click)="openSubmit()"><mat-icon>send</mat-icon> Submit</button> }
            @if (can('Edit')) { <a mat-stroked-button [routerLink]="['/travel/visa-cases', c.id, 'edit']"><mat-icon>edit</mat-icon> Edit</a> }
          </ng-container>
        </ui-page-header>

        <div class="grid gap-5 lg:grid-cols-3">
          <div class="tn-card tn-card-body space-y-4">
            <div class="flex items-center gap-2">
              <ui-status-badge [status]="c.status" [label]="statusLabel[c.status]" [variant]="statusVariant[c.status]"></ui-status-badge>
              @if (c.status === 'OnHold' && c.onHoldFromStatus) { <span class="text-xs text-fleet-text-muted">from {{ statusLabel[c.onHoldFromStatus] }}</span> }
            </div>
            <dl class="tn-kv">
              <div><dt>Customer</dt><dd><a class="text-fleet-primary underline" [routerLink]="['/travel/customers', c.customerId]">{{ c.customerName }}</a></dd></div>
              @if (c.enquiryId) { <div><dt>Enquiry</dt><dd><a class="text-fleet-primary underline" [routerLink]="['/travel/enquiries', c.enquiryId]">{{ c.enquiryNumber }}</a></dd></div> }
              <div><dt>Travel date</dt><dd>{{ c.intendedTravelDate ? (c.intendedTravelDate | date: 'mediumDate': 'UTC') : '—' }}</dd></div>
              <div><dt>Appointment</dt><dd>{{ c.appointmentAt ? (utc(c.appointmentAt) | date: 'medium') : '—' }}</dd></div>
              <div><dt>Submitted</dt><dd>{{ c.submittedAt ? (utc(c.submittedAt) | date: 'medium') : '—' }}{{ c.submissionReference ? ' · ' + c.submissionReference : '' }}</dd></div>
              <div><dt>Expected decision</dt><dd>{{ c.expectedDecisionDate ? (c.expectedDecisionDate | date: 'mediumDate': 'UTC') : '—' }}</dd></div>
              @if (c.decidedAt) { <div><dt>Decided</dt><dd>{{ utc(c.decidedAt) | date: 'medium' }}</dd></div> }
              <div><dt>Quoted fees</dt><dd>
                @if (c.currencyCode) {
                  {{ (c.quotedServiceFee ?? 0) | currency: c.currencyCode : 'code' : '1.0-2' }} service
                  + {{ (c.quotedEmbassyFee ?? 0) | currency: c.currencyCode : 'code' : '1.0-2' }} embassy
                } @else { — }
              </dd></div>
              <div><dt>Agent</dt><dd>{{ c.assignedAgentName || 'Unassigned' }}</dd></div>
              <div><dt>Branch</dt><dd>{{ c.branchName || '—' }}</dd></div>
              @if (c.cancelReason) { <div><dt>Cancel reason</dt><dd>{{ c.cancelReason }}</dd></div> }
            </dl>
            @if (c.notes) { <p class="whitespace-pre-line text-sm text-fleet-text-muted">{{ c.notes }}</p> }
          </div>

          <div class="space-y-5 lg:col-span-2">
            <div class="tn-card">
              <div class="flex items-center justify-between border-b border-fleet-border px-5 py-4">
                <div>
                  <h3 class="tn-section-title">Applicants ({{ c.applicants.length }})</h3>
                  @if (can('RecordDecision')) { <p class="text-xs text-fleet-text-muted">Record each embassy decision; the case closes when all are decided.</p> }
                </div>
                @if (can('ManageApplicants')) {
                  <button mat-flat-button color="primary" *appHasPermission="perms.visaManage" (click)="openAddApplicants()"><mat-icon>person_add</mat-icon> Add applicants</button>
                }
              </div>
              @if (!c.applicants.length) {
                <p class="py-8 text-center text-sm text-fleet-text-muted">No applicants yet. Add the customer's travelers.</p>
              } @else {
                <div class="overflow-x-auto">
                  <table class="w-full min-w-[720px] text-left text-sm">
                    <thead class="bg-fleet-surface-alt/60">
                      <tr class="text-[11px] font-bold uppercase tracking-wider text-fleet-text-muted">
                        <th class="px-5 py-3">Applicant</th><th class="px-5 py-3">Passport</th><th class="px-5 py-3">Decision</th>
                        <th class="px-5 py-3">Visa</th><th class="px-5 py-3 text-right"></th>
                      </tr>
                    </thead>
                    <tbody class="divide-y divide-fleet-border">
                      @for (a of c.applicants; track a.id) {
                        <tr>
                          <td class="px-5 py-3"><div class="font-semibold">{{ a.travelerName }}</div>
                            <div class="text-xs text-fleet-text-muted">{{ a.gender }}{{ a.dateOfBirth ? ' · ' + (a.dateOfBirth | date: 'mediumDate': 'UTC') : '' }}</div></td>
                          <td class="px-5 py-3"><span class="font-mono">{{ a.passportNumber || '—' }}</span>
                            @if (a.passportWarning) {
                              <div class="flex items-center gap-1 text-xs text-amber-700"><mat-icon class="!text-[14px]">warning</mat-icon>
                                {{ a.passportNumber ? 'Less than 6 months validity after travel' : 'No passport on file' }}</div>
                            } @else if (a.passportExpiryDate) {
                              <div class="text-xs text-fleet-text-muted">exp. {{ a.passportExpiryDate | date: 'mediumDate': 'UTC' }}</div>
                            }</td>
                          <td class="px-5 py-3"><ui-status-badge [status]="a.decision" [variant]="decisionVariant[a.decision]"></ui-status-badge>
                            @if (a.rejectionReason) { <div class="text-xs text-fleet-text-muted">{{ a.rejectionReason }}</div> }</td>
                          <td class="px-5 py-3">@if (a.visaNumber) { <span class="font-mono">{{ a.visaNumber }}</span> }
                            @if (a.visaExpiryDate) { <div class="text-xs text-fleet-text-muted">valid to {{ a.visaExpiryDate | date: 'mediumDate': 'UTC' }}</div> }
                            @if (!a.visaNumber && !a.visaExpiryDate) { — }</td>
                          <td class="px-5 py-3 text-right whitespace-nowrap">
                            @if (can('RecordDecision')) {
                              <button mat-stroked-button *appHasPermission="perms.visaDecide" (click)="openDecision(a)">Record decision</button>
                            }
                            @if (can('ManageApplicants')) {
                              <button mat-icon-button *appHasPermission="perms.visaManage" aria-label="Remove applicant" (click)="removeApplicant(a)"><mat-icon>delete_outline</mat-icon></button>
                            }
                          </td>
                        </tr>
                      }
                    </tbody>
                  </table>
                </div>
              }
            </div>

            <app-visa-case-documents [caseId]="c.id" [caseStatus]="c.status" [applicants]="c.applicants" (changed)="load()" />

            <div class="tn-card">
              <div class="border-b border-fleet-border px-5 py-4"><h3 class="tn-section-title">History</h3></div>
              <ul class="tn-timeline px-5">
                @for (h of c.history; track h.id) {
                  <li>
                    <span class="tn-timeline-icon"><mat-icon class="!text-[18px]">{{ historyIcon(h.eventType) }}</mat-icon></span>
                    <div class="min-w-0 flex-1">
                      <div class="flex flex-wrap items-baseline justify-between gap-2">
                        <strong class="text-sm">{{ historyTitle(h.eventType, h.fromValue, h.toValue, h.applicantName) }}</strong>
                        <span class="text-xs text-fleet-text-muted">{{ utc(h.changedAt) | date: 'medium' }}{{ h.changedByName ? ' · ' + h.changedByName : '' }}</span>
                      </div>
                      @if (h.note) { <p class="text-sm text-fleet-text-muted">{{ h.note }}</p> }
                    </div>
                  </li>
                }
              </ul>
            </div>
          </div>
        </div>

        <!-- Transition note (Hold / Cancel) -->
        <ui-modal [(open)]="noteOpen" [title]="pendingAction() ? transitionLabel[pendingAction()!] : ''" size="sm">
          <div class="tn-field"><label for="t-note" [class.tn-required]="pendingAction() === 'Cancel'">{{ pendingAction() === 'Cancel' ? 'Reason' : 'Note' }}</label>
            <textarea id="t-note" class="tn-input" maxlength="500" [value]="note()" (input)="note.set($any($event.target).value)"></textarea></div>
          @if (dialogError()) { <div class="tn-banner error mt-3"><mat-icon>error_outline</mat-icon>{{ dialogError() }}</div> }
          <div modal-footer class="tn-actions">
            <button mat-stroked-button (click)="noteOpen.set(false)">Back</button>
            <button mat-flat-button [color]="pendingAction() === 'Cancel' ? 'warn' : 'primary'"
              [disabled]="busy() || (pendingAction() === 'Cancel' && !note().trim())" (click)="runTransition(pendingAction()!, note())">Confirm</button>
          </div>
        </ui-modal>

        <!-- Submit -->
        <ui-modal [(open)]="submitOpen" title="Submit to embassy / portal" size="sm">
          <form class="space-y-4" [formGroup]="submitForm" novalidate>
            <div class="tn-field"><label for="s-at" class="tn-required">Submitted at</label>
              <input id="s-at" class="tn-input" type="datetime-local" formControlName="submittedAt" /></div>
            <div class="tn-field"><label for="s-ref">Reference / tracking no.</label>
              <input id="s-ref" class="tn-input" formControlName="submissionReference" maxlength="100" /></div>
            <div class="tn-field"><label for="s-exp">Expected decision</label>
              <input id="s-exp" class="tn-input" type="date" formControlName="expectedDecisionDate" /></div>
            @if (dialogError()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ dialogError() }}</div> }
          </form>
          <div modal-footer class="tn-actions">
            <button mat-stroked-button (click)="submitOpen.set(false)">Cancel</button>
            <button mat-flat-button color="primary" [disabled]="busy() || submitForm.invalid" (click)="submit()">Submit</button>
          </div>
        </ui-modal>

        <!-- Add applicants -->
        <ui-modal [(open)]="addOpen" title="Add applicants" size="md">
          @if (candidates() === null) {
            <p class="text-sm text-fleet-text-muted">Loading travelers…</p>
          } @else if (!candidates()!.length) {
            <p class="text-sm">All of this customer's travelers are already applicants.
              <a class="text-fleet-primary underline" [routerLink]="['/travel/customers', c.customerId]">Add travelers to the customer</a> first.</p>
          } @else {
            <ul class="divide-y divide-fleet-border">
              @for (t of candidates()!; track t.id) {
                <li class="flex items-center gap-3 py-2">
                  <input type="checkbox" [id]="'cand-' + t.id" [checked]="selected().has(t.id)" (change)="toggle(t.id)" />
                  <label [for]="'cand-' + t.id" class="flex-1 text-sm"><strong>{{ t.fullName }}</strong>
                    <span class="text-fleet-text-muted"> · {{ t.gender }} · passport {{ t.passportNumber || 'missing' }}</span></label>
                </li>
              }
            </ul>
          }
          @if (dialogError()) { <div class="tn-banner error mt-3"><mat-icon>error_outline</mat-icon>{{ dialogError() }}</div> }
          <div modal-footer class="tn-actions">
            <button mat-stroked-button (click)="addOpen.set(false)">Cancel</button>
            <button mat-flat-button color="primary" [disabled]="busy() || !selected().size" (click)="addApplicants()">Add {{ selected().size || '' }}</button>
          </div>
        </ui-modal>

        <!-- Decision -->
        <ui-drawer [(open)]="decisionOpen" [title]="'Decision · ' + (deciding()?.travelerName ?? '')" width="440px">
          <form class="space-y-4" [formGroup]="decisionForm" (ngSubmit)="recordDecision()" novalidate>
            <div class="tn-segment" role="radiogroup" aria-label="Decision">
              @for (d of ['Approved', 'Rejected', 'Withdrawn']; track d) {
                <button type="button" role="radio" [attr.aria-checked]="decisionForm.controls.decision.value === d"
                  [class.active]="decisionForm.controls.decision.value === d" (click)="decisionForm.controls.decision.setValue($any(d))">{{ d }}</button>
              }
            </div>
            @if (decisionForm.controls.decision.value === 'Approved') {
              <div class="tn-grid">
                <div class="tn-field"><label for="d-no">Visa number</label><input id="d-no" class="tn-input uppercase" formControlName="visaNumber" maxlength="50" /></div>
                <div class="tn-field"><label for="d-iss">Issued</label><input id="d-iss" class="tn-input" type="date" formControlName="visaIssueDate" /></div>
                <div class="tn-field"><label for="d-from">Valid from</label><input id="d-from" class="tn-input" type="date" formControlName="visaValidFrom" /></div>
                <div class="tn-field"><label for="d-to">Valid to</label><input id="d-to" class="tn-input" type="date" formControlName="visaExpiryDate" />
                  @if (decisionForm.hasError('validity')) { <span class="tn-error">Must be after "valid from".</span> }</div>
              </div>
            }
            @if (decisionForm.controls.decision.value === 'Rejected') {
              <div class="tn-field"><label for="d-rej" class="tn-required">Rejection reason</label>
                <textarea id="d-rej" class="tn-input" formControlName="rejectionReason" maxlength="500"></textarea></div>
            }
            <div class="tn-field"><label for="d-notes">Notes</label><textarea id="d-notes" class="tn-input" formControlName="notes" maxlength="1000"></textarea></div>
            @if (dialogError()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ dialogError() }}</div> }
            <div class="tn-actions">
              <button mat-stroked-button type="button" (click)="decisionOpen.set(false)">Cancel</button>
              <button mat-flat-button color="primary" type="submit" [disabled]="busy() || decisionForm.invalid">Save decision</button>
            </div>
          </form>
        </ui-drawer>
      }
    </div>
  `,
  styleUrl: '../../travel-nexus.scss'
})
export class TravelVisaCaseDetailComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(TravelVisaService);
  private readonly customers = inject(TravelCustomerService);
  private readonly confirm = inject(UiConfirmService);
  private readonly toast = inject(UiToastService);
  private readonly fb = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly perms = TRAVEL_PERMISSIONS;
  protected readonly statusLabel = VISA_STATUS_LABEL;
  protected readonly statusVariant = VISA_STATUS_VARIANT;
  protected readonly decisionVariant = APPLICANT_DECISION_VARIANT;
  protected readonly transitionLabel = VISA_TRANSITION_LABEL;
  protected readonly utc = asUtc;
  private readonly id = Number(this.route.snapshot.paramMap.get('id'));

  protected readonly visaCase = signal<TravelVisaCase | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly dialogError = signal<string | null>(null);
  protected readonly transitions = computed(() => TRANSITIONS.filter(t => this.can(t.action)));
  protected readonly documentsOutstanding = computed(() => {
    const c = this.visaCase();
    return c ? c.documentsMandatoryTotal - c.documentsMandatoryComplete : 0;
  });

  protected readonly noteOpen = signal(false);
  protected readonly pendingAction = signal<TravelVisaTransitionAction | null>(null);
  protected readonly note = signal('');

  protected readonly submitOpen = signal(false);
  protected readonly submitForm = this.fb.group({
    submittedAt: ['', Validators.required],
    submissionReference: [''],
    expectedDecisionDate: ['']
  });

  protected readonly addOpen = signal(false);
  protected readonly candidates = signal<TravelTraveler[] | null>(null);
  protected readonly selected = signal(new Set<number>());

  protected readonly decisionOpen = signal(false);
  protected readonly deciding = signal<TravelVisaApplicant | null>(null);
  protected readonly decisionForm = this.fb.group({
    decision: ['Approved' as 'Approved' | 'Rejected' | 'Withdrawn'],
    visaNumber: [''],
    visaIssueDate: [''],
    visaValidFrom: [''],
    visaExpiryDate: [''],
    rejectionReason: [''],
    notes: ['']
  }, {
    validators: g => {
      const v = g.getRawValue();
      const errors: Record<string, boolean> = {};
      if (v.decision === 'Rejected' && !v.rejectionReason?.trim()) errors['reason'] = true;
      if (v.decision === 'Approved' && v.visaValidFrom && v.visaExpiryDate && v.visaExpiryDate <= v.visaValidFrom) errors['validity'] = true;
      return Object.keys(errors).length ? errors : null;
    }
  });

  constructor() { this.load(); }

  protected can(action: TravelVisaAction): boolean { return !!this.visaCase()?.allowedActions.includes(action); }

  protected startTransition(action: TravelVisaTransitionAction): void {
    const t = TRANSITIONS.find(x => x.action === action)!;
    if (t.needsNote) {
      this.pendingAction.set(action);
      this.note.set('');
      this.dialogError.set(null);
      this.noteOpen.set(true);
    } else {
      this.runTransition(action, '');
    }
  }

  protected runTransition(action: TravelVisaTransitionAction, note: string): void {
    this.busy.set(true);
    this.dialogError.set(null);
    this.api.transition(this.id, action, note.trim() || null).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: status => { this.busy.set(false); this.noteOpen.set(false); this.toast.success(`Case is now ${VISA_STATUS_LABEL[status as TravelVisaCaseStatus] ?? status}.`); this.load(); },
      error: err => {
        this.busy.set(false);
        const msg = apiErrorMessage(err, 'Could not update the case.');
        if (this.noteOpen()) this.dialogError.set(msg); else this.toast.error(msg);
        this.load();
      }
    });
  }

  protected openSubmit(): void {
    this.dialogError.set(null);
    this.submitForm.reset({ submittedAt: toDateTimeLocal(new Date().toISOString()), submissionReference: '', expectedDecisionDate: '' });
    this.submitOpen.set(true);
  }

  protected submit(): void {
    const v = this.submitForm.getRawValue();
    this.busy.set(true);
    this.api.submit(this.id, {
      submittedAt: fromDateTimeLocal(v.submittedAt)!,
      submissionReference: v.submissionReference?.trim() || null,
      expectedDecisionDate: v.expectedDecisionDate || null,
      note: null
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.busy.set(false); this.submitOpen.set(false); this.toast.success('Case submitted.'); this.load(); },
      error: err => { this.busy.set(false); this.dialogError.set(apiErrorMessage(err, 'Could not submit the case.')); }
    });
  }

  protected openAddApplicants(): void {
    const c = this.visaCase();
    if (!c) return;
    this.dialogError.set(null);
    this.selected.set(new Set());
    this.candidates.set(null);
    this.addOpen.set(true);
    const existing = new Set(c.applicants.map(a => a.travelerId));
    this.customers.getTravelers(c.customerId).pipe(catchError(() => of([] as TravelTraveler[])), takeUntilDestroyed(this.destroyRef))
      .subscribe(list => this.candidates.set(list.filter(t => !existing.has(t.id))));
  }

  protected toggle(id: number): void {
    const next = new Set(this.selected());
    if (next.has(id)) next.delete(id); else next.add(id);
    this.selected.set(next);
  }

  protected addApplicants(): void {
    this.busy.set(true);
    this.api.addApplicants(this.id, [...this.selected()]).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: n => { this.busy.set(false); this.addOpen.set(false); this.toast.success(`${n} applicant${n === 1 ? '' : 's'} added.`); this.load(); },
      error: err => { this.busy.set(false); this.dialogError.set(apiErrorMessage(err, 'Could not add applicants.')); }
    });
  }

  protected async removeApplicant(a: TravelVisaApplicant): Promise<void> {
    if (!(await this.confirm.confirmDelete(`Remove ${a.travelerName} from this case?`, 'Remove applicant'))) return;
    this.api.removeApplicant(this.id, a.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.toast.success('Applicant removed.'); this.load(); },
      error: err => this.toast.error(apiErrorMessage(err, 'Could not remove the applicant.'))
    });
  }

  protected openDecision(a: TravelVisaApplicant): void {
    this.dialogError.set(null);
    this.deciding.set(a);
    this.decisionForm.reset({ decision: 'Approved', visaNumber: '', visaIssueDate: '', visaValidFrom: '', visaExpiryDate: '', rejectionReason: '', notes: '' });
    this.decisionOpen.set(true);
  }

  protected recordDecision(): void {
    const a = this.deciding();
    if (!a || this.decisionForm.invalid) return;
    const v = this.decisionForm.getRawValue();
    const approved = v.decision === 'Approved';
    this.busy.set(true);
    this.api.recordDecision(this.id, a.id, {
      decision: v.decision!,
      visaNumber: approved ? v.visaNumber?.trim() || null : null,
      visaIssueDate: approved ? v.visaIssueDate || null : null,
      visaValidFrom: approved ? v.visaValidFrom || null : null,
      visaExpiryDate: approved ? v.visaExpiryDate || null : null,
      rejectionReason: v.decision === 'Rejected' ? v.rejectionReason!.trim() : null,
      notes: v.notes?.trim() || null
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: r => {
        this.busy.set(false);
        this.decisionOpen.set(false);
        this.toast.success(r.newCaseStatus ? `Decision saved. Case is now ${VISA_STATUS_LABEL[r.newCaseStatus]}.` : 'Decision saved.');
        this.load();
      },
      error: err => { this.busy.set(false); this.dialogError.set(apiErrorMessage(err, 'Could not save the decision.')); }
    });
  }

  protected historyIcon(type: string): string {
    if (type.startsWith('Document')) return 'description';
    return type === 'ApplicantAdded' ? 'person_add' : type === 'ApplicantRemoved' ? 'person_remove' : type === 'ApplicantDecision' ? 'gavel' : 'swap_horiz';
  }

  protected historyTitle(type: string, from: string | null, to: string | null, applicant: string | null): string {
    const label = (s: string | null) => (s && s in VISA_STATUS_LABEL ? VISA_STATUS_LABEL[s as TravelVisaCaseStatus] : s ?? '');
    switch (type) {
      case 'ApplicantAdded': return `${applicant ?? 'Applicant'} added`;
      case 'ApplicantRemoved': return `${applicant ?? 'Applicant'} removed`;
      case 'ApplicantDecision': return `${applicant ?? 'Applicant'}: ${to}`;
      case 'DocumentUploaded': return `${applicant ?? 'Applicant'}: document uploaded`;
      case 'DocumentVerified': return `${applicant ?? 'Applicant'}: document verified`;
      case 'DocumentRejected': return `${applicant ?? 'Applicant'}: document rejected`;
      case 'DocumentWaived': return `${applicant ?? 'Applicant'}: document waived`;
      case 'DocumentRequested': return `${applicant ?? 'Applicant'}: document requested`;
      case 'DocumentRemoved': return `${applicant ?? 'Applicant'}: requirement removed`;
      default: return from ? `${label(from)} → ${label(to)}` : label(to);
    }
  }

  protected load(): void {
    this.api.getCase(this.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: c => this.visaCase.set(c),
      error: err => this.error.set(apiErrorMessage(err, 'Visa case not found.'))
    });
  }
}
