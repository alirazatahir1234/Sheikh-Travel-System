import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  computed,
  inject,
  signal
} from '@angular/core';
import { DatePipe, NgClass, PercentPipe } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { forkJoin, catchError, of } from 'rxjs';

import { DriverService } from '../../../core/services/driver.service';
import { OcrService } from '../../../core/services/ocr.service';
import { OcrSettings } from '../../../core/models/ocr.model';
import {
  DRIVER_VERIFICATION_DOC_TYPES,
  DRIVER_VERIFICATION_STATUS_LABELS,
  Driver,
  DriverDocumentDetailed,
  DriverStatus,
  DriverTimelineEvent,
  DriverVerificationStatus,
  VerificationReviewNote,
  DocumentStatus,
  driverDisplayName
} from '../../../core/models/driver.model';
import {
  resolveUploadUrl,
  resolveDriverPhotoUrl,
  vehicleUploadSizeError,
  UPLOAD_MAX_SIZE_LABEL,
  isImageUploadUrl,
  isPdfUploadUrl
} from '../../../core/utils/upload-url.util';
import { apiErrorMessage } from '../../../core/utils/api-error.util';
import { UiPageHeaderComponent } from '../../../shared/components/ui/page-header/ui-page-header.component';
import { UiButtonComponent } from '../../../shared/components/ui/button/ui-button.component';
import { UiStatusBadgeComponent } from '../../../shared/components/ui/status-badge/ui-status-badge.component';

interface DocSlot {
  type: string;
  label: string;
  doc: DriverDocumentDetailed | null;
  /** Rejection reason being entered by the reviewer */
  rejectReason: string;
  /** Whether the reject-reason input is visible */
  showRejectInput: boolean;
  /** Expiry date being set when uploading */
  pendingExpiryDate: string;
  uploadSizeError: string | null;
}

const PIPELINE_STEPS: { key: DriverVerificationStatus; label: string; icon: string }[] = [
  { key: 'Pending',     label: 'Pending',      icon: 'schedule'       },
  { key: 'UnderReview', label: 'Under Review',  icon: 'manage_search'  },
  { key: 'Verified',    label: 'Verified',      icon: 'verified'       },
  { key: 'Rejected',    label: 'Rejected',      icon: 'cancel'         },
  { key: 'ExpiredDocs', label: 'Docs Expired',  icon: 'event_busy'     }
];

const STATUS_ORDER: Record<DriverVerificationStatus, number> = {
  Pending: 0, UnderReview: 1, Verified: 2, Rejected: 2, ExpiredDocs: 2
};

const DEFAULT_OCR_SETTINGS: OcrSettings = {
  mode: 'HYBRID',
  confidenceThreshold: 70,
  enableFallback: true,
  saveRawOcr: false,
  azureEnabled: true,
  paddleEnabled: true
};

