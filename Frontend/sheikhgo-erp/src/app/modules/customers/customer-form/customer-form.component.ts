import { Component, OnDestroy, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, Subject, firstValueFrom } from 'rxjs';
import { takeUntil } from 'rxjs/operators';

import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { CustomerService } from '../../../core/services/customer.service';
import { OcrService } from '../../../core/services/ocr.service';
import { OcrSettingsService } from '../../../core/services/ocr-settings.service';
import { OcrExtractResult } from '../../../core/models/ocr.model';
import {
  compressCnicImageToJpeg,
  enrichCnicParsedFromCombinedRaw,
  mergeCnicSideAddresses,
  parseCnicDocumentOcrText,
  pickBestCnicFullName,
  polishCnicParsedFields,
  type CnicOcrParsedFields
} from '../../../core/utils/cnic-document-ocr.util';
import {
  Customer,
  CreateCustomerDto,
  UpdateCustomerDto
} from '../../../core/models/customer.model';

interface CnicBackendOcrMeta {
  ocrEngine: string;
  confidence: number | null;
  fallbackUsed: boolean | null;
  lowConfidence: boolean | null;
  confidenceThreshold: number | null;
  addressTranslated: boolean | null;
  azureQualityMergeUsed: boolean | null;
  primaryOcrEngine: string | null;
  secondaryOcrEngine: string | null;
}

type CnicSideUiStatus = 'empty' | 'uploading' | 'ocr' | 'done' | 'error';

@Component({
  selector: 'app-customer-form',
  templateUrl: './customer-form.component.html',
  styleUrls: ['./customer-form.component.scss']
})
export class CustomerFormComponent implements OnInit, OnDestroy {
  form: FormGroup;
  /** True while saving (Create / Update). */
  loading = false;
  /** True while loading an existing customer for edit. */
  loadingCustomer = false;
  isEdit = false;
  customerId: number | null = null;

  /** Cached list for CNIC duplicate hints after OCR. */
  private customers: Customer[] = [];
  private readonly maxUploadBytes = 15 * 1024 * 1024;

  cnicUploadBusy = false;
  cnicOcrBusy = false;
  cnicFrontFileName: string | null = null;
  cnicBackFileName: string | null = null;
  cnicFrontPreviewDataUrl: string | null = null;
  cnicBackPreviewDataUrl: string | null = null;
  cnicOcrProvider: string | null = null;
  cnicOcrConfidence: number | null = null;
  cnicOcrLowConfidence = false;
  cnicOcrAddressTranslated: boolean | null = null;
  cnicOcrAzureMerge = false;
  cnicOcrErrorRecovery = false;
  cnicOcrPrimaryEngine: string | null = null;
  cnicOcrSecondaryEngine: string | null = null;
  cnicOcrConfidenceThreshold: number | null = null;
  /** JPEG sent to OCR (same orientation as preview); used for Retry OCR. */
  cnicOrientedFrontFile: File | null = null;
  cnicOrientedBackFile: File | null = null;
  /** Full-size CNIC preview overlay. */
  cnicLightboxUrl: string | null = null;

  /** Instant blob preview before compression finishes. */
  cnicFrontInstantPreviewUrl: string | null = null;
  cnicBackInstantPreviewUrl: string | null = null;
  cnicFrontSideUi: CnicSideUiStatus = 'empty';
  cnicBackSideUi: CnicSideUiStatus = 'empty';
  /** 0–100 synthetic progress while preparing / OCR. */
  cnicSyntheticProgress = 0;
  cnicLastOcrSuccessMessage: string | null = null;
  cnicLastOcrInlineError: string | null = null;
  readonly ocrHighlightFields = new Set<string>();
  private uploadProgressIntervalId: number | null = null;
  private clearSuccessMessageTimeoutId: number | null = null;
  private clearHighlightTimeoutId: number | null = null;

  /** Drag-over highlight for unified drop zone. */
  cnicUnifiedDragOver = false;
  /** Last OCR/extract path failed — show Retry. */
  cnicOcrLastFailure = false;
  private cnicReplaceTargetSide: 'front' | 'back' | null = null;

  private cnicFrontExtracted: CnicOcrParsedFields | null = null;
  private cnicBackExtracted: CnicOcrParsedFields | null = null;
  /** Raw OCR text per side for a second pass after both sides exist (holder name often needs full context). */
  private cnicFrontOcrRawText: string | null = null;
  private cnicBackOcrRawText: string | null = null;
  private cnicBackendMetaFront: CnicBackendOcrMeta | null = null;
  private cnicBackendMetaBack: CnicBackendOcrMeta | null = null;

  private readonly destroy$ = new Subject<void>();

  constructor(
    private fb: FormBuilder,
    private customerService: CustomerService,
    private ocrService: OcrService,
    private ocrSettingsService: OcrSettingsService,
    private router: Router,
    private route: ActivatedRoute,
    private toast: UiToastService
  ) {
    this.form = this.fb.group({
      fullName: ['', [Validators.required, Validators.maxLength(100)]],
      fatherOrHusbandName: ['', [Validators.maxLength(200)]],
      phone: ['', [Validators.required, Validators.maxLength(20)]],
      email: ['', [Validators.email, Validators.maxLength(120)]],
      cnic: [''],
      gender: ['', [Validators.maxLength(20)]],
      dateOfBirth: [''],
      nationality: ['', [Validators.maxLength(120)]],
      address: ['']
    });
  }

