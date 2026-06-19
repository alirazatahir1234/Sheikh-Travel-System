import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  ElementRef,
  input,
  output,
  signal,
  viewChild
} from '@angular/core';
import { NgClass } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { DocumentSlotState } from '../../models/vehicle-wizard.model';
import {
  isImageFile,
  isImageUploadUrl,
  isPdfFile,
  isPdfUploadUrl,
  resolveUploadUrl,
  vehicleUploadSizeError
} from '../../../../../core/utils/upload-url.util';

type DocumentPreview = { kind: 'image' | 'pdf'; src: string };

@Component({
  selector: 'app-document-upload-zone',
  standalone: true,
  imports: [MatIconModule, NgClass],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      class="rounded-lg border-2 border-dashed p-5 transition-colors"
      [ngClass]="{
        'border-fleet-primary bg-fleet-primary-soft': dragOver,
        'border-fleet-border': !dragOver,
        'border-solid': !!preview()
      }"
      (dragover)="onDragOver($event)"
      (dragleave)="dragOver = false"
      (drop)="onDrop($event)"
      (click)="onZoneClick($event)">
      <input
        #fileInput
        type="file"
        class="hidden"
        [accept]="acceptTypes()"
        (change)="onFileSelected($event)" />

      @if (slot().uploading) {
        <div class="flex flex-col items-center gap-2 py-4 text-center">
          <div class="h-8 w-8 animate-spin rounded-full border-2 border-fleet-primary border-t-transparent"></div>
          <p class="text-sm text-fleet-text-muted">Uploading {{ slot().label }}…</p>
        </div>
      } @else if (preview()) {
        <div class="space-y-3">
          <div class="overflow-hidden rounded-md border border-fleet-border bg-fleet-surface-muted">
            @if (preview()!.kind === 'image') {
              <img [src]="preview()!.src" [alt]="slot().label" class="mx-auto max-h-52 w-full object-contain" />
            } @else {
              <iframe
                [src]="pdfViewerSrc(preview()!.src)"
                [title]="slot().label + ' preview'"
                class="h-56 w-full bg-white"
                loading="lazy"></iframe>
            }
          </div>

          <div class="flex items-center justify-between gap-3">
            <div class="flex min-w-0 flex-1 items-center gap-3">
              <mat-icon class="shrink-0 text-emerald-600">{{ preview()!.kind === 'pdf' ? 'picture_as_pdf' : 'check_circle' }}</mat-icon>
              <div class="min-w-0">
                <p class="truncate text-sm font-medium text-fleet-text">{{ slot().label }}</p>
              </div>
            </div>
          </div>

          <div class="flex items-stretch gap-2 border-t border-fleet-border pt-3">
            @if (preview()!.kind === 'pdf') {
              <a
                [href]="preview()!.src"
                target="_blank"
                rel="noopener noreferrer"
                class="flex flex-1 items-center justify-center rounded-sm border border-fleet-border bg-fleet-surface-muted px-3 py-1.5 text-xs font-semibold text-fleet-text no-underline transition-colors hover:border-fleet-primary/40 hover:bg-fleet-primary-soft hover:text-fleet-primary"
                (click)="$event.stopPropagation()">
                Open
              </a>
            }
            <button
              type="button"
              class="flex flex-1 items-center justify-center rounded-sm border border-fleet-primary/50 bg-white px-3 py-1.5 text-xs font-semibold text-fleet-primary transition-colors hover:bg-fleet-primary-soft"
              (click)="$event.stopPropagation(); openFilePicker()">
              Replace
            </button>
          </div>
        </div>
      } @else if (slot().file && slot().error) {
        <div class="flex items-center justify-between gap-3">
          <div class="flex min-w-0 flex-1 items-center gap-3">
            <mat-icon class="shrink-0 text-fleet-error">error</mat-icon>
            <div class="min-w-0">
              <p class="truncate text-sm font-medium text-fleet-text">{{ slot().label }}</p>
            </div>
          </div>
        </div>
        <div class="mt-3 border-t border-fleet-border pt-3">
          <button
            type="button"
            class="w-full rounded-sm border border-fleet-primary/50 bg-white px-3 py-1.5 text-xs font-semibold text-fleet-primary transition-colors hover:bg-fleet-primary-soft"
            (click)="$event.stopPropagation(); openFilePicker()">
            Retry
          </button>
        </div>
      } @else {
        <div class="flex flex-col items-center gap-2 py-4 text-center">
          <mat-icon class="text-fleet-text-muted/50" style="font-size:36px;width:36px;height:36px;">cloud_upload</mat-icon>
          <p class="text-sm font-medium text-fleet-text">{{ slot().label }}</p>
          <p class="text-xs text-fleet-text-muted">Drag & drop or click — JPG, PNG, or PDF (max 2 MB)</p>
          @if (slot().required) {
            <span class="text-xs font-medium text-fleet-error">Required</span>
          }
        </div>
      }

      @if (sizeError()) {
        <p class="mt-2 text-center text-xs text-fleet-error">{{ sizeError() }}</p>
      } @else if (slot().error) {
        <p class="mt-2 text-center text-xs text-fleet-error">{{ slot().error }}</p>
      }
    </div>
  `
})
export class DocumentUploadZoneComponent {
  readonly slot = input.required<DocumentSlotState>();
  readonly index = input.required<number>();
  readonly fileSelected = output<{ index: number; file: File }>();

  readonly fileInput = viewChild<ElementRef<HTMLInputElement>>('fileInput');
  dragOver = false;
  readonly sizeError = signal<string | null>(null);

  private readonly localPreviewUrl = signal<string | null>(null);

  readonly acceptTypes = computed(() =>
    this.slot().documentType === 'VehicleImage'
      ? '.jpg,.jpeg,.png,.webp,.gif'
      : '.jpg,.jpeg,.png,.pdf'
  );

  readonly preview = computed((): DocumentPreview | null => {
    const slot = this.slot();
    const remoteUrl = slot.fileUrl ? resolveUploadUrl(slot.fileUrl) : null;

    if (remoteUrl) {
      if (isImageUploadUrl(slot.fileUrl)) return { kind: 'image', src: remoteUrl };
      if (isPdfUploadUrl(slot.fileUrl)) return { kind: 'pdf', src: remoteUrl };
    }

    const local = this.localPreviewUrl();
    const file = slot.file;
    if (local && file) {
      if (isPdfFile(file)) return { kind: 'pdf', src: local };
      if (isImageFile(file)) return { kind: 'image', src: local };
    }

    return null;
  });

  private readonly syncLocalPreview = effect((onCleanup) => {
    const file = this.slot().file;

    if (!file || (!isImageFile(file) && !isPdfFile(file))) {
      this.localPreviewUrl.set(null);
      return;
    }

    const blobUrl = URL.createObjectURL(file);
    this.localPreviewUrl.set(blobUrl);
    onCleanup(() => URL.revokeObjectURL(blobUrl));
  });

  openFilePicker(): void {
    this.fileInput()?.nativeElement.click();
  }

  pdfViewerSrc(src: string): string {
    return src.includes('#') ? src : `${src}#toolbar=0&navpanes=0`;
  }

  onZoneClick(event: MouseEvent): void {
    if ((event.target as HTMLElement).closest('a,button,iframe')) return;
    this.openFilePicker();
  }

  onDragOver(e: DragEvent): void {
    e.preventDefault();
    this.dragOver = true;
  }

  onDrop(e: DragEvent): void {
    e.preventDefault();
    this.dragOver = false;
    const file = e.dataTransfer?.files?.[0];
    if (!file || !this.isAcceptedFile(file)) return;
    this.queueFile(file);
  }

  onFileSelected(e: Event): void {
    const input = e.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file || !this.isAcceptedFile(file)) return;
    this.queueFile(file);
    input.value = '';
  }

  private queueFile(file: File): void {
    const sizeError = vehicleUploadSizeError(file);
    if (sizeError) {
      this.sizeError.set(sizeError);
      return;
    }
    this.sizeError.set(null);
    this.fileSelected.emit({ index: this.index(), file });
  }

  private isAcceptedFile(file: File): boolean {
    if (this.slot().documentType === 'VehicleImage') {
      return isImageFile(file);
    }
    return isImageFile(file) || isPdfFile(file);
  }
}