@Component({
  selector: 'driver-verification-hub',
  standalone: true,
  imports: [
    DatePipe,
    NgClass,
    RouterModule,
    MatIconModule,
    UiPageHeaderComponent,
    UiButtonComponent,
    UiStatusBadgeComponent
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './driver-verification-hub.component.html',
  styleUrls: ['./driver-verification-hub.component.scss']
})
export class DriverVerificationHubComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly driverService = inject(DriverService);
  private readonly ocrService = inject(OcrService);
  private readonly toast = inject(UiToastService);
  private readonly destroyRef = inject(DestroyRef);

  // ── State ────────────────────────────────────────────────────────────────
  readonly loading = signal(true);
  readonly saving  = signal(false);
  readonly driver  = signal<Driver | null>(null);
  readonly docSlots = signal<DocSlot[]>([]);
  readonly timeline = signal<DriverTimelineEvent[]>([]);
  readonly reviewNotes = signal<VerificationReviewNote[]>([]);

  readonly ocrResult = signal<Record<string, string | null>>({});
  readonly ocrLoading = signal<Record<string, boolean>>({});

  readonly newNote = signal('');
  readonly newNoteDocType = signal<string>('');
  readonly savingNote = signal(false);

  driverId = 0;

  readonly uploadMaxSizeLabel = UPLOAD_MAX_SIZE_LABEL;
  readonly pipelineSteps = PIPELINE_STEPS;
  readonly docTypes = DRIVER_VERIFICATION_DOC_TYPES;
  readonly statusLabels = DRIVER_VERIFICATION_STATUS_LABELS;

  // ── Computed ─────────────────────────────────────────────────────────────
  readonly driverName = computed(() => {
    const d = this.driver();
    return d ? driverDisplayName(d) : '—';
  });

  readonly photoUrl = computed(() => resolveDriverPhotoUrl(this.driver()?.photoUrl));

  readonly verificationStatus = computed<DriverVerificationStatus>(() => {
    const raw = this.driver()?.verificationStatus ?? 'Pending';
    return (DRIVER_VERIFICATION_STATUS_LABELS[raw as DriverVerificationStatus] != null
      ? raw
      : 'Pending') as DriverVerificationStatus;
  });

  readonly pipelineActiveIndex = computed(() => {
    const st = this.verificationStatus();
    return STATUS_ORDER[st] ?? 0;
  });

  readonly totalDocs    = computed(() => this.docTypes.length);
  readonly uploadedDocs = computed(() =>
    this.docSlots().filter(s => s.doc && s.doc.status !== 'Missing').length
  );
  readonly approvedDocs = computed(() =>
    this.docSlots().filter(s => s.doc?.status === 'Approved').length
  );

  readonly completionPct = computed(() => {
    const slots = this.docSlots();
    if (!slots.length) return 0;
    const checks = [
      slots.some(s => s.type === 'DrivingLicense'      && s.doc?.status === 'Approved'),
      slots.some(s => s.type === 'MedicalCertificate'  && s.doc?.status === 'Approved'),
      slots.some(s => s.type === 'BackgroundCheck'      && s.doc?.status === 'Approved'),
      !this.driver()?.licenseExpired
    ];
    return Math.round((checks.filter(Boolean).length / checks.length) * 100);
  });

  readonly daysUntilLicenseExpiry = computed(() => {
    const d = this.driver();
    if (!d?.licenseExpiryDate) return null;
    const diff = new Date(d.licenseExpiryDate).getTime() - Date.now();
    return Math.ceil(diff / 86_400_000);
  });

  readonly canApproveAll = computed(() =>
    this.verificationStatus() === 'UnderReview' && this.approvedDocs() === this.totalDocs()
  );
  readonly canStartReview = computed(() => this.verificationStatus() === 'Pending');

  // ── Lifecycle ─────────────────────────────────────────────────────────────
  ngOnInit(): void {
    this.driverId = Number(this.route.snapshot.paramMap.get('id'));
    this.load();
  }

  load(): void {
    this.loading.set(true);
    forkJoin({
      driver:    this.driverService.getById(this.driverId),
      documents: this.driverService.getDocumentsDetailed(this.driverId),
      timeline:  this.driverService.getTimeline(this.driverId).pipe(catchError(() => of([]))),
      notes:     this.driverService.getReviewNotes(this.driverId).pipe(catchError(() => of([])))
    })
    .pipe(takeUntilDestroyed(this.destroyRef))
    .subscribe({
      next: ({ driver, documents, timeline, notes }) => {
        this.driver.set(driver);
        this.timeline.set(timeline);
        this.reviewNotes.set(notes);
        this.buildDocSlots(documents);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.toast.error('Failed to load driver details');
      }
    });
  }

  private buildDocSlots(docs: DriverDocumentDetailed[]): void {
    const slots: DocSlot[] = this.docTypes.map(dt => ({
      type: dt.type,
      label: dt.label,
      doc: docs.find(d => d.documentType === dt.type) ?? null,
      rejectReason: '',
      showRejectInput: false,
      pendingExpiryDate: '',
      uploadSizeError: null
    }));
    this.docSlots.set(slots);
  }

  // ── Pipeline actions ──────────────────────────────────────────────────────
  startReview(): void {
    this.saving.set(true);
    this.driverService.startReview(this.driverId).subscribe({
      next: () => { this.toast.success('Review started'); this.load(); },
      error: err => { this.saving.set(false); this.toast.error(apiErrorMessage(err, 'Failed')); }
    });
  }

  approveAll(): void {
    this.saving.set(true);
    this.driverService.updateVerification(this.driverId, 'Verified').subscribe({
      next: () => { this.toast.success('Driver verified ✓'); this.load(); },
      error: err => { this.saving.set(false); this.toast.error(apiErrorMessage(err, 'Failed')); }
    });
  }

  rejectDriver(): void {
    this.saving.set(true);
    this.driverService.updateVerification(this.driverId, 'Rejected').subscribe({
      next: () => { this.toast.success('Driver rejected'); this.load(); },
      error: err => { this.saving.set(false); this.toast.error(apiErrorMessage(err, 'Failed')); }
    });
  }

  // ── Per-document actions ──────────────────────────────────────────────────
  approveDoc(slot: DocSlot): void {
    if (!slot.doc) return;
    this.driverService.updateDocumentStatus(this.driverId, slot.doc.id, { status: 'Approved' }).subscribe({
      next: () => { this.toast.success(`${slot.label} approved`); this.load(); },
      error: err => this.toast.error(apiErrorMessage(err, 'Failed'))
    });
  }

  showRejectInput(slot: DocSlot): void {
    this.docSlots.update(slots =>
      slots.map(s => s.type === slot.type ? { ...s, showRejectInput: true } : s)
    );
  }

  cancelReject(slot: DocSlot): void {
    this.docSlots.update(slots =>
      slots.map(s => s.type === slot.type ? { ...s, showRejectInput: false, rejectReason: '' } : s)
    );
  }

  confirmReject(slot: DocSlot): void {
    if (!slot.doc) return;
    this.driverService.updateDocumentStatus(this.driverId, slot.doc.id, {
      status: 'Rejected',
      rejectionReason: slot.rejectReason || null
    }).subscribe({
      next: () => { this.toast.success(`${slot.label} rejected`); this.load(); },
      error: err => this.toast.error(apiErrorMessage(err, 'Failed'))
    });
  }

  onRejectReasonChange(slot: DocSlot, value: string): void {
    this.docSlots.update(slots =>
      slots.map(s => s.type === slot.type ? { ...s, rejectReason: value } : s)
    );
  }

  onExpiryChange(slot: DocSlot, value: string): void {
    this.docSlots.update(slots =>
      slots.map(s => s.type === slot.type ? { ...s, pendingExpiryDate: value } : s)
    );
  }

  // ── Upload ─────────────────────────────────────────────────────────────────
  onUploadDoc(slot: DocSlot, event: Event): void {
    const input = event.target as HTMLInputElement;
    const file  = input.files?.[0];
    if (!file) return;

    const sizeError = vehicleUploadSizeError(file);
    if (sizeError) {
      this.docSlots.update(slots => slots.map(s => s.type === slot.type ? { ...s, uploadSizeError: sizeError } : s));
      input.value = '';
      return;
    }
    this.docSlots.update(slots => slots.map(s => s.type === slot.type ? { ...s, uploadSizeError: null } : s));

    const currentSlot = this.docSlots().find(s => s.type === slot.type);
    const expiryDate  = currentSlot?.pendingExpiryDate || undefined;

    this.driverService.uploadDocument(this.driverId, slot.type, file, expiryDate).subscribe({
      next: () => {
        this.toast.success(`${slot.label} uploaded`);
        this.runOcr(slot, file);
        this.load();
        input.value = '';
      },
      error: err => this.toast.error(apiErrorMessage(err, 'Upload failed'))
    });
  }

  // ── OCR ────────────────────────────────────────────────────────────────────
  private runOcr(slot: DocSlot, file: File): void {
    if (slot.type !== 'DrivingLicense' && slot.type !== 'BackgroundCheck') return;
    this.ocrLoading.update(m => ({ ...m, [slot.type]: true }));
    this.ocrService.extractFromDocument(file, DEFAULT_OCR_SETTINGS).subscribe({
      next: result => {
        const chip = result.identityNumber || result.cnic
          ? `ID: ${result.identityNumber ?? result.cnic}`
          : result.fullName
          ? `Name: ${result.fullName}`
          : 'OCR: No data extracted';
        this.ocrResult.update(m => ({ ...m, [slot.type]: chip }));
        this.ocrLoading.update(m => ({ ...m, [slot.type]: false }));
      },
      error: () => {
        this.ocrLoading.update(m => ({ ...m, [slot.type]: false }));
      }
    });
  }

  // ── Review notes ───────────────────────────────────────────────────────────
  submitNote(): void {
    const note = this.newNote().trim();
    if (!note) return;
    this.savingNote.set(true);
    this.driverService.addReviewNote(this.driverId, {
      note,
      documentType: this.newNoteDocType() || null
    }).subscribe({
      next: added => {
        this.reviewNotes.update(n => [added, ...n]);
        this.newNote.set('');
        this.newNoteDocType.set('');
        this.savingNote.set(false);
      },
      error: err => {
        this.savingNote.set(false);
        this.toast.error(apiErrorMessage(err, 'Failed to save note'));
      }
    });
  }

  // ── Helpers ────────────────────────────────────────────────────────────────
  docStatusVariant(status: DocumentStatus | undefined): 'success' | 'warning' | 'error' | 'inactive' {
    switch (status) {
      case 'Approved': return 'success';
      case 'Uploaded': return 'warning';
      case 'Rejected': return 'error';
      case 'Expired':  return 'error';
      default:         return 'inactive';
    }
  }

  pipelineStepClass(step: typeof PIPELINE_STEPS[0]): string {
    const active = this.verificationStatus();
    if (step.key === active) return 'step--active';
    if (step.key === 'Rejected' && active === 'Rejected') return 'step--rejected';
    if (STATUS_ORDER[step.key] < STATUS_ORDER[active]) return 'step--done';
    return '';
  }

  resolveUrl(url: string | null | undefined): string | null {
    return resolveUploadUrl(url);
  }

  isImage(url: string | null | undefined): boolean {
    return isImageUploadUrl(url);
  }

  isPdf(url: string | null | undefined): boolean {
    return isPdfUploadUrl(url);
  }

  goBack(): void {
    void this.router.navigate(['/drivers', this.driverId]);
  }
}