  ngOnInit(): void {
    this.customerService.getAll(1, 500).pipe(takeUntil(this.destroy$)).subscribe({
      next: (r) => (this.customers = r.items ?? []),
      error: () => (this.customers = [])
    });

    this.route.paramMap.pipe(takeUntil(this.destroy$)).subscribe(pm => {
      const idStr = pm.get('id');
      if (!idStr) {
        this.isEdit = false;
        this.customerId = null;
        this.resetFormForCreate();
        return;
      }
      const id = +idStr;
      if (!Number.isFinite(id) || id <= 0) return;

      this.isEdit = true;
      this.customerId = id;
      this.loadCustomer(id);
    });
  }

  ngOnDestroy(): void {
    this.stopSyntheticProgress();
    this.clearCnicTimers();
    this.revokeAllInstantPreviews();
    this.destroy$.next();
    this.destroy$.complete();
  }

  private resetFormForCreate(): void {
    this.loadingCustomer = false;
    this.form.reset({
      fullName: '',
      fatherOrHusbandName: '',
      phone: '',
      email: '',
      cnic: '',
      gender: '',
      dateOfBirth: '',
      nationality: '',
      address: ''
    });
    this.clearCnicUploadState();
  }

  private clearCnicUploadState(): void {
    this.cnicUnifiedDragOver = false;
    this.cnicOcrLastFailure = false;
    this.cnicReplaceTargetSide = null;
    this.cnicFrontFileName = null;
    this.cnicBackFileName = null;
    this.cnicFrontPreviewDataUrl = null;
    this.cnicBackPreviewDataUrl = null;
    this.cnicFrontExtracted = null;
    this.cnicBackExtracted = null;
    this.cnicFrontOcrRawText = null;
    this.cnicBackOcrRawText = null;
    this.cnicBackendMetaFront = null;
    this.cnicBackendMetaBack = null;
    this.cnicOcrProvider = null;
    this.cnicOcrConfidence = null;
    this.cnicOcrLowConfidence = false;
    this.cnicOcrAddressTranslated = null;
    this.cnicOcrAzureMerge = false;
    this.cnicOcrErrorRecovery = false;
    this.cnicOcrPrimaryEngine = null;
    this.cnicOcrSecondaryEngine = null;
    this.cnicOcrConfidenceThreshold = null;
    this.cnicOrientedFrontFile = null;
    this.cnicOrientedBackFile = null;
    this.revokeAllInstantPreviews();
    this.cnicFrontSideUi = 'empty';
    this.cnicBackSideUi = 'empty';
    this.cnicSyntheticProgress = 0;
    this.cnicLastOcrSuccessMessage = null;
    this.cnicLastOcrInlineError = null;
    this.ocrHighlightFields.clear();
    this.stopSyntheticProgress();
    this.clearCnicTimers();
  }

  get cnicFrontDisplayUrl(): string | null {
    return this.cnicFrontPreviewDataUrl || this.cnicFrontInstantPreviewUrl;
  }

  get cnicBackDisplayUrl(): string | null {
    return this.cnicBackPreviewDataUrl || this.cnicBackInstantPreviewUrl;
  }

  get showCnicRetryOcr(): boolean {
    return this.cnicOcrLastFailure && (!!this.cnicOrientedFrontFile || !!this.cnicOrientedBackFile);
  }

  get cnicPipelineStepLabel(): string {
    if (this.cnicOcrBusy) return 'Step 2 of 3 — Reading CNIC (OCR)';
    if (this.cnicUploadBusy) return 'Step 1 of 3 — Preparing images';
    if (this.cnicLastOcrSuccessMessage) return 'Step 3 of 3 — Form updated';
    return '';
  }

  get cnicBackMissingHint(): boolean {
    return !!this.cnicFrontFileName && !this.cnicBackFileName;
  }

  cnicSideStatusLabel(side: 'front' | 'back'): string {
    const s = side === 'front' ? this.cnicFrontSideUi : this.cnicBackSideUi;
    switch (s) {
      case 'uploading':
        return 'Preparing image';
      case 'ocr':
        return 'Reading text…';
      case 'done':
        return 'Ready';
      case 'error':
        return 'Issue';
      default:
        return '';
    }
  }

  removeCnicSide(side: 'front' | 'back'): void {
    if (this.cnicUploadBusy || this.cnicOcrBusy) return;
    if (side === 'front') {
      this.revokeInstantPreview('front');
      this.cnicFrontFileName = null;
      this.cnicFrontPreviewDataUrl = null;
      this.cnicOrientedFrontFile = null;
      this.cnicFrontExtracted = null;
      this.cnicFrontOcrRawText = null;
      this.cnicBackendMetaFront = null;
      this.cnicFrontSideUi = 'empty';
    } else {
      this.revokeInstantPreview('back');
      this.cnicBackFileName = null;
      this.cnicBackPreviewDataUrl = null;
      this.cnicOrientedBackFile = null;
      this.cnicBackExtracted = null;
      this.cnicBackOcrRawText = null;
      this.cnicBackendMetaBack = null;
      this.cnicBackSideUi = 'empty';
    }
    this.cnicOcrLastFailure = false;
    this.cnicLastOcrInlineError = null;
    this.cnicLastOcrSuccessMessage = null;
    this.refreshMergedCnicOcrDisplay();
    this.finalizeCnicMergeAndApply(false);
  }

  private revokeInstantPreview(side: 'front' | 'back'): void {
    if (side === 'front') {
      if (this.cnicFrontInstantPreviewUrl) {
        URL.revokeObjectURL(this.cnicFrontInstantPreviewUrl);
        this.cnicFrontInstantPreviewUrl = null;
      }
    } else {
      if (this.cnicBackInstantPreviewUrl) {
        URL.revokeObjectURL(this.cnicBackInstantPreviewUrl);
        this.cnicBackInstantPreviewUrl = null;
      }
    }
  }

