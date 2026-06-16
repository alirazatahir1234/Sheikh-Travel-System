import { ChangeDetectionStrategy, Component, ElementRef, input, output, viewChild } from '@angular/core';
import { NgClass } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { DocumentSlotState } from '../../models/vehicle-wizard.model';

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
        'border-fleet-border': !dragOver
      }"
      (dragover)="onDragOver($event)"
      (dragleave)="dragOver = false"
      (drop)="onDrop($event)"
      (click)="fileInput.click()">
      <input
        #fileInput
        type="file"
        class="hidden"
        accept=".jpg,.jpeg,.png,.pdf"
        (change)="onFileSelected($event)" />

      @if (slot().uploading) {
        <div class="flex flex-col items-center gap-2 py-4 text-center">
          <div class="h-8 w-8 animate-spin rounded-full border-2 border-fleet-primary border-t-transparent"></div>
          <p class="text-sm text-fleet-text-muted">Uploading…</p>
        </div>
      } @else if (slot().fileUrl) {
        <div class="flex items-center justify-between gap-3">
          <div class="flex items-center gap-3 min-w-0">
            <mat-icon class="text-emerald-600">check_circle</mat-icon>
            <div class="min-w-0">
              <p class="truncate text-sm font-medium text-fleet-text">{{ slot().label }}</p>
              <p class="truncate text-xs text-fleet-text-muted">{{ fileName() }}</p>
            </div>
          </div>
          <button type="button" class="text-sm text-fleet-primary hover:underline" (click)="$event.stopPropagation(); fileInput.click()">
            Replace
          </button>
        </div>
      } @else if (slot().file && slot().error) {
        <div class="flex items-center justify-between gap-3">
          <div class="flex items-center gap-3 min-w-0">
            <mat-icon class="text-fleet-error">error</mat-icon>
            <div class="min-w-0">
              <p class="truncate text-sm font-medium text-fleet-text">{{ slot().label }}</p>
              <p class="truncate text-xs text-fleet-text-muted">{{ fileName() }}</p>
            </div>
          </div>
          <button type="button" class="text-sm text-fleet-primary hover:underline" (click)="$event.stopPropagation(); fileInput.click()">
            Retry
          </button>
        </div>
      } @else {
        <div class="flex flex-col items-center gap-2 py-4 text-center">
          <mat-icon class="text-fleet-text-muted/50" style="font-size:36px;width:36px;height:36px;">cloud_upload</mat-icon>
          <p class="text-sm font-medium text-fleet-text">{{ slot().label }}</p>
          <p class="text-xs text-fleet-text-muted">Drag & drop or click — JPG, PNG, PDF (max 5–10 MB)</p>
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

  fileName(): string {
    const s = this.slot();
    if (s.file) return s.file.name;
    if (s.fileUrl) return s.fileUrl.split('/').pop() ?? 'Uploaded';
    return '';
  }

  onDragOver(e: DragEvent): void {
    e.preventDefault();
    this.dragOver = true;
  }

  onDrop(e: DragEvent): void {
    e.preventDefault();
    this.dragOver = false;
    const file = e.dataTransfer?.files?.[0];
    if (file) this.fileSelected.emit({ index: this.index(), file });
  }

  onFileSelected(e: Event): void {
    const input = e.target as HTMLInputElement;
    const file = input.files?.[0];
    if (file) this.fileSelected.emit({ index: this.index(), file });
    input.value = '';
  }
}
