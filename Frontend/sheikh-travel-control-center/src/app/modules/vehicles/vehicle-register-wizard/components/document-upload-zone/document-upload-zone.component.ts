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
  resolveUploadUrl
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
            <div class="flex min-w-0 items-center gap-3">
              <mat-icon class="text-emerald-600">{{ preview()!.kind === 'pdf' ? 'picture_as_pdf' : 'check_circle' }}</mat-icon>
              <div class="min-w-0">
                <p class="truncate text-sm font-medium text-fleet-text">{{ slot().label }}</p>
                <p class="truncate text-xs text-fleet-text-muted">{{ fileName() }}</p>
              </div>
            </div>
            <div class="flex shrink-0 items-center gap-3">
              @if (preview()!.kind === 'pdf') {
                <a
                  [href]="preview()!.src"
                  target="_blank"
                  rel="noopener noreferrer"
                  class="text-sm text-fleet-text-muted hover:text-fleet-primary"
                  (click)="$event.stopPropagation()">
                  Open
                </a>
              }
              <button type="button" class="text-sm text-fleet-primary hover:underline" (click)="$event.stopPropagation(); openFilePicker()">
                Replace
              </button>
            </div>
          </div>
        </div>
      } @else if (slot().file && slot().error) {
        <div class="flex items-center justify-between gap-3">
          <div class="flex min-w-0 items-center gap-3">
            <mat-icon class="text-fleet-error">error</mat-icon>
            <div class="min-w-0">
              <p class="truncate text-sm font-medium text-fleet-text">{{ slot().label }}</p>
              <p class="truncate text-xs text-fleet-text-muted">{{ fileName() }}</p>
            </div>
          </div>
          <button type="button" class="text-sm text-fleet-primary hover:underline" (click)="$event.stopPropagation(); openFilePicker()">
            Retry
          </button>
        </div>
      } @else {
        <div class="flex flex-col items-center gap-2 py-4 text-center">
          <mat-icon class="text-fleet-text-muted/50" style="font-size:36px;width:36px;height:36px;">cloud_upload</mat-icon>
          <p class="text-sm font-medium text-fleet-text">{{ slot().label }}</p>
          <p class="text-xs text-fleet-text-muted">Drag & drop or click — JPG, PNG, or PDF (max 5–10 MB)</p>
          @if (slot().required) {
            <span class="text-xs font-medium text-fleet-error">Required</span>
          }
        </div>
      }

      @if (slot().error) {
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

  fileName(): string {
    const s = this.slot();
    if (s.file) return s.file.name;
    if (s.fileUrl) return decodeURIComponent(s.fileUrl.split('/').pop() ?? 'Uploaded');
    return '';
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
    this.fileSelected.emit({ index: this.index(), file });
  }

  onFileSelected(e: Event): void {
    const input = e.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file || !this.isAcceptedFile(file)) return;
    this.fileSelected.emit({ index: this.index(), file });
    input.value = '';
  }

  private isAcceptedFile(file: File): boolean {
    if (this.slot().documentType === 'VehicleImage') {
      return isImageFile(file);
    }
    return isImageFile(file) || isPdfFile(file);
  }
}