  private revokeAllInstantPreviews(): void {
    this.revokeInstantPreview('front');
    this.revokeInstantPreview('back');
  }

  private setSideUiStatus(side: 'front' | 'back', status: CnicSideUiStatus): void {
    if (side === 'front') this.cnicFrontSideUi = status;
    else this.cnicBackSideUi = status;
  }

  private startSyntheticProgress(): void {
    this.stopSyntheticProgress();
    this.cnicSyntheticProgress = 8;
    this.uploadProgressIntervalId = window.setInterval(() => {
      this.cnicSyntheticProgress = Math.min(92, this.cnicSyntheticProgress + 4);
    }, 120);
  }

  private stopSyntheticProgress(): void {
    if (this.uploadProgressIntervalId != null) {
      clearInterval(this.uploadProgressIntervalId);
      this.uploadProgressIntervalId = null;
    }
  }

  private finishSyntheticProgress(): void {
    this.stopSyntheticProgress();
    this.cnicSyntheticProgress = 100;
    window.setTimeout(() => {
      this.cnicSyntheticProgress = 0;
    }, 450);
  }

  private clearCnicTimers(): void {
    if (this.clearSuccessMessageTimeoutId != null) {
      clearTimeout(this.clearSuccessMessageTimeoutId);
      this.clearSuccessMessageTimeoutId = null;
    }
    if (this.clearHighlightTimeoutId != null) {
      clearTimeout(this.clearHighlightTimeoutId);
      this.clearHighlightTimeoutId = null;
    }
  }

  onCnicUnifiedDragOver(ev: DragEvent): void {
    ev.preventDefault();
    ev.stopPropagation();
    this.cnicUnifiedDragOver = true;
  }

  onCnicUnifiedDragLeave(ev: DragEvent): void {
    ev.preventDefault();
    ev.stopPropagation();
    const related = ev.relatedTarget as Node | null;
    if (related && (ev.currentTarget as HTMLElement).contains(related)) return;
    this.cnicUnifiedDragOver = false;
  }

  onCnicUnifiedDrop(ev: DragEvent): void {
    ev.preventDefault();
    ev.stopPropagation();
    this.cnicUnifiedDragOver = false;
    const dt = ev.dataTransfer;
    const files = dt?.files?.length ? Array.from(dt.files) : [];
    if (!files.length) return;
    void this.ingestCnicImagesFromFiles(files);
  }

  onCnicUnifiedInputChange(ev: Event): void {
    const input = ev.target as HTMLInputElement;
    // FileList is live: clearing `value` empties `files` on the same reference — snapshot first.
    const files = input.files?.length ? Array.from(input.files) : [];
    input.value = '';
    if (files.length) void this.ingestCnicImagesFromFiles(files);
  }

  onCnicReplaceInputChange(ev: Event): void {
    const input = ev.target as HTMLInputElement;
    const file = input.files?.length ? input.files.item(0) : null;
    input.value = '';
    const side = this.cnicReplaceTargetSide;
    this.cnicReplaceTargetSide = null;
    if (!file || !side) return;
    void this.replaceCnicSide(side, file);
  }

  startReplaceCnicSide(side: 'front' | 'back', replaceInput: HTMLInputElement): void {
    if (this.cnicUploadBusy || this.cnicOcrBusy || this.loadingCustomer) return;
    this.cnicReplaceTargetSide = side;
    replaceInput.click();
  }

  private async replaceCnicSide(side: 'front' | 'back', file: File): Promise<void> {
    this.cnicOcrLastFailure = false;
    this.cnicLastOcrInlineError = null;
    await this.ingestCnicFile(side, file, { manageUploadBusy: true, applyOcrResult: false });
    this.finalizeCnicMergeAndApply(true);
  }

  private collectCnicEligibleFiles(source: FileList | File[]): File[] {
    const out: File[] = [];
    const list = source instanceof FileList ? Array.from(source) : source;
    for (const f of list) {
      if (f && this.isCnicEligibleFile(f)) {
        out.push(f);
      }
    }
    return out;
  }

  /** Some browsers leave `file.type` empty for camera/gallery picks — fall back on extension. */
  private isCnicEligibleFile(f: File): boolean {
    const t = (f.type || '').toLowerCase();
    if (t === 'image/jpeg' || t === 'image/jpg' || t === 'image/png' || t === 'image/webp') return true;
    if (t && t !== 'application/octet-stream') return false;
    const n = (f.name || '').toLowerCase();
    return /\.(jpe?g|png|webp)$/i.test(n);
  }

  /**
   * Guess front vs back from file names (front|face / back|rear|reverse…); otherwise first → front, second → back.
   */
  private partitionFrontBack(files: File[]): {
    front: File | null;
    back: File | null;
    skippedExtra: number;
  } {
    if (files.length === 0) return { front: null, back: null, skippedExtra: 0 };

    const hint = (name: string): 'front' | 'back' | 'unknown' => {
      const n = name.toLowerCase();
      if (/\b(back|rear|reverse|verso|dos|b\.|_b)\b/i.test(n) || /\bback\b/i.test(n)) return 'back';
      if (/\b(front|face|obverse|f\.|_f)\b/i.test(n) || /\bfront\b/i.test(n)) return 'front';
      return 'unknown';
    };

    let front: File | null = null;
    let back: File | null = null;
    const unknown: File[] = [];

    for (const f of files) {
      const h = hint(f.name);
      if (h === 'front') {
        if (!front) front = f;
        else unknown.push(f);
      } else if (h === 'back') {
        if (!back) back = f;
        else unknown.push(f);
      } else {
        unknown.push(f);
      }
    }

    for (const u of unknown) {
      if (!front) front = u;
      else if (!back) back = u;
      else break;
    }

    const used = (front ? 1 : 0) + (back ? 1 : 0);
    const skippedExtra = Math.max(0, files.length - used);

    return { front, back, skippedExtra };
  }

