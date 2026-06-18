import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { DocumentSlotState, VehicleImageSlotState } from '../../../models/vehicle-wizard.model';
import { DocumentUploadZoneComponent } from '../../document-upload-zone/document-upload-zone.component';
import { VehicleImageGalleryComponent } from '../../vehicle-image-gallery/vehicle-image-gallery.component';

@Component({
  selector: 'app-wizard-step-documents',
  standalone: true,
  imports: [DocumentUploadZoneComponent, VehicleImageGalleryComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="rounded-lg border border-fleet-border bg-white p-6 shadow-sm">
      <h2 class="mb-1 text-lg font-semibold text-fleet-text">Documents</h2>
      <p class="mb-5 text-sm text-fleet-text-muted">Upload vehicle images and compliance documents.</p>

      <div class="space-y-6">
        <app-vehicle-image-gallery
          [slots]="vehicleImageSlots()"
          [showRequiredError]="showImageRequiredError()"
          (fileSelected)="vehicleImageSelected.emit($event)"
          (selectPrimary)="selectPrimaryImage.emit($event)" />

        <div class="grid gap-4 md:grid-cols-1">
          @for (slot of slots(); track slot.documentType; let i = $index) {
            <app-document-upload-zone
              [slot]="slot"
              [index]="i"
              (fileSelected)="fileSelected.emit($event)" />
          }
        </div>
      </div>
    </section>
  `
})
export class WizardStepDocumentsComponent {
  readonly slots = input.required<DocumentSlotState[]>();
  readonly vehicleImageSlots = input.required<VehicleImageSlotState[]>();
  readonly showImageRequiredError = input(false);
  readonly fileSelected = output<{ index: number; file: File }>();
  readonly vehicleImageSelected = output<{ index: number; file: File }>();
  readonly selectPrimaryImage = output<number>();
}
