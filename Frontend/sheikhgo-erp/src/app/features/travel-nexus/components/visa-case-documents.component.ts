import { DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, effect, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { HttpEventType } from '@angular/common/http';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { catchError, of } from 'rxjs';
import { UiConfirmService, UiModalComponent, UiStatusBadgeComponent } from '../../../shared/components/ui';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { HasPermissionDirective } from '../../../shared/directives/has-permission.directive';
import { apiErrorMessage } from '../../../core/utils/api-error.util';
import {
  DOCUMENT_STATUS_VARIANT, TRAVEL_PERMISSIONS, TravelApplicantDocument, TravelCaseDocuments, TravelDocumentFile, TravelDocumentType,
  TravelVisaApplicant
} from '../models/travel.models';
import { asUtc } from '../models/travel-date.util';
import { TravelConfigService } from '../services/travel-config.service';
import { TravelDocumentService } from '../services/travel-document.service';
import { DocumentUploadComponent } from './document-upload.component';
import { DocumentViewerComponent } from './document-viewer.component';

type ReviewKind = 'verify' | 'reject' | 'waive';

/** Documents section of a visa case: requirements per applicant, upload, view, verify / reject / waive. */
@Component({
  selector: 'app-visa-case-documents',
  standalone: true,
  imports: [DatePipe, DecimalPipe, MatButtonModule, MatIconModule, UiModalComponent, UiStatusBadgeComponent, HasPermissionDirective,
    DocumentUploadComponent, DocumentViewerComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="tn-card">
      <div class="flex flex-wrap items-center justify-between gap-3 border-b border-fleet-border px-5 py-4">
        <div>
          <h3 class="tn-section-title">Documents</h3>
          @if (docs(); as d) {
            <p class="text-xs text-fleet-text-muted">
              {{ d.checklistName ? d.checklistName + ' · v' + d.checklistVersion : 'No checklist (ad-hoc requirements only)' }}
              · {{ d.mandatoryComplete }} of {{ d.mandatoryTotal }} mandatory complete
            </p>
          }
        </div>
        @if (docs()?.canEdit) {
          <button mat-stroked-button *appHasPermission="perms.documentManage" (click)="openRequest()"><mat-icon>note_add</mat-icon> Request document</button>
        }
      </div>

      @if (error()) {
        <div class="tn-card-body"><div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ error() }}
          <button mat-stroked-button class="ml-auto" (click)="load()">Retry</button></div></div>
      } @else if (!docs()) {
        <div class="tn-card-body space-y-2">@for (s of [1,2]; track s) { <div class="h-8 animate-pulse rounded bg-fleet-surface-muted"></div> }</div>
      } @else if (!docs()!.requirements.length) {
        <p class="px-5 py-6 text-sm text-fleet-text-muted">
          {{ caseStatus() === 'Draft' ? 'Documents are listed once you request them (\"Request documents\" on the case).' : 'No document requirements on this case.' }}
        </p>
      } @else {
        @if (!docs()!.canEdit) {
          <p class="border-b border-fleet-border bg-fleet-surface-alt/50 px-5 py-2 text-xs text-fleet-text-muted">
            Read-only: documents can be changed while the case is collecting documents.</p>
        }
        @for (group of groups(); track group.applicantId) {
          <div class="border-b border-fleet-border last:border-0">
            <div class="bg-fleet-surface-alt/60 px-5 py-2 text-sm font-semibold">{{ group.name }}</div>
            <ul class="divide-y divide-fleet-border">
              @for (r of group.items; track r.id) {
                <li class="flex flex-col gap-3 px-5 py-3 lg:flex-row lg:items-center">
                  <div class="min-w-0 flex-1">
                    <div class="flex flex-wrap items-center gap-2 text-sm">
                      <strong>{{ r.documentTypeName }}</strong>
                      @if (!r.isMandatory) { <span class="text-xs text-fleet-text-muted">(optional)</span> }
                      @if (r.isAdHoc) { <span class="text-xs text-fleet-text-muted">· requested</span> }
                      <ui-status-badge [status]="r.status" [variant]="statusVariant[r.status]"></ui-status-badge>
                    </div>
                    @if (r.instructions) { <p class="text-xs text-fleet-text-muted">{{ r.instructions }}</p> }
                    @if (r.minValidityDays) { <p class="text-xs text-fleet-text-muted">Must be valid {{ r.minValidityDays }} days after travel.</p> }
                    @if (r.reviewNote) { <p class="text-xs" [class.text-red-600]="r.status === 'Rejected'">{{ r.status }}: {{ r.reviewNote }}</p> }
                    @if (r.expiryDate) { <p class="text-xs text-fleet-text-muted">Expires {{ r.expiryDate | date: 'mediumDate': 'UTC' }}</p> }
                    @for (f of r.files; track f.id; let first = $first) {
                      <button type="button" class="mt-1 mr-3 inline-flex items-center gap-1 text-xs text-fleet-primary underline"
                        *appHasPermission="perms.documentDownload" (click)="view(f)">
                        <mat-icon class="!text-[14px]">{{ f.contentType === 'application/pdf' ? 'picture_as_pdf' : 'image' }}</mat-icon>
                        {{ first ? 'Current' : 'Earlier' }}: {{ f.originalFileName }} · {{ f.sizeBytes / 1024 | number: '1.0-0' }} KB · {{ utc(f.uploadedAt) | date: 'short' }}
                      </button>
                    }
                  </div>
                  @if (docs()!.canEdit) {
                    <div class="flex flex-wrap items-center gap-2">
                      <app-document-upload *appHasPermission="perms.documentManage" [progress]="uploading() === r.id ? progress() : null"
                        [disabled]="uploading() !== null" [label]="r.files.length ? 'Replace file' : 'Upload'" (fileSelected)="upload(r, $event)" />
                      <ng-container *appHasPermission="perms.documentVerify">
                        @if (r.status === 'Uploaded') { <button mat-stroked-button (click)="openReview(r, 'verify')">Verify</button> }
                        @if (r.status === 'Uploaded' || r.status === 'Verified') { <button mat-stroked-button color="warn" (click)="openReview(r, 'reject')">Reject</button> }
                        @if (r.status === 'Missing' || r.status === 'Uploaded' || r.status === 'Rejected') { <button mat-button (click)="openReview(r, 'waive')">Waive</button> }
                      </ng-container>
                      @if (r.isAdHoc) {
                        <button mat-icon-button *appHasPermission="perms.documentManage" aria-label="Remove requirement" (click)="remove(r)"><mat-icon>delete_outline</mat-icon></button>
                      }
                    </div>
                  }
                </li>
              }
            </ul>
          </div>
        }
      }
    </div>

    <app-document-viewer [(open)]="viewerOpen" [file]="viewing()" />

    <ui-modal [(open)]="reviewOpen" [title]="reviewTitle()" size="sm">
      <div class="space-y-4">
        @if (reviewKind() === 'verify') {
          <div class="tn-field"><label for="rv-exp" [class.tn-required]="!!reviewing()?.minValidityDays">Document expiry date</label>
            <input id="rv-exp" class="tn-input" type="date" [value]="reviewExpiry()" (input)="reviewExpiry.set($any($event.target).value)" />
            @if (reviewing()?.minValidityDays) { <span class="tn-hint">Must be at least {{ reviewing()!.minValidityDays }} days after the travel date.</span> }</div>
        }
        <div class="tn-field"><label for="rv-note" [class.tn-required]="reviewKind() !== 'verify'">{{ reviewKind() === 'verify' ? 'Note' : 'Reason' }}</label>
          <textarea id="rv-note" class="tn-input" maxlength="500" [value]="reviewNote()" (input)="reviewNote.set($any($event.target).value)"></textarea></div>
        @if (dialogError()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ dialogError() }}</div> }
      </div>
      <div modal-footer class="tn-actions">
        <button mat-stroked-button (click)="reviewOpen.set(false)">Cancel</button>
        <button mat-flat-button [color]="reviewKind() === 'reject' ? 'warn' : 'primary'"
          [disabled]="busy() || (reviewKind() !== 'verify' && !reviewNote().trim())" (click)="submitReview()">Confirm</button>
      </div>
    </ui-modal>

    <ui-modal [(open)]="requestOpen" title="Request a document" size="sm">
      <div class="space-y-4">
        <div class="tn-field"><label for="rq-app" class="tn-required">Applicant</label>
          <select id="rq-app" class="tn-input" [value]="requestApplicant() ?? ''" (change)="requestApplicant.set(+$any($event.target).value || null)">
            <option value="">Choose…</option>
            @for (a of applicants(); track a.id) { <option [value]="a.id">{{ a.travelerName }}</option> }
          </select></div>
        <div class="tn-field"><label for="rq-type" class="tn-required">Document</label>
          <select id="rq-type" class="tn-input" [value]="requestType() ?? ''" (change)="requestType.set(+$any($event.target).value || null)">
            <option value="">Choose…</option>
            @for (t of documentTypes(); track t.id) { <option [value]="t.id">{{ t.name }}</option> }
          </select></div>
        <label class="flex items-center gap-2 text-sm"><input type="checkbox" [checked]="requestMandatory()" (change)="requestMandatory.set($any($event.target).checked)" /> Mandatory</label>
        <div class="tn-field"><label for="rq-ins">Instructions</label>
          <textarea id="rq-ins" class="tn-input" maxlength="500" [value]="requestInstructions()" (input)="requestInstructions.set($any($event.target).value)"></textarea></div>
        @if (dialogError()) { <div class="tn-banner error"><mat-icon>error_outline</mat-icon>{{ dialogError() }}</div> }
      </div>
      <div modal-footer class="tn-actions">
        <button mat-stroked-button (click)="requestOpen.set(false)">Cancel</button>
        <button mat-flat-button color="primary" [disabled]="busy() || !requestApplicant() || !requestType()" (click)="submitRequest()">Request</button>
      </div>
    </ui-modal>
  `,
  styleUrl: '../travel-nexus.scss'
})
export class VisaCaseDocumentsComponent {
  private readonly api = inject(TravelDocumentService);
  private readonly config = inject(TravelConfigService);
  private readonly toast = inject(UiToastService);
  private readonly confirm = inject(UiConfirmService);
  private readonly destroyRef = inject(DestroyRef);

  readonly caseId = input.required<number>();
  readonly caseStatus = input.required<string>();
  readonly applicants = input<TravelVisaApplicant[]>([]);
  /** Emitted after any change so the case header (progress, allowed actions) refreshes. */
  readonly changed = output<void>();

  protected readonly perms = TRAVEL_PERMISSIONS;
  protected readonly statusVariant = DOCUMENT_STATUS_VARIANT;
  protected readonly utc = asUtc;

  protected readonly docs = signal<TravelCaseDocuments | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly dialogError = signal<string | null>(null);
  protected readonly uploading = signal<number | null>(null);
  protected readonly progress = signal<number | null>(null);

  protected readonly viewerOpen = signal(false);
  protected readonly viewing = signal<TravelDocumentFile | null>(null);

  protected readonly reviewOpen = signal(false);
  protected readonly reviewKind = signal<ReviewKind>('verify');
  protected readonly reviewing = signal<TravelApplicantDocument | null>(null);
  protected readonly reviewNote = signal('');
  protected readonly reviewExpiry = signal('');
  protected readonly reviewTitle = computed(() => {
    const r = this.reviewing();
    const verb = { verify: 'Verify', reject: 'Reject', waive: 'Waive' }[this.reviewKind()];
    return r ? `${verb} · ${r.documentTypeName} · ${r.travelerName}` : verb;
  });

  protected readonly requestOpen = signal(false);
  protected readonly documentTypes = signal<TravelDocumentType[]>([]);
  protected readonly requestApplicant = signal<number | null>(null);
  protected readonly requestType = signal<number | null>(null);
  protected readonly requestMandatory = signal(true);
  protected readonly requestInstructions = signal('');

  protected readonly groups = computed(() => {
    const byApplicant = new Map<number, { applicantId: number; name: string; items: TravelApplicantDocument[] }>();
    for (const r of this.docs()?.requirements ?? []) {
      const g = byApplicant.get(r.applicantId) ?? { applicantId: r.applicantId, name: r.travelerName, items: [] };
      g.items.push(r);
      byApplicant.set(r.applicantId, g);
    }
    return [...byApplicant.values()];
  });

  constructor() {
    // Reload whenever the case or its status changes (e.g. after "Request documents").
    effect(() => {
      this.caseId();
      this.caseStatus();
      this.load();
    });
  }

  load(): void {
    this.error.set(null);
    this.api.getCaseDocuments(this.caseId()).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: d => this.docs.set(d),
      error: err => this.error.set(apiErrorMessage(err, 'Could not load documents.'))
    });
  }

  protected view(f: TravelDocumentFile): void {
    this.viewing.set(f);
    this.viewerOpen.set(true);
  }

  protected upload(r: TravelApplicantDocument, file: File): void {
    this.uploading.set(r.id);
    this.progress.set(0);
    this.api.upload(r.id, file).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: event => {
        if (event.type === HttpEventType.UploadProgress && event.total) this.progress.set(Math.round((100 * event.loaded) / event.total));
        if (event.type === HttpEventType.Response) {
          this.finishUpload();
          this.toast.success(`${r.documentTypeName} uploaded.`);
          this.refresh();
        }
      },
      error: err => { this.finishUpload(); this.toast.error(apiErrorMessage(err, 'Upload failed.')); }
    });
  }

  protected openReview(r: TravelApplicantDocument, kind: ReviewKind): void {
    this.reviewing.set(r);
    this.reviewKind.set(kind);
    this.reviewNote.set('');
    this.reviewExpiry.set(r.expiryDate ? r.expiryDate.substring(0, 10) : '');
    this.dialogError.set(null);
    this.reviewOpen.set(true);
  }

  protected submitReview(): void {
    const r = this.reviewing();
    if (!r) return;
    const note = this.reviewNote().trim();
    const call = this.reviewKind() === 'verify'
      ? this.api.verify(r.id, this.reviewExpiry() || null, note || null)
      : this.reviewKind() === 'reject' ? this.api.reject(r.id, note) : this.api.waive(r.id, note);
    this.busy.set(true);
    call.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.busy.set(false); this.reviewOpen.set(false); this.toast.success('Saved.'); this.refresh(); },
      error: err => { this.busy.set(false); this.dialogError.set(apiErrorMessage(err, 'Could not save.')); }
    });
  }

  protected openRequest(): void {
    this.dialogError.set(null);
    this.requestApplicant.set(null);
    this.requestType.set(null);
    this.requestMandatory.set(true);
    this.requestInstructions.set('');
    this.config.getDocumentTypes(true).pipe(catchError(() => of([]))).subscribe(t => this.documentTypes.set(t));
    this.requestOpen.set(true);
  }

  protected submitRequest(): void {
    const applicant = this.requestApplicant();
    const type = this.requestType();
    if (!applicant || !type) return;
    this.busy.set(true);
    this.api.request(this.caseId(), applicant, type, this.requestMandatory(), this.requestInstructions().trim() || null)
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: () => { this.busy.set(false); this.requestOpen.set(false); this.toast.success('Document requested.'); this.refresh(); },
        error: err => { this.busy.set(false); this.dialogError.set(apiErrorMessage(err, 'Could not request the document.')); }
      });
  }

  protected async remove(r: TravelApplicantDocument): Promise<void> {
    if (!(await this.confirm.confirmDelete(`Remove the ${r.documentTypeName} requirement for ${r.travelerName}?`, 'Remove requirement'))) return;
    this.api.removeRequirement(r.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.toast.success('Requirement removed.'); this.refresh(); },
      error: err => this.toast.error(apiErrorMessage(err, 'Could not remove the requirement.'))
    });
  }

  private finishUpload(): void {
    this.uploading.set(null);
    this.progress.set(null);
  }

  private refresh(): void {
    this.load();
    this.changed.emit();
  }
}