  async ingestCnicImagesFromFiles(source: FileList | File[]): Promise<void> {
    if (this.cnicUploadBusy || this.cnicOcrBusy || this.loadingCustomer) {
      if (this.loadingCustomer) {
        this.toast.warning('Please wait until the customer record finishes loading.');
      }
      return;
    }

    const eligible = this.collectCnicEligibleFiles(source);
    if (eligible.length === 0) {
      this.toast.warning('Use JPG, PNG, or WebP photos of the CNIC (PDF is not supported).');
      return;
    }

    const { front, back, skippedExtra } = this.partitionFrontBack(eligible);
    if (!front && !back) {
      this.toast.error('Could not assign images to front or back.');
      return;
    }

    if (skippedExtra > 0) {
      this.toast.info(`Using up to two images for CNIC. ${skippedExtra} extra file(s) were skipped.`);
    }

    this.cnicLastOcrInlineError = null;
    this.cnicLastOcrSuccessMessage = null;
    this.cnicUploadBusy = true;
    this.cnicOcrLastFailure = false;
    this.startSyntheticProgress();
    try {
      if (front) {
        await this.ingestCnicFile('front', front, { manageUploadBusy: false, applyOcrResult: false });
      }
      if (back) {
        await this.ingestCnicFile('back', back, { manageUploadBusy: false, applyOcrResult: false });
      }
      this.finalizeCnicMergeAndApply(true);
      if (!this.cnicOcrLastFailure) {
        this.cnicLastOcrInlineError = null;
      }
    } finally {
      this.cnicUploadBusy = false;
      this.finishSyntheticProgress();
    }
  }

  private finalizeCnicMergeAndApply(showSummaryToast: boolean): void {
    const merged = this.mergeCnicSides(this.cnicFrontExtracted, this.cnicBackExtracted);
    const filled = enrichCnicParsedFromCombinedRaw(merged, this.cnicFrontOcrRawText, this.cnicBackOcrRawText);
    this.applyCnicExtraction(filled, showSummaryToast);
  }

