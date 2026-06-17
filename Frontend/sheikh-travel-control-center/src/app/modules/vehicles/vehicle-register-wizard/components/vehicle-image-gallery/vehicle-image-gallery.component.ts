import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { NgClass } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { VehicleImageSlotState } from '../../models/vehicle-wizard.model';
import { resolveUploadUrl } from '../../../../../core/utils/upload-url.util';

@Component({
  selector: 'app-vehicle-image-gallery',
  standalone: true,
  imports: [MatIconModule, NgClass],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="space-y-3">
      <div class="flex items-center justify-between gap-3">
        <div>
          <p class="text-sm font-medium text-fleet-text">Vehicle Images</p>
          <p class="text-xs text-fleet-text-muted">Upload front, side, and back photos. Click a photo to set the display image.</p>
        </div>
        @if (hasAnyImage()) {
          <span class="text-xs text-fleet-text-muted">{{ uploadedCount() }} / {{ slots().length }} uploaded</span>
        }
      </div>

      <div class="grid grid-cols-3 gap-3">
        @for (slot of slots(); track slot.angle; let i = $index) {
          <div
            class="group relative overflow-hidden rounded-lg border-2 transition-all"
            [ngClass]="{
              'border-fleet-primary ring-2 ring-fleet-primary/20': slot.isPrimary && slot.fileUrl,
              'border-fleet-border': !slot.isPrimary || !slot.fileUrl,
              'border-dashed': !slot.fileUrl && !slot.uploading,
              'cursor-pointer': !!slot.fileUrl && !slot.uploading
            }"
            (click)="onSlotClick(i)">
            <input
              #angleFileInput
              type="file"
              class="hidden"
              accept=".jpg,.jpeg,.png,.webp,.gif"
              (change)="onFileSelected($event, i)" />

            @if (slot.uploading) {
              <div class="flex aspect-[4/3] flex-col items-center justify-center gap-2 bg-fleet-surface-muted p-3">
                <div class="h-7 w-7 animate-spin rounded-full border-2 border-fleet-primary border-t-transparent"></div>
                <p class="text-xs text-fleet-text-muted">Uploading…</p>
              </div>
            } @else if (previewUrl(slot)) {
              <div class="relative aspect-[4/3] bg-fleet-surface-muted">
                <img [src]="previewUrl(slot)!" [alt]="slot.label" class="h-full w-full object-cover" />
                @if (slot.isPrimary) {
                  <span class="absolute left-2 top-2 rounded bg-fleet-primary px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wide text-white">
                    Display
                  </span>
                }
                <div class="absolute inset-x-0 bottom-0 bg-gradient-to-t from-black/60 to-transparent px-2 pb-2 pt-6">
                  <p class="text-xs font-medium text-white">{{ slot.label }}</p>
                </div>
              </div>
              <div class="flex items-center justify-between gap-2 border-t border-fleet-border bg-white px-2 py-1.5">
                <button
                  type="button"
                  class="text-xs text-fleet-primary hover:underline"
                  (click)="$event.stopPropagation(); angleFileInput.click()">
                  Replace
                </button>
                @if (!slot.isPrimary) {
                  <button
                    type="button"
                    class="text-xs text-fleet-text-muted hover:text-fleet-primary"
                    (click)="$event.stopPropagation(); selectPrimary.emit(i)">
                    Set display
                  </button>
                }
              </div>
            } @else {
              <button
                type="button"
                class="flex aspect-[4/3] w-full flex-col items-center justify-center gap-1.5 bg-fleet-surface-muted/50 p-3 text-center hover:bg-fleet-primary-soft/40"
                (click)="$event.stopPropagation(); angleFileInput.click()">
                <mat-icon class="text-fleet-text-muted/50" style="font-size:28px;width:28px;height:28px;">add_a_photo</mat-icon>
                <p class="text-xs font-medium text-fleet-text">{{ slot.label }}</p>
                <p class="text-[10px] text-fleet-text-muted">JPG, PNG, WEBP</p>
              </button>
            }

            @if (slot.error) {
              <p class="px-2 py-1 text-center text-[10px] text-fleet-error">{{ slot.error }}</p>
            }
          </div>
        }
      </div>

      @if (showRequiredError()) {
        <p class="text-xs text-fleet-error">At least one vehicle image is required.</p>
      }
    </div>
  `
})
export class VehicleImageGalleryComponent {
  readonly slots = input.required<VehicleImageSlotState[]>();
  readonly showRequiredError = input(false);

  readonly fileSelected = output<{ index: number; file: File }>();
  readonly selectPrimary = output<number>();

  readonly uploadedCount = computed(() => this.slots().filter(s => !!s.fileUrl).length);
  readonly hasAnyImage = computed(() => this.uploadedCount() > 0);

  previewUrl(slot: VehicleImageSlotState): string | null {
    if (slot.file) return URL.createObjectURL(slot.file);
    return slot.fileUrl ? resolveUploadUrl(slot.fileUrl) : null;
  }

  onSlotClick(index: number): void {
    const slot = this.slots()[index];
    if (slot?.fileUrl && !slot.uploading) {
      this.selectPrimary.emit(index);
    }
  }

  onFileSelected(event: Event, index: number): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    if (!file.type.startsWith('image/')) return;
    this.fileSelected.emit({ index, file });
    input.value = '';
  }
}