  private loadCustomer(id: number): void {
    this.loadingCustomer = true;
    this.customerService
      .getById(id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (c) => {
          if (this.customerId !== id) return;
          this.applyCustomerToForm(c);
          this.loadingCustomer = false;
        },
        error: (err: HttpErrorResponse) => {
          if (this.customerId !== id) return;
          this.loadingCustomer = false;
          this.toast.error(this.extractError(err) || 'Could not load customer.');
        }
      });
  }

  private applyCustomerToForm(c: Customer | Record<string, unknown>): void {
    const r = c as Record<string, unknown>;
    this.form.patchValue({
      fullName: this.readStr(r, 'fullName', 'FullName'),
      fatherOrHusbandName: this.readStrOrEmpty(r, 'fatherOrHusbandName', 'FatherOrHusbandName'),
      phone: this.readStr(r, 'phone', 'Phone'),
      email: this.readStrOrEmpty(r, 'email', 'Email'),
      cnic: this.readStrOrEmpty(r, 'cnic', 'cNIC', 'CNIC'),
      gender: this.readStrOrEmpty(r, 'gender', 'Gender'),
      dateOfBirth: this.readDateInput(r, 'dateOfBirth', 'DateOfBirth'),
      nationality: this.readStrOrEmpty(r, 'nationality', 'Nationality'),
      address: this.readStrOrEmpty(r, 'address', 'Address')
    });
  }

  private readDateInput(r: Record<string, unknown>, ...keys: string[]): string {
    for (const k of keys) {
      const v = r[k];
      if (v == null || v === '') continue;
      const s = String(v);
      if (s.length >= 10) return s.slice(0, 10);
      return s;
    }
    return '';
  }

  private readStr(r: Record<string, unknown>, ...keys: string[]): string {
    for (const k of keys) {
      const v = r[k];
      if (v != null && v !== '') return String(v);
    }
    return '';
  }

  private readStrOrEmpty(r: Record<string, unknown>, ...keys: string[]): string {
    for (const k of keys) {
      const v = r[k];
      if (v != null) return v === '' ? '' : String(v);
    }
    return '';
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.loading = true;
    const f = this.form.value;

    const baseDto: CreateCustomerDto = {
      fullName: f.fullName.trim(),
      phone: f.phone.trim(),
      email: f.email?.trim() ? f.email.trim() : null,
      cnic: f.cnic?.trim() ? f.cnic.trim() : null,
      address: f.address?.trim() ? f.address.trim() : null,
      fatherOrHusbandName: f.fatherOrHusbandName?.trim() ? f.fatherOrHusbandName.trim() : null,
      gender: f.gender?.trim() ? f.gender.trim() : null,
      dateOfBirth: f.dateOfBirth?.trim() ? f.dateOfBirth.trim() : null,
      nationality: f.nationality?.trim() ? f.nationality.trim() : null
    };

    const obs: Observable<unknown> = this.isEdit
      ? this.customerService.update({
          id: this.customerId!,
          customer: { ...baseDto } as UpdateCustomerDto
        })
      : this.customerService.create({ customer: baseDto });

    obs.subscribe({
      next: () => {
        this.toast.success(`Customer ${this.isEdit ? 'updated' : 'created'}`);
        this.router.navigate(['/customers']);
      },
      error: (err: HttpErrorResponse) => {
        this.loading = false;
        this.toast.error(this.extractError(err));
      }
    });
  }

  private extractError(err: HttpErrorResponse): string {
    const body: any = err?.error;
    if (body?.errors) {
      const flat = Object.values(body.errors).flat();
      if (flat.length) return String(flat[0]);
    }
    if (body?.error?.message) return String(body.error.message);
    if (body?.message) return String(body.message);
    if (typeof body === 'string' && body) return body;
    if (err.status === 404) return 'Customer not found.';
    if (err.status === 401 || err.status === 403) return 'Not authorized. Sign in again.';
    return `Operation failed (${err.status || 'network'}).`;
  }

  private primeImageInstantPreview(side: 'front' | 'back', file: File): void {
    this.revokeInstantPreview(side);
    if (side === 'front') {
      this.cnicFrontFileName = file.name;
      this.cnicFrontInstantPreviewUrl = URL.createObjectURL(file);
    } else {
      this.cnicBackFileName = file.name;
      this.cnicBackInstantPreviewUrl = URL.createObjectURL(file);
    }
    this.setSideUiStatus(side, 'uploading');
  }

  private async ingestCnicFile(
    side: 'front' | 'back',
    file: File,
    options?: { manageUploadBusy?: boolean; applyOcrResult?: boolean }
  ): Promise<void> {
    const manageUploadBusy = options?.manageUploadBusy !== false;
    const applyOcrResult = options?.applyOcrResult !== false;

    if (file.size > this.maxUploadBytes) {
      this.toast.warning('CNIC file must be 15 MB or smaller.');
      this.cnicOcrLastFailure = true;
      this.setSideUiStatus(side, 'error');
      this.cnicLastOcrInlineError = 'File is too large (max 15 MB).';
      return;
    }

    const isImage =
      file.type === 'image/jpeg' ||
      file.type === 'image/jpg' ||
      file.type === 'image/png' ||
      file.type === 'image/webp' ||
      (!file.type && /\.(jpe?g|png|webp)$/i.test(file.name || ''));
    if (!isImage) {
      this.toast.warning('Please upload CNIC as a JPG, PNG, or WebP photo (not PDF).');
      this.cnicOcrLastFailure = true;
      this.setSideUiStatus(side, 'error');
      this.cnicLastOcrInlineError = 'Unsupported file type.';
      return;
    }

    this.primeImageInstantPreview(side, file);

    const ownBusy = manageUploadBusy && !this.cnicUploadBusy;
    if (ownBusy) {
      this.cnicUploadBusy = true;
      this.startSyntheticProgress();
    }
    try {
      const { dataUrl, jpegFile } = await compressCnicImageToJpeg(file, 1600, 0.9);
      this.revokeInstantPreview(side);
      if (side === 'front') {
        this.cnicFrontPreviewDataUrl = dataUrl;
        this.cnicFrontFileName = file.name;
        this.cnicOrientedFrontFile = jpegFile;
      } else {
        this.cnicBackPreviewDataUrl = dataUrl;
        this.cnicBackFileName = file.name;
        this.cnicOrientedBackFile = jpegFile;
      }
      await this.extractAndMergeCnicSide(side, jpegFile, dataUrl, applyOcrResult);
    } catch {
      this.cnicOcrLastFailure = true;
      this.setSideUiStatus(side, 'error');
      this.cnicLastOcrInlineError = `Could not prepare the ${side} image. Try another photo or file.`;
      this.toast.error('Could not process CNIC image. Try a clearer file.');
    } finally {
      if (ownBusy) {
        this.cnicUploadBusy = false;
        this.finishSyntheticProgress();
      }
    }
  }

  private async extractAndMergeCnicSide(
    side: 'front' | 'back',
    ocrFile: File,
    imageDataUrl: string,
    applyToForm = true
  ): Promise<void> {
    this.cnicOcrBusy = true;
    this.setSideUiStatus(side, 'ocr');
    try {
      if (side === 'front') {
        this.cnicBackendMetaFront = null;
      } else {
        this.cnicBackendMetaBack = null;
      }

      const { extracted, backendResult, ocrRawText } = await this.runOcrOnCnicImage(ocrFile, imageDataUrl, side);

      if (side === 'front') {
        this.cnicFrontExtracted = extracted;
        this.cnicFrontOcrRawText = ocrRawText ?? null;
      } else {
        this.cnicBackExtracted = extracted;
        this.cnicBackOcrRawText = ocrRawText ?? null;
      }

      if (backendResult) {
        const ocrEngine =
          (backendResult.ocrEngine || backendResult.provider || 'OCR').trim() || 'OCR';
        const confidence = Number(backendResult.confidence ?? 0);
        const meta: CnicBackendOcrMeta = {
          ocrEngine,
          confidence: Number.isFinite(confidence) ? Math.max(0, Math.round(confidence)) : null,
          fallbackUsed: backendResult.fallbackUsed ?? null,
          lowConfidence: backendResult.lowConfidence ?? null,
          confidenceThreshold:
            backendResult.confidenceThreshold != null && Number.isFinite(backendResult.confidenceThreshold)
              ? Math.round(backendResult.confidenceThreshold)
              : null,
          addressTranslated: backendResult.addressTranslated ?? null,
          azureQualityMergeUsed: backendResult.azureQualityMergeUsed ?? null,
          primaryOcrEngine: backendResult.primaryOcrEngine ?? null,
          secondaryOcrEngine: backendResult.secondaryOcrEngine ?? null
        };
        if (side === 'front') {
          this.cnicBackendMetaFront = meta;
        } else {
          this.cnicBackendMetaBack = meta;
        }
      }

      this.setSideUiStatus(side, 'done');
      this.refreshMergedCnicOcrDisplay();
      if (applyToForm) {
        this.finalizeCnicMergeAndApply(false);
      }

      this.cnicOcrLastFailure = false;
    } catch {
      this.cnicOcrLastFailure = true;
      this.setSideUiStatus(side, 'error');
      this.cnicLastOcrInlineError = `Could not read the ${side} side clearly. Try a sharper photo or use Retry OCR.`;
      this.toast.error('OCR failed. You can still fill fields manually.');
    } finally {
      this.cnicOcrBusy = false;
    }
  }

  private async runOcrOnCnicImage(
    ocrFile: File,
    imageDataUrl: string,
    side: 'front' | 'back'
  ): Promise<{
    extracted: CnicOcrParsedFields;
    backendResult: OcrExtractResult | null;
    usedTesseract: boolean;
    ocrRawText?: string;
  }> {
    const settings = this.ocrSettingsService.getSettings();
    try {
      const backend = await firstValueFrom(this.ocrService.extractFromDocument(ocrFile, settings));
      let extracted = this.ocrResultToExtracted(backend);
      if (backend.rawText?.trim()) {
        const fromRaw = parseCnicDocumentOcrText(backend.rawText, side);
        extracted = polishCnicParsedFields({
          fullName: pickBestCnicFullName(extracted.fullName, fromRaw.fullName),
          fatherName: extracted.fatherName || fromRaw.fatherName,
          cnic: extracted.cnic || fromRaw.cnic,
          address: mergeCnicSideAddresses(fromRaw.address, extracted.address),
          phone: extracted.phone || fromRaw.phone,
          email: extracted.email || fromRaw.email,
          gender: extracted.gender || fromRaw.gender,
          dateOfBirth: extracted.dateOfBirth || fromRaw.dateOfBirth,
          nationality: extracted.nationality || fromRaw.nationality
        });
      }
      const ocrRawText = (backend.rawText || '').trim() || undefined;
      if (
        extracted.fullName ||
        extracted.fatherName ||
        extracted.cnic ||
        extracted.address ||
        extracted.phone ||
        extracted.email ||
        extracted.gender ||
        extracted.dateOfBirth ||
        extracted.nationality ||
        ocrRawText
      ) {
        return {
          extracted,
          backendResult: backend,
          usedTesseract: false,
          ocrRawText
        };
      }
    } catch {
      // Continue to frontend OCR fallback.
    }

    const { createWorker } = await import('tesseract.js');
    const worker = await createWorker('eng');
    const { data } = await worker.recognize(imageDataUrl);
    await worker.terminate();

    const extracted = polishCnicParsedFields(parseCnicDocumentOcrText(data.text || '', side));
    return {
      extracted,
      backendResult: null,
      usedTesseract: true,
      ocrRawText: (data.text || '').trim() || undefined
    };
  }

  private ocrResultToExtracted(result: OcrExtractResult | null | undefined): CnicOcrParsedFields {
    if (!result) return {};
    const rawDob = (result.dateOfBirth ?? '').toString().trim();
    const dateOfBirth = rawDob.length >= 10 ? rawDob.slice(0, 10) : rawDob || undefined;
    return polishCnicParsedFields({
      fullName: (result.fullName || '').trim() || undefined,
      fatherName: (result.fatherName || '').trim() || undefined,
      cnic: ((result.identityNumber || result.cnic || '').trim()) || undefined,
      address: (result.address || '').trim() || undefined,
      gender: (result.gender || '').trim() || undefined,
      dateOfBirth,
      nationality: (result.nationality || '').trim() || undefined
    });
  }

  private mergeCnicSides(front: CnicOcrParsedFields | null, back: CnicOcrParsedFields | null): CnicOcrParsedFields {
    if (!this.sideHasCnicData(front) && !this.sideHasCnicData(back)) return {};
    const fullName = pickBestCnicFullName(front?.fullName, back?.fullName);
    const cnicRaw = (front?.cnic || back?.cnic || '').trim() || undefined;
    const addrF = (front?.address || '').trim();
    const addrB = (back?.address || '').trim();
    const address = mergeCnicSideAddresses(addrF || undefined, addrB || undefined);
    const phone = (front?.phone || back?.phone || '').trim() || undefined;
    const email = (front?.email || back?.email || '').trim() || undefined;
    const pickLonger = (a?: string, b?: string) => {
      const x = (a || '').trim();
      const y = (b || '').trim();
      if (!x) return y || undefined;
      if (!y) return x || undefined;
      return x.length >= y.length ? x : y;
    };
    const fatherName = pickLonger(front?.fatherName, back?.fatherName);
    const gender = (front?.gender || back?.gender || '').trim() || undefined;
    const dateOfBirth = (front?.dateOfBirth || back?.dateOfBirth || '').trim() || undefined;
    const nationality = (front?.nationality || back?.nationality || '').trim() || undefined;
    return { fullName, cnic: cnicRaw, address, phone, email, fatherName, gender, dateOfBirth, nationality };
  }

  private refreshMergedCnicOcrDisplay(): void {
    const hasFront = this.sideHasCnicData(this.cnicFrontExtracted);
    const hasBack = this.sideHasCnicData(this.cnicBackExtracted);
    const metas = [this.cnicBackendMetaFront, this.cnicBackendMetaBack].filter(
      (m): m is CnicBackendOcrMeta => m != null
    );

    if (hasFront && hasBack) {
      const engines = metas.map((m) => m.ocrEngine).filter(Boolean);
      const uniq = [...new Set(engines)];
      this.cnicOcrProvider = uniq.length <= 1 ? uniq[0] ?? 'Hybrid' : 'Hybrid';
    } else if (metas.length > 0) {
      this.cnicOcrProvider = metas[0].ocrEngine;
    } else {
      this.cnicOcrProvider = hasFront || hasBack ? 'Frontend OCR' : null;
    }

    const confidences = metas.map((m) => m.confidence).filter((c): c is number => c != null && Number.isFinite(c));
    this.cnicOcrConfidence = confidences.length
      ? Math.round(confidences.reduce((a, b) => a + b, 0) / confidences.length)
      : null;

    this.cnicOcrLowConfidence = metas.some((m) => m.lowConfidence === true);
    if (metas.some((m) => m.addressTranslated === true)) {
      this.cnicOcrAddressTranslated = true;
    } else if (metas.length > 0 && metas.every((m) => m.addressTranslated === false)) {
      this.cnicOcrAddressTranslated = false;
    } else {
      this.cnicOcrAddressTranslated = null;
    }

    this.cnicOcrAzureMerge = metas.some((m) => m.azureQualityMergeUsed === true);
    this.cnicOcrErrorRecovery = metas.some((m) => m.fallbackUsed === true);
    this.cnicOcrPrimaryEngine = metas.find((m) => m.primaryOcrEngine)?.primaryOcrEngine ?? null;
    this.cnicOcrSecondaryEngine = metas.find((m) => m.secondaryOcrEngine)?.secondaryOcrEngine ?? null;
    const th = metas.map((m) => m.confidenceThreshold).find((t) => t != null && Number.isFinite(t));
    this.cnicOcrConfidenceThreshold =
      th ?? Math.round(Number(this.ocrSettingsService.getSettings().confidenceThreshold) || 70);
  }

  private sideHasCnicData(e: CnicOcrParsedFields | null): boolean {
    return !!(
      e &&
      ((e.fullName || '').trim() ||
        (e.fatherName || '').trim() ||
        (e.cnic || '').trim() ||
        (e.address || '').trim() ||
        (e.gender || '').trim() ||
        (e.dateOfBirth || '').trim() ||
        (e.nationality || '').trim())
    );
  }

  private applyCnicExtraction(extracted: CnicOcrParsedFields, showSummaryToast: boolean): void {
    const ex = polishCnicParsedFields(extracted);
    const patch: Record<string, string> = {};
    const rawName = (extracted.fullName || '').trim();
    if (rawName) patch['fullName'] = (ex.fullName || '').trim();
    const rawAddr = (extracted.address || '').trim();
    if (rawAddr) patch['address'] = (ex.address || '').trim();
    if (ex.fatherName) patch['fatherOrHusbandName'] = ex.fatherName;
    if (ex.phone) patch['phone'] = ex.phone;
    if (ex.email) patch['email'] = ex.email;
    if (ex.cnic) patch['cnic'] = ex.cnic;
    if (ex.gender) patch['gender'] = ex.gender;
    if (ex.dateOfBirth) patch['dateOfBirth'] = ex.dateOfBirth;
    if (ex.nationality) patch['nationality'] = ex.nationality;

    const patchedKeys = Object.keys(patch);
    const positivePatchCount = Object.values(patch).filter((v) => String(v).trim().length > 0).length;
    if (patchedKeys.length > 0) {
      this.form.patchValue(patch);
      this.scrollCustomerFormFieldsIntoView();
      for (const k of patchedKeys) {
        if (String(patch[k]).trim()) this.ocrHighlightFields.add(k);
      }
      if (this.clearHighlightTimeoutId != null) {
        clearTimeout(this.clearHighlightTimeoutId);
      }
      this.clearHighlightTimeoutId = window.setTimeout(() => {
        for (const k of patchedKeys) {
          this.ocrHighlightFields.delete(k);
        }
      }, 2600);
    }

    const cnicEffective = (patch['cnic'] || ex.cnic || '').toString().trim();
    if (cnicEffective) {
      this.warnIfDuplicateCnic(cnicEffective);
    }

    const filled = [
      ex.fullName ? 'name' : '',
      ex.fatherName ? 'father name' : '',
      ex.phone ? 'phone' : '',
      ex.email ? 'email' : '',
      ex.cnic ? 'CNIC' : '',
      ex.gender ? 'gender' : '',
      ex.dateOfBirth ? 'date of birth' : '',
      ex.nationality ? 'nationality' : '',
      ex.address ? 'address' : ''
    ].filter(Boolean);
    if (showSummaryToast) {
      if (positivePatchCount > 0) {
        this.cnicLastOcrSuccessMessage = `✓ CNIC processed successfully — ${positivePatchCount} field(s) updated automatically.`;
        if (this.clearSuccessMessageTimeoutId != null) {
          clearTimeout(this.clearSuccessMessageTimeoutId);
        }
        this.clearSuccessMessageTimeoutId = window.setTimeout(() => {
          this.cnicLastOcrSuccessMessage = null;
        }, 8000);
      } else if (patchedKeys.length > 0 && positivePatchCount === 0) {
        this.toast.success(
          'Unreliable OCR text was removed from one or more fields. Enter details manually or upload a sharper image.');
      } else if (filled.length > 0) {
        this.toast.success(`CNIC OCR filled: ${filled.join(', ')}. Please verify before saving.`);
      } else {
        this.toast.warning('No reliable CNIC fields found. Please fill manually.');
      }
    }
  }

  private scrollCustomerFormFieldsIntoView(): void {
    queueMicrotask(() => {
      document.querySelector('.form-card .cnic-section-title')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    });
  }

  /** Removes scans, OCR metadata, and auto-filled identity fields (keeps phone & email). */
  clearCnicScans(): void {
    if (this.cnicUploadBusy || this.cnicOcrBusy || this.loadingCustomer) return;
    this.clearCnicUploadState();
    this.form.patchValue({
      fullName: '',
      fatherOrHusbandName: '',
      cnic: '',
      gender: '',
      dateOfBirth: '',
      nationality: '',
      address: ''
    });
    this.toast.success('CNIC scans cleared. Phone and email were left unchanged — re-upload or type details.');
  }

  private warnIfDuplicateCnic(cnic: string): void {
    const norm = cnic.trim();
    if (!norm) return;
    const dup = this.customers.find(
      (c) =>
        (c.cnic || '').trim() === norm &&
        (!this.isEdit || c.id !== this.customerId)
    );
    if (dup) {
      this.toast.success(
        `A customer named "${dup.fullName}" already has this CNIC. Verify before saving.`);
    }
  }

  openCnicLightbox(url: string): void {
    this.cnicLightboxUrl = url;
  }

  ocrJustFilled(key: string): boolean {
    return this.ocrHighlightFields.has(key);
  }

  readonly cnicOcrFieldTotal = 9;

  get cnicOcrFieldsFilledCount(): number {
    const f = this.form.getRawValue() as Record<string, unknown>;
    const keys = [
      'fullName',
      'fatherOrHusbandName',
      'phone',
      'email',
      'cnic',
      'gender',
      'dateOfBirth',
      'nationality',
      'address'
    ];
    return keys.filter((k) => String(f[k] ?? '').trim().length > 0).length;
  }

  get cnicOcrVerificationRows(): { label: string; ok: boolean }[] {
    if (!this.cnicOcrProvider) return [];
    const f = this.form.getRawValue() as Record<string, unknown>;
    const v = (k: string) => String(f[k] ?? '').trim().length > 0;
    return [
      { label: 'Full name', ok: v('fullName') },
      { label: 'Father / husband name', ok: v('fatherOrHusbandName') },
      { label: 'CNIC', ok: v('cnic') },
      { label: 'Gender', ok: v('gender') },
      { label: 'Date of birth', ok: v('dateOfBirth') },
      { label: 'Nationality', ok: v('nationality') },
      { label: 'Address (English)', ok: v('address') },
      { label: 'Phone', ok: v('phone') },
      { label: 'Email', ok: v('email') }
    ];
  }

  get cnicOcrProgressSteps(): { label: string; state: 'pending' | 'active' | 'done' }[] {
    const oriented = !!(this.cnicFrontPreviewDataUrl || this.cnicBackPreviewDataUrl);
    const hasSelection = !!(this.cnicFrontFileName || this.cnicBackFileName);
    const ocrDone = !!this.cnicOcrProvider && !this.cnicOcrBusy;

    let step1: 'pending' | 'active' | 'done' = 'pending';
    if (oriented) step1 = 'done';
    else if (hasSelection && (this.cnicUploadBusy || this.cnicOcrBusy || this.cnicFrontInstantPreviewUrl || this.cnicBackInstantPreviewUrl)) {
      step1 = 'active';
    }

    let step2: 'pending' | 'active' | 'done' = 'pending';
    if (ocrDone) step2 = 'done';
    else if (this.cnicOcrBusy || (oriented && !this.cnicOcrProvider)) step2 = 'active';

    let step3: 'pending' | 'active' | 'done' = 'pending';
    let step3Label = 'Permanent address (Urdu → English)';
    if (ocrDone) {
      step3 = 'done';
      if (this.cnicOcrAddressTranslated === true) {
        step3Label = 'Permanent address translated (Urdu → English)';
      } else if (this.cnicOcrAddressTranslated === false) {
        step3Label = 'Permanent address not auto-translated — verify (no Urdu block or empty result)';
      } else {
        step3Label = 'Address translation not reported — verify if the address is in English';
      }
    } else if (this.cnicOcrBusy) {
      step3 = 'active';
      step3Label = 'Translating permanent address when needed…';
    }

    return [
      { label: 'Images uploaded & oriented for OCR', state: step1 },
      { label: 'Server OCR finished', state: step2 },
      { label: step3Label, state: step3 }
    ];
  }

  async retryCnicOcr(): Promise<void> {
    if (this.cnicUploadBusy || this.cnicOcrBusy || this.loadingCustomer) return;
    this.cnicLastOcrInlineError = null;
    this.cnicOcrLastFailure = false;
    const jobs: { side: 'front' | 'back'; file: File; preview: string }[] = [];
    if (this.cnicOrientedFrontFile && this.cnicFrontPreviewDataUrl) {
      jobs.push({ side: 'front', file: this.cnicOrientedFrontFile, preview: this.cnicFrontPreviewDataUrl });
    }
    if (this.cnicOrientedBackFile && this.cnicBackPreviewDataUrl) {
      jobs.push({ side: 'back', file: this.cnicOrientedBackFile, preview: this.cnicBackPreviewDataUrl });
    }
    if (!jobs.length) {
      this.toast.warning('Upload CNIC images first, then retry OCR.');
      return;
    }
    this.cnicUploadBusy = true;
    this.startSyntheticProgress();
    try {
      for (const j of jobs) {
        await this.extractAndMergeCnicSide(j.side, j.file, j.preview, false);
      }
      this.finalizeCnicMergeAndApply(true);
    } finally {
      this.cnicUploadBusy = false;
      this.finishSyntheticProgress();
    }
  }

  closeCnicLightbox(): void {
    this.cnicLightboxUrl = null;
  }
}
